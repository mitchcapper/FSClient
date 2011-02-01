using System.Collections.Generic;
using System.Linq;
using System.Xml;
namespace FSClient {
	public class PortAudio {
		private static int cur_guid = 1000;
		private class InternalAudioDevice {
			public AudioDevice device;
			public int id;
			public bool is_alive;
		}
		private static List<InternalAudioDevice> _devices = new List<InternalAudioDevice>();

		private static AudioDevice[] _pub_devices;

		public class AudioDevice {
			public readonly string name;
			public readonly int inputs;
			public readonly int outputs;
			public readonly int guid;
			public AudioDevice(int guid, string name, int inputs, int outputs) {
				this.name = name;
				this.guid = guid;
				this.inputs = inputs;
				this.outputs = outputs;
			}
			public override string ToString() {
				return name;
			}
			public void SetInDev() {
				PortAudio.SetInDev(guid);
			}
			public void SetOutDev() {
				PortAudio.SetOutDev(guid);
			}
			public void SetRingDev() {
				PortAudio.SetRingDev(guid);
			}
		}

		public static void Hangup(int call_id) {
			Utils.bgapi_exec("pa", "hangup " + call_id);
		}
		public static void Call(string dest) {
			Utils.bgapi_exec("pa", "call " + dest);
		}
		public static void HoldAll() {
			Utils.bgapi_exec("pa", "switch none");
		}
		public static void SwitchTo(int call_id) {
			Utils.bgapi_exec("pa", "switch " + call_id);
		}
		private static int guid_to_id(int guid) {
			return (from I in _devices where I.device.guid == guid select I.id).FirstOrDefault();
		}
		private static void SetInDev(int guid) {
			Utils.bgapi_exec("pa", "indev #" + guid_to_id(guid));
		}
		private static void SetOutDev(int guid) {
			Utils.bgapi_exec("pa", "outdev #" + guid_to_id(guid));
		}
		public static void ClearRingDev() {
			Utils.bgapi_exec("pa", "ringdev #" + -1);
		}
		private static void SetRingDev(int guid) {
			Utils.bgapi_exec("pa", "ringdev #" + guid_to_id(guid));
		}
		public static void SetInAndOutDev(AudioDevice indev, AudioDevice outdev) {
			if (indev != null && outdev != null)
				SetInAndOutDev(indev.guid, outdev.guid);
			else if (indev != null)
				indev.SetInDev();
			else if (outdev != null)
				outdev.SetOutDev();
		}
		public static void PrepareStream(AudioDevice indev, AudioDevice outdev) {
			Utils.bgapi_exec("pa", "preparestream #" + guid_to_id(indev.guid) + " #" + guid_to_id(outdev.guid));
		}
		private static void SetInAndOutDev(int indev_guid, int outdev_guid) {
			Utils.bgapi_exec("pa", "switchstream #" + guid_to_id(indev_guid) + " #" + guid_to_id(outdev_guid));
		}
		public static void PlayDTMF(char dtmf, string uuid, bool no_close=false) {
			if (uuid == null)
				Utils.bgapi_exec("pa", "play tone_stream://d=150;v=-5;" + dtmf + (no_close ? " -1 no_close" : ""));
			else
				PlayInUUID(uuid, "tone_stream://d=150;v=-5;" + dtmf);
		}

		public static void CloseStreams(){
			Utils.bgapi_exec("pa", "closestreams");
		}

		public static void PlayInUUID(string uuid, string to_play) {
			Utils.bgapi_exec("uuid_displace", uuid + " start " + to_play + " 0 mux");
		}
		public static void SendDTMF(string dtmf) {
			char[] chars = dtmf.ToCharArray();
			foreach (char c in chars)
				Utils.bgapi_exec("pa", "dtmf " + c);
		}
		public static void set_mute(bool muted) {
			if (muted)
				Utils.bgapi_exec("pa", "flags off mouth");
			else
				Utils.bgapi_exec("pa", "flags on mouth");
		}
		public static AudioDevice[] get_devices(bool refresh) {
			if (refresh || _pub_devices == null)
				refresh_devices();
			return _pub_devices;
		}

		public static void refresh_devices() {
			foreach (InternalAudioDevice device in _devices)
				device.is_alive = false;
			Utils.api_exec("pa", "rescan");
			XmlDocument doc = XmlUtils.GetDocument(Utils.api_exec("pa", "devlist xml"));
			XmlNode node = XmlUtils.GetNode(doc, "devices", 0);
			foreach (XmlNode child in node.ChildNodes) {
				AudioDevice dev = new AudioDevice(
									cur_guid, XmlUtils.GetNodeAttrib(child, "name"),
									int.Parse(XmlUtils.GetNodeAttrib(child, "inputs")),
									int.Parse(XmlUtils.GetNodeAttrib(child, "outputs"))
									);
				int dev_id = int.Parse(XmlUtils.GetNodeAttrib(child, "id"));
				bool found_device = false;
				foreach (InternalAudioDevice device in _devices)//TODO: Probably should sort here
				{
					if (device.device.name == dev.name && device.is_alive == false) {
						device.is_alive = true;
						device.id = dev_id;
						found_device = true;
						break;
					}
				}
				if (!found_device) {
					InternalAudioDevice new_device = new InternalAudioDevice { device = dev, is_alive = true, id = dev_id };
					cur_guid++;
					_devices.Add(new_device);
				}
			}
			_pub_devices = (from c in _devices where c.is_alive select c.device).ToArray();
		}

		internal static void Answer(int call_id) {
			Utils.bgapi_exec("pa", "answer " + call_id);
		}
	}
}
