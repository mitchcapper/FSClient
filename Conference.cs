using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using FreeSWITCH.Native;

namespace FSClient {
	[XmlRoot("settingsConference")]
	public class SettingsConference {
		public SettingsField[] fields { get; set; }
		public Conference GetConference() {
			var socket = Conference.instance;
			foreach (SettingsField field in fields) {
				FieldValue val = FieldValue.GetByName(socket.values, field.name);
				if (val != null)
					val.value = field.value;
			}
			return socket;
		}
		public SettingsConference() {
		}
		public SettingsConference(Conference socket) {
			fields = (from fv in socket.values select new SettingsField(fv)).ToArray();
		}
	}

	public class ConferenceUser : ObservableClass {

		[Flags]
		public enum USER_STATE {
			TALK = 1, FLOOR = 2, MUTE = 4, DEAF = 8
		};

		public static bool StateTest(USER_STATE state, USER_STATE test) {
			return ((state & test) != 0);
		}
		public bool StateIs(USER_STATE test) {
			return StateTest(this.state, test);
		}
		public string id {
			get { return _id; }
			set {
				if (_id == value)
					return;
				_id = value;
				RaisePropertyChanged("id");
			}
		}
		private string _id;

		public int min_energy_level {
			get { return _min_energy_level; }
			private set {
				if (_min_energy_level == value)
					return;
				_min_energy_level = value;
				RaisePropertyChanged("min_energy_level");
			}
		}
		private int _min_energy_level;

		public bool is_us {
			get { return _is_us; }
			set {
				if (_is_us == value)
					return;
				_is_us = value;
				RaisePropertyChanged("is_us");
			}
		}
		private bool _is_us;


		public string uuid {
			get { return _uuid; }
			set {
				if (_uuid == value)
					return;
				_uuid = value;
				RaisePropertyChanged("uuid");
			}
		}
		private string _uuid;

		public USER_STATE state {
			get { return _state; }
			set {
				if (_state == value)
					return;
				_state = value;
				RaisePropertyChanged("state");
			}
		}
		private USER_STATE _state;

		public string party_name {
			get { return _party_name; }
			set {
				if (_party_name == value)
					return;
				_party_name = value;
				RaisePropertyChanged("party_name");
			}
		}
		private string _party_name;

		public string party_number {
			get { return _party_number; }
			set {
				if (_party_number == value)
					return;
				_party_number = value;
				RaisePropertyChanged("party_number");
			}
		}
		private string _party_number;

		public DateTime start_time {
			get { return _start_time; }
			set {
				if (_start_time == value)
					return;
				_start_time = value;
				RaisePropertyChanged("start_time");
			}
		}
		private DateTime _start_time;

		public TimeSpan duration {
			get { return _duration; }
			set {
				if (_duration == value)
					return;
				_duration = value;
				RaisePropertyChanged("duration");
			}
		}
		private TimeSpan _duration;

		public int their_volume {
			get { return _their_volume; }
			set {
				if (_their_volume == value)
					return;
				_their_volume = value;
				RaisePropertyChanged("their_volume");
			}
		}
		private int _their_volume;

		public int conference_volume {
			get { return _conference_volume; }
			set {
				if (_conference_volume == value)
					return;
				_conference_volume = value;
				RaisePropertyChanged("conference_volume");
			}
		}
		private int _conference_volume;

		public ConferenceUser() {
			state = 0;
			start_time = DateTime.Now;
			min_energy_level = 300;
		}
		public void SetEnergyLevel(int level) {
			Conference.ConferenceAction("energy " + id + " " + level);
			min_energy_level = level;
		}
		public void SetAudioLevel(int level, bool conference_level = false) {
			if (level > 4 || level < -4)
				return;
			String direction = conference_level ? "volume_out" : "volume_in";
			Conference.ConferenceAction(direction + " " + id + " " + level);
			if (conference_level)
				conference_volume = level;
			else
				their_volume = level;
		}
		public void Mute(bool unmute = false) {
			String add = unmute ? "un" : "";
			Conference.ConferenceAction(add + "mute " + id);
			if (unmute)
				state ^= USER_STATE.MUTE;
			else
				state |= USER_STATE.MUTE;
		}
		public void Deaf(bool undeaf = false) {
			String add = undeaf ? "un" : "";
			Conference.ConferenceAction(add + "deaf " + id);
			if (undeaf)
				state ^= USER_STATE.DEAF;
			else
				state |= USER_STATE.DEAF;
		}
		public void Drop() {
			Utils.bgapi_exec("uuid_kill", uuid + " NORMAL_CLEARING");
		}
		public void Split() {
			Utils.bgapi_exec("uuid_transfer", uuid + " auto_answer xml public");
		}

	}

	public class Conference : ObservableClass {
		public static Field[] fields = {
										   new Field(Field.FIELD_TYPE.String, "Enter Sound","enter-sound","enter-sound","tone_stream://%(200,0,500,600,700)",""),
										   new Field(Field.FIELD_TYPE.String, "Exit Sound","exit-sound","exit-sound","tone_stream://%(500,0,300,200,100,50,25)",""),
										new Field(Field.FIELD_TYPE.Int,"Min Energy Level","energy-level","energy-level","300",""),
										new Field(Field.FIELD_TYPE.Bool,"Comfort Noise","comfort-noise","comfort-noise","true",""),
										new Field(Field.FIELD_TYPE.Combo,"Audio Sample Rate","rate","rate","16000","",new Field.FieldOption{display_value="8000", value="8000"},new Field.FieldOption{display_value="12000", value="12000"},new Field.FieldOption{display_value="16000", value="16000"},new Field.FieldOption{display_value="24000", value="24000"},new Field.FieldOption{display_value="32000", value="32000"},new Field.FieldOption{display_value="44100", value="44100"},new Field.FieldOption{display_value="48000", value="48000"},new Field.FieldOption{display_value="First Member Rate(auto)", value="auto"}),
										new Field(Field.FIELD_TYPE.Combo,"Audio Frame Interval","interval","interval","20","",new Field.FieldOption{display_value="10", value="10"},new Field.FieldOption{display_value="20", value="20"},new Field.FieldOption{display_value="30", value="30"},new Field.FieldOption{display_value="40", value="40"},new Field.FieldOption{display_value="50", value="50"},new Field.FieldOption{display_value="60", value="60"},new Field.FieldOption{display_value="70", value="70"},new Field.FieldOption{display_value="80", value="80"},new Field.FieldOption{display_value="90", value="90"},new Field.FieldOption{display_value="110", value="110"},new Field.FieldOption{display_value="120", value="120"},new Field.FieldOption{display_value="First Member Rate(auto)", value="auto"}),
										new Field(Field.FIELD_TYPE.MultiItemSort,"Default Member Flags","member-flags","member-flags","dist-dtmf","","dist-dtmf","mute","deaf","mute-detect","moderator","nomoh","endconf","mintwo","ghost"),

	};
		private static string[] AllowedEmptyFields = new[] { "enter-sound", "exit-sound" };
		public FieldValue[] values = FieldValue.FieldValues(fields);
		public void gen_config(XmlNode config_node) {
			var controls = XmlUtils.AddNodeNode(config_node, "caller-controls");
			var control_group = XmlUtils.AddNodeNode(controls, "group");
			XmlUtils.AddNodeAttrib(control_group, "name", "default");
			var profiles = XmlUtils.AddNodeNode(config_node, "profiles");
			var profile = XmlUtils.AddNodeNode(profiles, "profile");
			XmlUtils.AddNodeAttrib(profile, "name", "default");
			foreach (FieldValue value in values) {
				if (String.IsNullOrEmpty(value.field.xml_name))
					continue;
				if (String.IsNullOrWhiteSpace(value.value) && !AllowedEmptyFields.Contains(value.field.name))
					continue;
				Utils.add_xml_param(profile, value.field.xml_name, value.value);
			}
			//File.WriteAllText(@"c:\temp\conf.xml",config_node.OuterXml);
		}
		public void reload_config() {
			Utils.bgapi_exec("reload", "mod_conference");

		}
		public void edit() {
			GenericEditor editor = new GenericEditor();
			editor.Init("Editing Conference Settings", values);
			editor.ShowDialog();
			if (editor.DialogResult == true) {
				MessageBoxResult mres = MessageBox.Show("Do you want to reload the conference settings now, if there is an active conference this will do nothing?", "Reload Module Warning", MessageBoxButton.YesNo);
				if (mres != MessageBoxResult.Yes)
					return;
				reload_config();
			}
		}

		private System.Threading.Timer duration_timer;


		public Call our_conference_call {
			get { return _our_conference_call; }
			set {
				if (_our_conference_call == value)
					return;
				_our_conference_call = value;
				_our_conference_call.PropertyChanged += _our_conference_call_PropertyChanged;
				RaisePropertyChanged("our_conference_call");
			}
		}

		void _our_conference_call_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
			if (!our_conference_call.call_ended && our_conference_call.state == Call.CALL_STATE.Answered)
				conf_color = "#FF4EFF00";
			else
				conf_color = "#FFF1FF00";

		}
		private Call _our_conference_call;


		public void Record(String file, bool stop = false) {
			String add = stop ? "no" : "";
			ConferenceAction(add + "record '" + file + "'");
		}
		public static void ConferenceAction(String action) {
			Utils.bgapi_exec("conference", "fsc_conference " + action);
		}
		public ContextMenu menu;
		private Conference() {
			duration_timer = new System.Threading.Timer(DurationTimerFired, null, 1000, 1000);
			menu = new ContextMenu();
			menu.Opened += conference_ContextMenuOpened;
			users.CollectionChanged += users_CollectionChanged;
			conf_visible = Visibility.Hidden;
			conf_color = "#FF4EFF00";
		}

		void users_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
			conf_visible = users.Count > 0 ? Visibility.Visible : Visibility.Hidden;
			if (am_recording_file != null && users.Count == 0)
				am_recording_file = null;
			DelayedFunction.DelayedCall("ConferenceOnlyUsLeft", only_us_left_check, 2000);
		}

		private void only_us_left_check() {
			if (users.Count != 1)
				return;
			var first_user = users.FirstOrDefault();
			if (first_user != null && our_conference_call != null && (our_conference_call.leg_a_uuid == first_user.uuid || our_conference_call.leg_b_uuid == first_user.uuid))
				our_conference_call.hangup("Last in conference");
		}

		private Broker _broker;
		private Broker broker {
			get { return _broker ?? (_broker = Broker.get_instance()); }
		}

		private void DurationTimerFired(object obj) {
			if (Application.Current != null)
				Application.Current.Dispatcher.BeginInvoke((Action)(UpdateDurations));
		}
		private void UpdateDurations() {
			foreach (ConferenceUser user in users) {
				user.duration = DateTime.Now - user.start_time;
			}
		}

		private static Conference _instance;
		public static Conference instance { get { return _instance ?? (_instance = new Conference()); } }
		public static ObservableCollection<ConferenceUser> _users = new ObservableCollection<ConferenceUser>();
		public static ObservableCollection<ConferenceUser> users { get { return _users; } }
		private MenuItem CreateMenuItem(String header, Action action) {
			MenuItem item = new MenuItem { Header = header };
			item.Click += (o, args) => action();
			return item;
		}

		private MenuItem UserMenu(ConferenceUser user) {
			MenuItem main = new MenuItem();
			MenuItem item;
			main.Header = user.party_name + " " + ConfStateConverter.StateConvert(user.state);
			item = new MenuItem() { Header = "Min Energy Level" };
			for (int x = 0; x <= 1500; x += 150) {
				int val = x;
				String add = val == user.min_energy_level ? "*" : "";
				item.Items.Add(CreateMenuItem("Level " + val + add, () => user.SetEnergyLevel(val)));
			}
			main.Items.Add(item);

			item = new MenuItem() { Header = "Their Volume Level" };
			for (int x = -4; x <= 4; x++) {
				int val = x;
				String add = val == user.their_volume ? "*" : "";
				item.Items.Add(CreateMenuItem("Level " + x + add, () => user.SetAudioLevel(val)));
			}
			main.Items.Add(item);

			item = new MenuItem() { Header = "Conference Volume Level" };
			for (int x = -4; x <= 4; x++) {
				int val = x;
				String add = val == user.conference_volume ? "*" : "";
				item.Items.Add(CreateMenuItem("Level " + x + add, () => user.SetAudioLevel(val, true)));
			}
			main.Items.Add(item);
			if (user.StateIs(ConferenceUser.USER_STATE.MUTE))
				main.Items.Add(CreateMenuItem("UnMute", () => user.Mute(true)));
			else
				main.Items.Add(CreateMenuItem("Mute", () => user.Mute()));

			if (user.StateIs(ConferenceUser.USER_STATE.DEAF))
				main.Items.Add(CreateMenuItem("UnDeaf", () => user.Deaf(true)));
			else
				main.Items.Add(CreateMenuItem("Deaf", () => user.Deaf()));
			if (!user.is_us)
				main.Items.Add(CreateMenuItem("Split Out", user.Split));
			main.Items.Add(CreateMenuItem("Drop From Conference", user.Drop));
			return main;
		}
		private void conference_ContextMenuOpened(object sender, RoutedEventArgs e) {
			ContextMenu menu = sender as ContextMenu;
			menu.Items.Clear();
			if (am_recording_file != null)
				menu.Items.Add(CreateMenuItem("Stop Recording Conference", StopRecord));
			else if (broker.recordings_folder != null)
				menu.Items.Add(CreateMenuItem("Record Conference", RecordConference));
			menu.Items.Add(CreateMenuItem("End Conference", EndConference));
			menu.Items.Add(new MenuItem { Header = "----------Users----------" });
			foreach (var user in users)
				menu.Items.Add(UserMenu(user));
		}

		public void EndConference() {
			ConferenceAction("kick all");
		}

		private void StopRecord() {
			Record(am_recording_file, true);
			am_recording_file = null;
		}

		private string am_recording_file;
		private void RecordConference() {
			String full_path = broker.recordings_folder.Replace('/', '\\');
			if (!full_path.Contains(":\\"))
				full_path = Path.Combine(Directory.GetCurrentDirectory(), full_path); //freeswitch is not happy with relative paths when using quotes
			full_path = Path.Combine(full_path, "rec_conference_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
			int num = 1;
			String orig_full_path = full_path;
			while (File.Exists(full_path + ".wav"))
				full_path = orig_full_path + "." + num++;

			full_path = full_path.Replace('\\', '/'); //seems freeswitch will selectively escape what it cant otherwise
			am_recording_file = full_path + ".wav";
			Record(am_recording_file);
		}

		public string conf_color {
			get { return _conf_color; }
			set {
				if (_conf_color == value)
					return;
				_conf_color = value;
				RaisePropertyChanged("conf_color");
			}
		}
		private string _conf_color;

		public Visibility conf_visible {
			get { return _conf_visible; }
			set {
				if (_conf_visible == value)
					return;
				_conf_visible = value;
				RaisePropertyChanged("conf_visible");
			}
		}
		private Visibility _conf_visible;


		private ConferenceUser GetUser(FSEvent evt) {
			String member_id = evt.get_header("Member-ID");
			ConferenceUser user = (from u in users where u.id == member_id select u).FirstOrDefault();
			return user;
		}
		public void NewFSEvent(object sender, FSEvent evt) {
			if (evt.event_id != switch_event_types_t.SWITCH_EVENT_CUSTOM || evt.subclass_name != "conference::maintenance" || evt.get_header("Conference-Name") != "fsc_conference")
				return;
			String type = evt.get_header("Action");
			ConferenceUser user;
			switch (type) {
				case "add-member":
					String member_id = evt.get_header("Member-ID");
					String uuid = evt.get_header("Unique-ID");
					String source = evt.get_header("Caller-Source");
					String direction = evt.get_header("Call-Direction");
					user = new ConferenceUser { id = member_id, uuid = uuid, party_name = evt.get_header("Caller-Caller-ID-Name"), party_number = evt.get_header("Caller-Caller-ID-Number") };
					if (source == "mod_portaudio" && direction == "inbound") {
						user.party_number = user.party_name = "You";
						user.is_us = true;
					}
					users.Add(user);
					break;
				case "del-member":
					user = GetUser(evt);
					if (user == null)
						return;
					users.Remove(user);
					break;
				case "start-talking":
					user = GetUser(evt);
					if (user == null)
						return;
					user.state |= ConferenceUser.USER_STATE.TALK;
					break;
				case "stop-talking":
					user = GetUser(evt);
					if (user == null)
						return;
					user.state ^= ConferenceUser.USER_STATE.TALK;
					break;
				case "floor-change":
					user = GetUser(evt);
					if (user == null)
						return;
					foreach (var u in users)
						u.state ^= ConferenceUser.USER_STATE.FLOOR;
					user.state |= ConferenceUser.USER_STATE.FLOOR;
					break;

			}
		}

		public void join_conference() {
			if (our_conference_call != null && our_conference_call.call_ended == false) {
				if (our_conference_call.state != Call.CALL_STATE.Answered)
					our_conference_call.switch_to();
			} else
				broker.DialString("fsc_conference");
		}
	}
}
