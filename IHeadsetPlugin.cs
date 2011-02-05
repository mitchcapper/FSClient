using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FSClient {
	public abstract class IHeadsetDevice {
		[Flags]
		public enum HEADSET_LIMITATIONS { NO_UNMUTE, ONLY_ACTIVE_DURING_CALLS, ANAL };
		public abstract string GetName();
		public abstract string GetManufacturer();
		public abstract string GetModel();
		public abstract string GetUniqueId();


		public abstract HEADSET_LIMITATIONS Initalize(IDeviceHost host);
		public abstract void SetCallerId(String name, String number);
		public abstract void SetActive(bool active);

		public enum HEADSET_EVENT_TYPE { Talk, Hangup, ToggleTalk, ToggleMute, Mute, UnMute, Flash, Redial, RadioOpen, RadioClosed };
		public class StatusEventArgs : EventArgs {
			public HEADSET_EVENT_TYPE type { get; set; }
			public StatusEventArgs(HEADSET_EVENT_TYPE type) {
				this.type = type;
			}

		}
		public EventHandler<StatusEventArgs> StatusChanged;
	}
	public class IDeviceHost {
		public class StatusEventArgs : EventArgs {
			public PHONE_EVENT_TYPE type { get; set; }
			public bool enable { get; set; }
			public StatusEventArgs(PHONE_EVENT_TYPE type, bool enable) {
				this.type = type;
				this.enable = enable;
			}

		}
		public EventHandler<StatusEventArgs> StatusChanged;
		public enum PHONE_EVENT_TYPE { Ring, Mute, LineActive, Hold, InCallRing };
		internal void CreateEvent(PHONE_EVENT_TYPE type, bool enable) {
			if (StatusChanged != null)
				StatusChanged(this, new StatusEventArgs(type, enable));
		}
	}

	public class HeadsetPluginManager : IDisposable {
		private class DeviceData {
			public IDeviceHost host;
			public IHeadsetDevice device;
			public bool active;
			public bool in_use;
			public PluginData plugin;
			public IHeadsetDevice.HEADSET_LIMITATIONS limitations;
			public bool HasLimit(IHeadsetDevice.HEADSET_LIMITATIONS limit) {
				return (limitations & limit) == limit;
			}
		}
		private class PluginData{
			public IHeadsetPlugin plugin;
			public int error_tries_left = 0;
			public DateTime last_error_time = DateTime.Now;//start with now so error count will not reset right away
		}
		private class PluginError{
			public DeviceData device;
			public PluginData plugin;
			public Exception exception;
		}
		private object devices_lock = new object();
		private Broker broker;
		private const int DEFAULT_PLUGIN_RETRIES = 5;
		private List<DeviceData> devices = new List<DeviceData>();

		private List<PluginData> plugins = new List<PluginData>();
		public string[] AvailableDevices() {
			return (from d in devices select d.device.GetName()).ToArray();
		}
		public string ActiveDevice() {
			return (from d in devices where d.active select d.device.GetName()).SingleOrDefault();
		}
		private string active_device_name;
		private string last_active_device_name;
		public void SetActiveDevice(String name) {
			active_device_name = name;
			bool found_already = false;
			List<PluginError> errors = null;

			lock (devices_lock) {
				foreach (DeviceData device in devices) {
					try {
						if (device.device.GetName() == name && !found_already) {
							found_already = true;
							if (!device.active) {
								device.active = device.in_use = true;
								device.device.SetActive(true);
							}
						}
						else if (device.active) {
							device.active = device.in_use = false;
							device.device.SetActive(false);
						}
					}
					catch (Exception e) {
						if (errors == null)
							errors = new List<PluginError>();
						errors.Add(new PluginError{device = device, exception = e});
					}
				}
			}
			if (errors != null)
				foreach (PluginError err in errors)
					HandleError(err);
		}
		private void Ring(bool enable) {
			CreateEvent(IDeviceHost.PHONE_EVENT_TYPE.Ring, enable);
		}
		private void Mute(bool enable) {
			CreateEvent(IDeviceHost.PHONE_EVENT_TYPE.Mute, enable);
		}
		private void LineActive(bool enable, bool in_call) {
			CreateEvent(IDeviceHost.PHONE_EVENT_TYPE.LineActive, enable, in_call);
		}
		private void Hold(bool enable) {
			CreateEvent(IDeviceHost.PHONE_EVENT_TYPE.Hold, enable);
		}
		private void InCallRing(bool enable) {
			CreateEvent(IDeviceHost.PHONE_EVENT_TYPE.InCallRing, enable);
		}
		private void CreateEvent(IDeviceHost.PHONE_EVENT_TYPE type, bool enable) {
			CreateEvent(type, enable, false);
		}
		private void CreateEvent(IDeviceHost.PHONE_EVENT_TYPE type, bool enable, bool is_during_call) {
			List<PluginError> errors = null;

			lock (devices_lock) {
				foreach (DeviceData data in devices){
					if (!data.active)
						continue;
					try{
						if (type != IDeviceHost.PHONE_EVENT_TYPE.LineActive)
							data.host.CreateEvent(type, enable);
						else{
							if (data.HasLimit(IHeadsetDevice.HEADSET_LIMITATIONS.ONLY_ACTIVE_DURING_CALLS)){
								if (is_during_call)
									data.host.CreateEvent(type, enable);
							}
							else if (!is_during_call)
								data.host.CreateEvent(type, enable);
						}
					}
					catch (Exception e){
						if (errors == null)
							errors = new List<PluginError>();
						errors.Add(new PluginError { device = data, exception = e });

					}
				}
			}

			if (errors != null)
				foreach (PluginError err in errors)
					HandleError(err);
		}
		public void SetCallerID(String name, String number) {
			List<PluginError> errors = null;
			lock (devices_lock) {
				foreach (DeviceData data in devices) {

					if (data.active) {
						try {
							data.device.SetCallerId(name, number);
						}
						catch (Exception e) {

							if (errors == null)
								errors = new List<PluginError>();
							errors.Add(new PluginError { device = data, exception = e });

						}
					}
				}
			}
			if (errors != null)
				foreach (PluginError err in errors)
					HandleError(err);
		}
		private void DeviceStatusChanged(object sender, IHeadsetDevice.StatusEventArgs e) {
			IHeadsetDevice device = sender as IHeadsetDevice;
			DeviceData data = (from d in devices where d.device == device select d).SingleOrDefault();
			if (!data.active)
				return;
			if (!data.in_use && e.type != IHeadsetDevice.HEADSET_EVENT_TYPE.Talk)
				return;
			switch (e.type) {
				case IHeadsetDevice.HEADSET_EVENT_TYPE.Talk:
					broker.TalkPressed();
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.ToggleTalk:
					broker.TalkTogglePressed();
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.Hangup:
					broker.HangupPressed();
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.Mute:
					broker.Muted = true;
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.ToggleMute:
					broker.Muted = !broker.Muted;
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.UnMute:
					broker.Muted = false;
					break;
				case IHeadsetDevice.HEADSET_EVENT_TYPE.Redial:
					broker.RedialPressed();
					break;

				case IHeadsetDevice.HEADSET_EVENT_TYPE.Flash:
					broker.FlashPressed();
					break;
			}

		}
		private void DeviceAdded(object sender, IHeadsetPlugin.DeviceEventArgs e){

			IHeadsetPlugin plugin = sender as IHeadsetPlugin;
			DeviceData data = new DeviceData { device = e.device, host = new IDeviceHost(), active = false, in_use = false };
			data.plugin = (from p in plugins where p.plugin == plugin select p).Single();
			try {
				lock (devices_lock) {
					devices.Add(data);
				}
				data.device.StatusChanged += DeviceStatusChanged;
				data.limitations = data.device.Initalize(data.host);
				data.device.GetModel();
				if ((active_device_name == null && data.device.GetName() == last_active_device_name) || active_device_name == data.device.GetName()) {
					SetActiveDevice(data.device.GetName());
					PortAudio.refresh_devices();
				}
			}
			catch (Exception exp) {
				HandleError(new PluginError{device = data, exception = exp});
			}
		}
		
		private void DeviceRemoved(object sender, IHeadsetPlugin.DeviceEventArgs e) {
			DeviceData data = (from d in devices where d.device == e.device select d).SingleOrDefault();
			if (data == null)
				return;
			lock (devices_lock) {
				devices.Remove(data);
			}
			if (data.device.GetName() == active_device_name) {
				last_active_device_name = active_device_name;
				active_device_name = null;
			}
		}
		public void RegisterPlugin(IHeadsetPlugin plugin) {
			PluginData data = new PluginData() { plugin = plugin };
			plugins.Add(data);
			try{
				plugin.DeviceAdded += DeviceAdded;
				plugin.DeviceRemoved += DeviceRemoved;
				plugin.Initialize();
				data.error_tries_left = DEFAULT_PLUGIN_RETRIES;
			}
			catch (Exception e) {
				HandleError(new PluginError { plugin = data, exception = e });
			}
		}
		private bool IsTypeOf(Type to_check, Type of) {
			if (to_check == null)
				return false;
			if (to_check == of)
				return true;
			return IsTypeOf(to_check.BaseType, of);
		}



		private void BrokerDevicesReadyChanged(object sender, bool data) {
			LineActive(data, false);
		}
		private void BrokerAnsweredChanged(object sender, bool data) {
			LineActive(data, true);
		}
		private void BrokerMuteChanged(object sender, bool data) {
			Mute(data);
		}

		private void BrokerCallRingingChanged(object sender, bool data) {
			if (data)
				SetCallerID(Call.active_call.other_party_name, Call.active_call.other_party_number);
			Ring(data);
		}
		public HeadsetPluginManager() {
			broker = Broker.get_instance();
			broker.DevicesReadyChanged += BrokerDevicesReadyChanged;
			broker.call_answeredChanged += BrokerAnsweredChanged;
			broker.active_call_ringingChanged += BrokerCallRingingChanged;
			broker.MutedChanged += BrokerMuteChanged;
			string plugin_dir = Utils.plugins_dir();
			string[] dlls;
			try{
				dlls = Directory.GetFileSystemEntries(plugin_dir, "*.dll");
			}catch (DirectoryNotFoundException){
				return;
			}
			foreach (String dll in dlls) {
				try{
					Assembly asm = Assembly.LoadFrom(dll);
					if (asm == null)
						continue;
					foreach (Type type in asm.GetTypes()) {
						if (type.IsAbstract)
							continue;
						if (!IsTypeOf(type, typeof(IHeadsetPlugin)))
							continue;

						RegisterPlugin(asm.CreateInstance(type.FullName) as IHeadsetPlugin);
					}
				}
				catch (ReflectionTypeLoadException ex){
					String err = "Error creating headset plugin from dll \"" + dll + "\" due to a loader exception, make sure you have the headset runtime/api/sdk installed error was:\n";
					foreach (Exception e in ex.LoaderExceptions)
						err += e.Message + "\n";
					Utils.PluginLog("Headset Plugin Manager",err);
				}

				catch (Exception e){
					Utils.PluginLog("Headset Plugin Manager", "Error creating headset plugin from dll \"" + dll + "\" of: " + e.Message);
				}
			}
		}
		~HeadsetPluginManager() {
			Dispose();
		}
		private bool disposed;
		public void Dispose() {
			if (!disposed) {
				disposed = true;
				foreach (PluginData plugin_data in plugins) {
					try {
						plugin_data.plugin.Terminate();
					}
					catch (Exception e) { Utils.PluginLog("Headset Plugin Manager", "Error terminating a plugin: " + e.Message); }
				}
			}
			GC.SuppressFinalize(this);
		}

		private void HandleError(PluginError error) {
			if (error.plugin == null)
				error.plugin = error.device.plugin;
			if ((DateTime.Now - error.plugin.last_error_time).TotalSeconds > 60 * 5)  //if no errors for 5 minutes reset error count
				error.plugin.error_tries_left = DEFAULT_PLUGIN_RETRIES;

			error.plugin.last_error_time = DateTime.Now;
			String restart_msg = error.plugin.error_tries_left > 0 ? " will try to restart/init it " + error.plugin.error_tries_left + " more times" : " will not be restarting it";
			Utils.PluginLog("Headset Plugin Manager", "Plugin " + error.plugin.plugin.ProviderName() + " had an error Due to: " + error.exception.Message + "\n" + restart_msg);
			List<DeviceData> to_remove = new List<DeviceData>();
			lock (devices_lock) {
				foreach (DeviceData device in devices){
					if (device.plugin == error.plugin) {
						try{
							device.device.SetActive(false);
						}
						catch (Exception){
							Utils.PluginLog("Headset Plugin Manager", "While handling error wasn't able to deactivate device, not a major issue");
						}
						to_remove.Add(device);
					}
				}
			}
			lock (devices_lock) {
				foreach (DeviceData device in to_remove)
					devices.Remove(device);
			}
			error.plugin.plugin.Terminate();
			if (error.plugin.error_tries_left-- > 0)
				DelayedFunction.DelayedCall("IHeadsetPlugin_PluginStart_ " + error.plugin.plugin.ProviderName(), () => init_plugin(error.plugin), 1000);//give it a second
		}
		private void init_plugin(PluginData plugin){
			try{
				plugin.plugin.Initialize();
			}
			catch (Exception e) {
				HandleError(new PluginError { plugin = plugin, exception = e });
			}
		}
	}

	public abstract class IHeadsetPlugin {
		public class DeviceEventArgs : EventArgs {
			public readonly IHeadsetDevice device;
			public DeviceEventArgs(IHeadsetDevice device) {
				this.device = device;
			}
		}
		public EventHandler<DeviceEventArgs> DeviceAdded;
		public EventHandler<DeviceEventArgs> DeviceRemoved;
		public abstract string ProviderName();
		public abstract void Initialize();
		public abstract void Terminate();
	}
}
