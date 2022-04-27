using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using FSClient;
using JabraSDK;
using JA = JabraSDK;
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
			if (active && !device.IsLocked) {
				device.Lock();
				device.SetRinger(false);
				//device.SetCallerId("");
				device.SetHookState(false);
			} else if (!active && device.IsLocked)
				device.Unlock();
		}
		private string last_caller_id;
		public override void SetCallerId(string name, string number) {
			last_caller_id = name + "-" + number;
			if (last_caller_id.Length > 20)
				last_caller_id = last_caller_id.Substring(0, 20);
			//device.SetCallerId(last_caller_id);
		}
		public JabraHeadset(JA.IDevice device) {
			this.device = device;
			device_path = device.UsbDevicePath;
			device_name = device.Name;
			
		}
		private IDeviceHost host;
		private void HostStatusChanged(object sender, IDeviceHost.StatusEventArgs e) {
			var now = DateTime.Now;
			Debug.WriteLine($"Jabr OurHost status changed {now}.{now.Millisecond}: {e.type} enable: {e.enable}");
			switch (e.type) {
				case IDeviceHost.PHONE_EVENT_TYPE.InCallRing:
				case IDeviceHost.PHONE_EVENT_TYPE.Ring:
					device.SetRinger(e.enable);
					//device.SetCallerId(last_caller_id);
					break;
				case IDeviceHost.PHONE_EVENT_TYPE.LineActive:
					if (e.enable == false && muted)
						device.SetMicrophoneMuted(false);
					device.SetHookState(e.enable);
					hook_enabled = e.enable;
					break;
				case IDeviceHost.PHONE_EVENT_TYPE.Mute:
					//if (device.IsOffHook)
					device.SetMicrophoneMuted(e.enable);//doesn't work and screws up state otherwise
					muted = e.enable;
					break;
			}

		}
		public override HEADSET_LIMITATIONS Initalize(IDeviceHost host) {
			this.host = host;
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
		public void OnButtonEvent(object sender, TranslatedButtonInputEventArgs e) {
			try {
				
				var now = DateTime.Now;
				var button = e.ButtonId;
				var value = e.Value ?? false;
				Debug.WriteLine($"Jabra::OnButtonEvent {now}.{now.Millisecond} {button} {value} is muted: {muted}");//this gets called twice, at least for bluetooth devices for each event, second event does have right mute state microphone muted, with the
				var diff = DateTime.UtcNow - last_event;
				last_event = DateTime.UtcNow;
				if (diff.TotalMilliseconds < 300) {
					var hash = "" + device.UsbDevicePath + button + value;
					if (hash == last_event_hash) {
						Debug.WriteLine("Ignoring dupe event within 300 ms of last");
						return;
					}
					last_event_hash = hash;
				}

				switch (button) {
					case ButtonId.OffHook:
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
					case ButtonId.Mute:
						if (!ignore_next_mute)//only done on startup event
							StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.ToggleMute));
						ignore_next_mute = false;
						break;
					case ButtonId.Flash:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Flash));
						break;
					case ButtonId.RejectCall:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Hangup));
						break;
					case  ButtonId.FireAlarm:
						throw new Exception("WTF");
					case  ButtonId.Redial:
						StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Redial));
						break;
					case ButtonId.Online:
						if (value) {
							ignore_next_mute = true;
							DelayedFunction.DelayedCall("jabra_mute", () => {
								now = DateTime.Now;
								Debug.WriteLine($"Jabra Calling SetMicrophoneMuted {now}.{now.Millisecond}: {this.muted} after we come online");
								device.SetMicrophoneMuted(! muted);
								device.SetMicrophoneMuted(muted);

							}, 2500);
						} else
							StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.RadioClosed));
						
						break;

				}
			} catch (Exception) { }
		}
		private bool ignore_next_mute;
		



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
		private void OnDeviceAttached(object sender, DeviceAddedEventArgs e) {
			AddNewDevice(e.Device);
		}

		private void OnDeviceDetached(object sender, DeviceRemovedEventArgs e) {
			JabraHeadset headset = (from d in devices where d.device == e.Device select d).SingleOrDefault();
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
			deviceService.Dispose();
			shutdown_timer.Start();
		}

		void shutdowntimer_Elapsed(object sender, ElapsedEventArgs e) {
			shutdown_timer.Stop();
			Initialize();
		}
		public override string ProviderName() {
			return "JabraProvider";
		}
		private JA.ServiceFactory factory;
		private JA.IIntegrationService integrationService;
		private JA.IDeviceService deviceService;
		private Guid clientGUID = new Guid("b91c9121-0a17-4b26-a09d-d5980eb532db");
		public override void Initialize() {
			try {
				factory = new JA.ServiceFactory();
				factory.SetClientId(clientGUID.ToString());
				integrationService = factory.CreateIntegrationService();
				integrationService.ConnectClient(clientGUID, "FSClient", new Version("1.0.0.0"));
				integrationService.ClientState = JA.ClientStateId.ReadyForTelephony;

				deviceService = factory.CreateDeviceService();
				deviceService.DeviceAdded += OnDeviceAttached;
				deviceService.DeviceRemoved += OnDeviceDetached;
				deviceService.TranslatedButtonInput += OnButtonEvent;
				deviceService.SetStdHIDEventsFromJabraDevices(false);



			} catch (System.IO.FileNotFoundException) {
				Utils.PluginLog("Jabra Provider", "Unable to startup Jabra Suite, most likely due to not installed, headset support skipped");
			} catch (Exception) {
				if (shutdown_timer != null) {
					shutdown_timer.Stop();
					shutdown_timer.Start();
				}
			}
		}

		private void OnButtonEvent(object sender, TranslatedButtonInputEventArgs e) {
			var dev = (from d in devices where d.device.DeviceId == e.DeviceId select d).SingleOrDefault();
			dev.OnButtonEvent(sender, e);
		}

		private List<JabraHeadset> devices = new List<JabraHeadset>();

		public override void Terminate() {
			try {
				integrationService.ClientState = ClientStateId.NotReadyForTelephony;

			} catch { }
			try {
				integrationService.DisconnectClient(clientGUID);
			} catch { }
		}

	}
}
