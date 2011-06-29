using System;
using FreeSWITCH.Native;



namespace FSClient {
	public class FSEvent : EventArgs {
		private switch_event evt;
		public FSEvent(switch_event evt) {
			this.evt = evt;
		}
		public string get_header(string name) {
			//return freeswitch.switch_event_get_header(evt, name); //removed as for some reason its not swigging right for now...
			for (var x = evt.headers; x != null; x = x.next){
				if (name.ToLower() == x.name.ToLower())
					return x.value;
			}
			return null;
		}
		public string body { get { return evt.body; } }
		public int flags { get { return evt.flags; } }
		public uint key { get { return evt.key; } }
		public string owner { get { return evt.owner; } }
		public string subclass_name { get { return evt.subclass_name; } }
		public switch_event_types_t event_id { get { return evt.event_id; } }
		public switch_event_header first_header { get { return evt.headers; } }
	}
}
