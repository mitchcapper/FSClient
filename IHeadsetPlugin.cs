using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
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
			public IHeadsetPlugin plugin;
			public IHeadsetDevice.HEADSET_LIMITATIONS limitations;
			public bool HasLimit(IHeadsetDevice.HEADSET_LIMITATIONS limit) {
				return (limitations & limit) == limit;
			}
		}


		private Broker broker;
		private const int DEFAULT_PLUGIN_RETRIES = 5;
		private List<DeviceData> devices = new List<DeviceData>();

		private List<IHeadsetPlugin> plugins = new List<IHeadsetPlugin>();
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
			lock (devices) {
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
						HandleError(device, e, DEFAULT_PLUGIN_RETRIES);
					}
				}
			}
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
			foreach (DeviceData data in devices) {
				if (!data.active)
					continue;
				try {
					if (type != IDeviceHost.PHONE_EVENT_TYPE.LineActive)
						data.host.CreateEvent(type, enable);
					else {
						if (data.HasLimit(IHeadsetDevice.HEADSET_LIMITATIONS.ONLY_ACTIVE_DURING_CALLS)) {
							if (is_during_call)
								data.host.CreateEvent(type, enable);
						}
						else if (!is_during_call)
							data.host.CreateEvent(type, enable);
					}
				}
				catch (Exception e) {
					HandleError(data, e, DEFAULT_PLUGIN_RETRIES);
				}
			}
		}
		public void SetCallerID(String name, String number) {
			lock (devices) {
				foreach (DeviceData data in devices) {

					if (data.active) {
						try {
							data.device.SetCallerId(name, number);
						}
						catch (Exception e) {
							HandleError(data, e, DEFAULT_PLUGIN_RETRIES);
						}
					}
				}
			}
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
		private void DeviceAdded(object sender, IHeadsetPlugin.DeviceEventArgs e) {

			DeviceData data = new DeviceData { device = e.device, host = new IDeviceHost(), active = false, in_use = false, plugin = sender as IHeadsetPlugin };
			try {
				lock (devices) {
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
				HandleError(data, exp, DEFAULT_PLUGIN_RETRIES);
			}
		}
		private void DeviceRemoved(object sender, IHeadsetPlugin.DeviceEventArgs e) {
			DeviceData data = (from d in devices where d.device == e.device select d).SingleOrDefault();
			if (data == null)
				return;
			lock (devices) {
				devices.Remove(data);
			}
			if (data.device.GetName() == active_device_name) {
				last_active_device_name = active_device_name;
				active_device_name = null;
			}
		}
		public void RegisterPlugin(IHeadsetPlugin plugin) {
			try {
				plugins.Add(plugin);
				plugin.DeviceAdded += DeviceAdded;
				plugin.DeviceRemoved += DeviceRemoved;
				plugin.Initialize();
			}
			catch (Exception e) {
				HandleError(plugin, e, 0); //no retries if you error during init
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
				foreach (IHeadsetPlugin plugin in plugins) {
					try {
						plugin.Terminate();
					}
					catch (Exception e) { Utils.PluginLog("Headset Plugin Manager", "Error terminating a plugin: " + e.Message); }
				}
			}
			GC.SuppressFinalize(this);
		}
		private void HandleError(DeviceData device, Exception e, int trys_left) {
			HandleError(device.plugin, e, trys_left);
		}
		private void HandleError(IHeadsetPlugin plugin, Exception e, int trys_left){
			String restart_msg = trys_left > 0 ? " will try to restart/init it " + trys_left + " more times" : "";
			Utils.PluginLog("Headset Plugin Manager", "Plugin " + plugin.ProviderName() + " had an error Due to: " + e.Message + "\n" + restart_msg);
			List<DeviceData> to_remove = new List<DeviceData>();
			lock (devices){
				foreach (DeviceData device in devices){
					if (device.plugin == plugin){
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
			lock (devices){
				foreach (DeviceData device in to_remove)
					devices.Remove(device);
			}
			plugin.Terminate();
			if (trys_left-- > 0)
				DelayedFunction.DelayedCall("IHeadsetPlugin_PluginStart_ " + plugin.ProviderName(), () => init_plugin(plugin, trys_left), 1000);//give it a second
		}
		private void init_plugin(IHeadsetPlugin plugin, int trys_left){
			try{
			plugin.Initialize();
			}
			catch (Exception e) {
				HandleError(plugin, e, trys_left);
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
