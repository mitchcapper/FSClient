using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using FSClient;
using JA = JabraTelephonyAPI;
namespace JabraHeadsetPlugin {
	public class JabraHeadset : IHeadsetDevice {

		//ake sure mic mute actually goes both ways
		internal JA.IDevice device;
		public string device_path { get; private set; }
		private string device_name;
		public override string GetName() {
			return device_name;
		}
		public override string GetManufacturer() {
			return "Jabra";
		}
		public override string GetModel() {
			return device_name;
		}
		public override string GetUniqueId() {
			return "Jabra " + device_path;
		}
		public override void SetActive(bool active) {
			if (active && !device.Locked) {
				device.Lock();
				device.SetRinger(false, "");
				device.SetHookState(false);
			} else if (!active && device.Locked)
				device.Unlock();
		}
		private string last_caller_id;
		public override void SetCallerId(string name, string number) {
			last_caller_id = name + "-" + number;
			if (last_caller_id.Length > 20)
				last_caller_id = last_caller_id.Substring(0, 20);
			device.SetCallerId(last_caller_id);
		}
		public JabraHeadset(JA.IDevice device) {
			this.device = device;
			device_path = device.DeviceHandle.ToString();
			device_name = device.Name;
		}
		private IDeviceHost host;
		private void HostStatusChanged(object sender, IDeviceHost.StatusEventArgs e) {
			switch (e.type) {
				case IDeviceHost.PHONE_EVENT_TYPE.InCallRing:
				case IDeviceHost.PHONE_EVENT_TYPE.Ring:
					device.SetRinger(e.enable, last_caller_id);
					break;
				case IDeviceHost.PHONE_EVENT_TYPE.LineActive:
					if (e.enable == false && muted)
						device.SetMicrophoneMute(false);
					device.SetHookState(e.enable);
					hook_enabled = e.enable;
					break;
				case IDeviceHost.PHONE_EVENT_TYPE.Mute:
					device.SetMicrophoneMute(e.enable);
					muted = e.enable;
					break;
			}

		}
		public override HEADSET_LIMITATIONS Initalize(IDeviceHost host) {
			this.host = host;
			device.SetButtonEventHandler(OnButtonEvent);
			device.SetStateEventHandler(OnStateEvent);


			host.StatusChanged += HostStatusChanged;
			return HEADSET_LIMITATIONS.ONLY_ACTIVE_DURING_CALLS;
		}
		/*
		 * So jabra devices are a bit annoying, 'button' events are a bit less about button presses and a bit more about events
		 * */
		private bool hook_enabled;
		private bool muted = false;
		private string last_event_hash;
		private DateTime last_event;
		private void OnButtonEvent(JA.IDevice device, JA.ButtonEvent button, bool value) {
			try {
				var now = DateTime.Now;
				Debug.WriteLine($"Jabra::OnButtonEvent {now}.{now.Millisecond} {button} {value} is muted: {device.MicrophoneMuted}");//this gets called twice, at least for bluetooth devices for each event, second event does have right mute state microphone muted, with the
				var diff = DateTime.UtcNow - last_event;
				last_event = DateTime.UtcNow;
				if (diff.TotalMilliseconds < 300) {
					var hash = "" + device.DeviceHandle + button + value;
					if (hash == last_event_hash) {
						Debug.WriteLine("Ignoring dupe event within 300 ms of last");
						return;
					}
					last_event_hash = hash;
				}

				switch (button) {
					case JA.ButtonEvent.HookSwitch:
						if (value) {
							if (!hook_enabled)
								StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Talk));
							hook_enabled = true;
						} else {
							if (hook_enabled) {
								StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Hangup));
								device.SetHookState(false);
							}
							hook_enabled = false;
						}
						break;
					case JA.ButtonEvent.MicMute:
						if (!ignore_next_mute)//only done on startup event
							StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.ToggleMute));
						ignore_next_mute = false;
						break;
					case JA.ButtonEvent.Flash:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Flash));
						break;
					case JA.ButtonEvent.RejectCall:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Hangup));
						break;
					case JA.ButtonEvent.FireAlarm:
						throw new Exception("WTF");
					case JA.ButtonEvent.Redial:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Redial));
						break;

				}
			} catch (Exception) { }
		}
		private bool ignore_next_mute;
		private void OnStateEvent(JA.IDevice device, JA.State state, bool value) {
			switch (state) {
				case JA.State.OnLine:
					if (value) {
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.RadioOpen));
						if (muted) {
							DelayedFunction.DelayedCall("jabra_mute", () => {
								ignore_next_mute = true;
								device.SetMicrophoneMute(muted);

							}, 2500);
						}
					} else
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.RadioClosed));
					break;
			}
		}



	}

	public class JabraProvider : IHeadsetPlugin {
		private void AddNewDevice(JA.IDevice ja_device) {
			try {
				JabraHeadset device = new JabraHeadset(ja_device);
				devices.Add(device);
				DeviceAdded(this, new DeviceEventArgs(device));
			} catch (System.IO.FileNotFoundException) {
				Utils.PluginLog("Jabra Provider", "Unable to add new device");
			}
		}
		private void OnDeviceAttached(JA.IDevice device) {
			AddNewDevice(device);
		}

		private void OnDeviceDetached(JA.IDevice device) {
			JabraHeadset headset = (from d in devices where d.device == device select d).SingleOrDefault();
			if (headset != null)
				DeviceRemoved(this, new DeviceEventArgs(headset));
		}
		Timer shutdown_timer;
		private void OnShutdown(bool serverIsShuttingDown) {
			Debug.WriteLine("Shutting down jabra notice, we will restart in 15 seconds");
			if (shutdown_timer == null) {

				shutdown_timer = new Timer(15000);
				shutdown_timer.Elapsed += shutdowntimer_Elapsed;
			}
			shutdown_timer.Stop();
			JA.DeviceServiceConnector.Disconnect();
			shutdown_timer.Start();
		}

		void shutdowntimer_Elapsed(object sender, ElapsedEventArgs e) {
			shutdown_timer.Stop();
			Initialize();
		}
		public override string ProviderName() {
			return "JabraProvider";
		}
		public override void Initialize() {
			try {
				JA.DeviceServiceConnector.Connect(new Guid("b91c9121-0a17-4b26-a09d-d5980eb532db"), "FSClient", new Version("1.0.0.0"), true, OnShutdown, OnDeviceAttached, OnDeviceDetached);
				JA.DeviceServiceConnector.SetSoftphoneAvailable(true);
			} catch (System.IO.FileNotFoundException) {
				Utils.PluginLog("Jabra Provider", "Unable to startup Jabra Suite, most likely due to not installed, headset support skipped");
			} catch (Exception) {
				if (shutdown_timer != null) {
					shutdown_timer.Stop();
					shutdown_timer.Start();
				}
			}
		}
		private List<JabraHeadset> devices = new List<JabraHeadset>();

		public override void Terminate() {
			try {
				JA.DeviceServiceConnector.SetSoftphoneAvailable(false);
			} catch { }
			JA.DeviceServiceConnector.Disconnect();
		}

	}
}