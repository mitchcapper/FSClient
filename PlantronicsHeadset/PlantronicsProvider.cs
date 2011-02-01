using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FSClient;
using PL = Plantronics.Device.Common;
namespace PlantronicsHeadset {

	public class PlantronicsHeadset : IHeadsetDevice {
		private PL.IDevice device;

		public string device_path { get { return device.DevicePath; } }
		public override string GetName() {
			return "Plantronics " + device.ProductName;
		}
		public override string GetManufacturer() {
			return "Plantronics";
		}

		public override string GetModel() {
			int pos = device.ProductName.IndexOf('/');
			if (pos != -1)
				return device.ProductName.Substring(0, pos);
			return device.ProductName;
		}
		public override string GetUniqueId() {
			return "Plantronics" + device_path;
		}
		public override void SetActive(bool active) {
			if (active && !device.IsAttached) {
				device.Attach();
				device.HostCommand.Ring(false);
				device.HostCommand.AudioState = PL.AudioType.MonoOff;
			}
			else if (!active && device.IsAttached)
				device.Detach();
		}
		public override void SetCallerId(string name, string number) {

		}
		public PlantronicsHeadset(PL.IDevice device) {
			this.device = device;
		}
		private IDeviceHost host;
		private bool ignore_next_radio_on;
		private void HostStatusChanged(object sender, IDeviceHost.StatusEventArgs e) {

			switch (e.type) {
				case IDeviceHost.PHONE_EVENT_TYPE.InCallRing:
				case IDeviceHost.PHONE_EVENT_TYPE.Ring:
					device.HostCommand.Ring(e.enable);
					break;
				case IDeviceHost.PHONE_EVENT_TYPE.LineActive:
					ignore_next_radio_on = e.enable;
					device.HostCommand.AudioState = e.enable ? PL.AudioType.MonoOn : PL.AudioType.MonoOff;
					break;
			}

		}
		public override HEADSET_LIMITATIONS Initalize(IDeviceHost host) {
			this.host = host;

			device.DeviceEvents.MuteStateChanged += DeviceEvents_MuteStateChanged;
			device.DeviceEvents.FlashPressed += DeviceEvents_FlashPressed;
			device.DeviceEvents.TalkPressed += DeviceEvents_TalkPressed;
			device.DeviceEvents.AudioStateChanged += DeviceEvents_AudioStateChanged;
			device.DeviceEvents.ButtonPressed += DeviceEvents_ButtonPressed;
			host.StatusChanged += HostStatusChanged;
			return HEADSET_LIMITATIONS.NO_UNMUTE;
		}


		void DeviceEvents_ButtonPressed(object sender, PL.DeviceEventArgs e) {
			Debug.WriteLine("Got a raw event of: " + e.ButtonPressed);
		}

		void DeviceEvents_AudioStateChanged(object sender, PL.DeviceEventArgs e) {
			if (e.AudioState == PL.AudioType.MonoOff)
				StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.RadioClosed));
			else if (e.AudioState == PL.AudioType.MonoOn || e.AudioState == PL.AudioType.MonoOnWait) {
				StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.RadioOpen));
				if (ignore_next_radio_on)
					ignore_next_radio_on = false;
				else {
					StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.ToggleTalk));
				}
			}
		}

		void DeviceEvents_TalkPressed(object sender, PL.DeviceEventArgs e) {
			StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.ToggleTalk));
		}

		void DeviceEvents_FlashPressed(object sender, PL.DeviceEventArgs e) {
			StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Flash));
		}

		void DeviceEvents_MuteStateChanged(object sender, PL.DeviceEventArgs e) {
			if (e.Mute)
				StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.Mute));
			else
				StatusChanged(this, new StatusEventArgs(HEADSET_EVENT_TYPE.UnMute));
		}

	}

	public class PlantronicsProvider : IHeadsetPlugin {
		private PL.DeviceManager device_manager;
		private void AddNewDevice(PL.IDevice pl_device) {
			PlantronicsHeadset device = new PlantronicsHeadset(pl_device);
			devices.Add(device);
			DeviceAdded(this, new DeviceEventArgs(device));
		}
		public override void Initialize() {
			device_manager = new PL.DeviceManager();
			device_manager.DeviceStateChanged += device_manager_DeviceStateChanged;

			foreach (PL.IDevice device in device_manager.Devices)
				AddNewDevice(device);
		}
		public override string ProviderName() {
			return "Plantronics Provider";
		}
		private List<PlantronicsHeadset> devices = new List<PlantronicsHeadset>();
		void device_manager_DeviceStateChanged(object sender, PL.DeviceStateEventArgs e) {
			PlantronicsHeadset headset = (from d in devices where d.device_path == e.DevicePath select d).SingleOrDefault();
			if (e.State == PL.DeviceState.Removed && headset != null)
				DeviceRemoved(this, new DeviceEventArgs(headset));
			else if (e.State == PL.DeviceState.Added) {

				AddNewDevice(device_manager.FindDeviceForPath(e.DevicePath));
			}
		}
		public override void Terminate() {

		}

	}
}
