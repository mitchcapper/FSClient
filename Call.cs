using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using FreeSWITCH.Native;
namespace FSClient {
	public class Call : ObservableClass {
		private class Channel {
			public string gateway_id { get; set; }
		}
		public enum CALL_STATE { None, Answered, Ringing, Ended, Busy, Failed, Missed, Hold, Hold_Ringing };//don't want ringing first or state change won't fire

		#region properties

		public string sort_order {
			get {
				string ret = call_ended ? "0" : "1";
				switch (state) {
					case CALL_STATE.Answered:
						ret += "6";
						break;
					case CALL_STATE.Ringing:
						ret += "8";
						break;
					case CALL_STATE.Hold_Ringing:
					case CALL_STATE.Hold:
						ret += "4";
						break;
					default:
						ret += "0";
						break;
				}


				return ret + start_time.ToFileTime().ToString("0000000000000000000000000");
			}
		}

		public TimeSpan duration {
			get { return _duration; }
			set {
				if (value == _duration)
					return;
				_duration = value;
				RaisePropertyChanged("duration");
			}
		}
		private TimeSpan _duration;

		public string leg_a_uuid {
			get { return _leg_a_uuid; }
			set {
				if (value == _leg_a_uuid)
					return;
				_leg_a_uuid = value;
				RaisePropertyChanged("leg_a_uuid");
			}
		}
		private string _leg_a_uuid;

		public string leg_b_uuid {
			get { return _leg_b_uuid; }
			set {
				if (value == _leg_b_uuid)
					return;
				_leg_b_uuid = value;
				RaisePropertyChanged("leg_b_uuid");
			}
		}
		private string _leg_b_uuid;

		public int portaudio_id {
			get { return _portaudio_id; }
			set {
				if (value == _portaudio_id)
					return;
				_portaudio_id = value;
				RaisePropertyChanged("portaudio_id");
			}
		}
		private int _portaudio_id;

		public string other_party_name {
			get { return _other_party_name; }
			set {
				if (value == _other_party_name)
					return;
				_other_party_name = value;
				RaisePropertyChanged("other_party_name");
			}
		}
		private string _other_party_name;

		public string other_party_number {
			get { return _other_party_number; }
			set {
				if (value == _other_party_number)
					return;
				_other_party_number = value;
				RaisePropertyChanged("other_party_number");
			}
		}
		private string _other_party_number;

		public string note {
			get { return _note; }
			set {
				if (value == _note)
					return;
				_note = value;
				RaisePropertyChanged("note");
			}
		}
		private string _note;


		public bool is_outgoing {
			get { return _is_outgoing; }
			set {
				if (value == _is_outgoing)
					return;
				_is_outgoing = value;
				RaisePropertyChanged("is_outgoing");
			}
		}
		private bool _is_outgoing;


		public CALL_STATE state {
			get { return _state; }
		}
		private CALL_STATE _state;

		public bool call_ended { get; set; }

		public DateTime start_time {
			get { return _start_time; }
			set {
				if (value == _start_time)
					return;
				_start_time = value;
				RaisePropertyChanged("start_time");
			}
		}
		private DateTime _start_time;

		public DateTime end_time {
			get { return _end_time; }
			set {
				if (value == _end_time)
					return;
				_end_time = value;
				RaisePropertyChanged("end_time");
			}
		}
		private DateTime _end_time;

		public Account account {
			get { return _account; }
			set {
				if (value == _account)
					return;
				_account = value;
				RaisePropertyChanged("account");
			}
		}
		private Account _account;

		public string dtmfs {
			get { return _dtmfs; }
			set {
				if (value == _dtmfs)
					return;
				_dtmfs = value;
				RaisePropertyChanged("dtmfs");
			}
		}
		private string _dtmfs;

		#endregion

		private static Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
		public static ObservableCollection<Call> calls = new ObservableCollection<Call>();
		public static Call active_call { get; private set; }

		public static int live_call_count() {
			return (from c in calls where c.call_ended == false select c).Count();
		}


		#region events
		public class ActiveCallChangedArgs : EventArgs {
			public Call old_active_call;
			public Call new_active_call;
		}
		public class CallPropertyEventArgs : EventArgs {
			public Call call;
			public string property_name;
			public CallPropertyEventArgs(Call call, string property_name) {
				this.call = call;
				this.property_name = property_name;
			}
		}
		public class CallRightClickEventArgs : EventArgs {
			public Call call;
			public ContextMenu menu;
		}
		public static EventHandler<ActiveCallChangedArgs> ActiveCallChanged;
		public static EventHandler<CallPropertyEventArgs> CallPropertyChanged;
		public static EventHandler<CallPropertyEventArgs> CallStateChanged;
		public static EventHandler<CallRightClickEventArgs> CallRightClickMenuShowing;

		#endregion



		#region event_listeners

		private void call_ContextMenuOpened(object sender, RoutedEventArgs e) {
			ContextMenu menu = sender as ContextMenu;
			menu.Items.Clear();

			MenuItem item;
			if (call_ended) {
				menu.Items.Add(CreateMenuItem("Remove Call From History", RemoveCallFromHistory));
				menu.Items.Add(CreateMenuItem("Call on Account " + account.name, create_outgoing_call));
			}
			else {
				if (am_recording_file != null)
					menu.Items.Add(CreateMenuItem("Stop Recording Call", StopRecordCall));
				else if (broker.recordings_folder != null)
					menu.Items.Add(CreateMenuItem("Record Call", RecordCall));
				menu.Items.Add(CreateMenuItem("Transfer", TransferPrompt));
				item = new MenuItem() { Header = "Their Volume" };
				for (int x = -4; x <= 4; x++) {
					int val = x;
					item.Items.Add(CreateMenuItem("Level " + x, () => SetOtherPartyAudioLevel(val)));
				}
				menu.Items.Add(item);
				item = new MenuItem() { Header = "Your Volume" };
				for (int x = -4; x <= 4; x++) {
					int val = x;
					item.Items.Add(CreateMenuItem("Level " + x, () => SetOurAudioLevel(val)));
				}
				menu.Items.Add(item);
				menu.Items.Add(CreateMenuItem("End Call", hangup));
			}
			if (!String.IsNullOrWhiteSpace(dtmfs))
				menu.Items.Add(CreateMenuItem("Remove Call DTMF Presses", () => { dtmfs = ""; }));			

			if (CallRightClickMenuShowing != null) {
				CallRightClickMenuShowing(this, new CallRightClickEventArgs { call = this, menu = menu });
			}
		}

		private void StopRecordCall(){
			Utils.bgapi_exec("uuid_record", leg_b_uuid + " stop '" + am_recording_file + "'");
			am_recording_file = null;
		}

		private string am_recording_file;
		private void RecordCall(){
			String full_path = broker.recordings_folder.Replace('/', '\\');
			if (! full_path.Contains(":\\"))
				full_path = Path.Combine(Directory.GetCurrentDirectory(),full_path); //freeswitch is not happy with relative paths when using quotes
			full_path = Path.Combine(full_path,"rec_" + other_party_number + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
			int num =1;
			String orig_full_path = full_path;
			while (File.Exists(full_path + ".wav"))
				full_path = orig_full_path + "." + num++;

			full_path = full_path.Replace('\\', '/'); //seems freeswitch will selectively escape what it cant otherwise
			am_recording_file = full_path + ".wav";
			Utils.bgapi_exec("uuid_setvar", leg_b_uuid + " record_stereo true");
			Utils.bgapi_exec("uuid_record", leg_b_uuid + " start '" + am_recording_file  +"'");
		}

		private void Call_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
			CallPropertyEventArgs args = new CallPropertyEventArgs(this, e.PropertyName);
			if (e.PropertyName == "state" && CallStateChanged != null)
				CallStateChanged(this, args);

			if (CallPropertyChanged != null)
				CallPropertyChanged(this, args);
		}
		#endregion

		#region FSCore Event Handlers
		public static void NewFSEvent(object sender, FSEvent evt) {
			if (evt.event_id != switch_event_types_t.SWITCH_EVENT_MODULE_LOAD)
				Utils.DebugEventDump(evt);
			String uuid = evt.get_header("Unique-ID");
			switch (evt.event_id) {
				case switch_event_types_t.SWITCH_EVENT_CHANNEL_CREATE:
					handleChannelCreateEvent(evt, uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_CHANNEL_OUTGOING:
					HandleOutgoingEvent(evt, uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_CHANNEL_HANGUP_COMPLETE:
					HandleHangupCompleteEvent(evt, uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_CHANNEL_ANSWER:
					HandleChannelAnswerEvent(evt, uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_CUSTOM:
					HandleCustomEvent(evt, uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_CHANNEL_DESTROY:
					channels.Remove(uuid);
					break;
				case switch_event_types_t.SWITCH_EVENT_DTMF:
					HandleDTMFEvent(evt, uuid);
					break;
			}
		}

		private static void HandleDTMFEvent(FSEvent evt, string uuid) {
			String digit = evt.get_header("DTMF-Digit");
			var call = (from c in calls where c.leg_a_uuid == uuid || c.leg_b_uuid == uuid select c).SingleOrDefault();
			if (call.state == CALL_STATE.Answered && digit.Length == 1)
				PortAudio.PlayDTMF(digit[0], call.leg_a_uuid);

		}
		public static void HandleOutgoingEvent(FSEvent evt, String uuid) //capture an outgoing call the other leg
		{
			String other_leg = evt.get_header("Other-Leg-Unique-ID");
			Call call = (from c in calls where c.leg_a_uuid == other_leg select c).SingleOrDefault();
			if (call == null || !call.is_outgoing)
				return;
			call.leg_b_uuid = uuid;
		}
		public static void HandleHangupCompleteEvent(FSEvent evt, String uuid) {
			Utils.DebugEventDump(evt);
			Call call = (from c in calls where c.leg_a_uuid == uuid || c.leg_b_uuid == uuid select c).SingleOrDefault();
			if (call == null || call.call_ended)
				return;

			CALL_STATE new_state = CALL_STATE.None;


			if (call.state != CALL_STATE.Answered && call.state != CALL_STATE.Missed && call.state != CALL_STATE.Hold) {
				if (String.IsNullOrEmpty(call.note))
					call.note = evt.get_header("variable_sip_hangup_phrase");
				if (!call.is_outgoing && call.state == CALL_STATE.Ringing)
					new_state = CALL_STATE.Missed;
				else
					new_state = CALL_STATE.Failed;
			}
			else if (call.state == CALL_STATE.Answered || call.state == CALL_STATE.Hold)
				new_state = CALL_STATE.Ended;

			if (new_state == CALL_STATE.None)
				throw new Exception("Not sure what happened call was at state...: " + call.state);
			Call new_active_call;
			if (Call.active_call != call)
				new_active_call = Call.active_call;
			else
				new_active_call = (from c in calls where c.state == CALL_STATE.Ringing && c.is_outgoing == false && c != call select c).FirstOrDefault();
			call.UpdateCallState(new_state, new_active_call);
		}
		private static void handleChannelCreateEvent(FSEvent evt, string uuid) {
			Channel c;
			if (!channels.TryGetValue(uuid, out c)) {
				c = new Channel();
				channels.Add(uuid, c);
			}
			String gateway = evt.get_header("variable_sip_gateway");
			if (!String.IsNullOrEmpty(gateway))
				c.gateway_id = gateway;

		}

		private static void HandleCustomEvent(FSEvent evt, string uuid) {
			if (evt.subclass_name == "portaudio::ringing") {
				Utils.DebugEventDump(evt);
				if ((from c in calls where c._leg_a_uuid == uuid select c).Count() > 0)//only care about first ring
					return;
				Call call = new Call();
				call.SetCallInfoFromEvent(evt);
				String gw_id = (from c in channels where c.Key == call.leg_b_uuid select c.Value.gateway_id).SingleOrDefault();
				call.account = (from a in Account.accounts where a.gateway_id == gw_id select a).SingleOrDefault();
				calls.Add(call);
				call.UpdateCallState(CALL_STATE.Ringing, active_call ?? call);
			}
			else if (evt.subclass_name == "portaudio::makecall") {
				Utils.DebugEventDump(evt);
				if (evt.get_header("fail") == "true") {
					MessageBox.Show("Make Call failed!!!, came from portaudio not sure why");
					return;
				}
				Call call = new Call();
				call.is_outgoing = true;
				call.SetCallInfoFromEvent(evt);
				calls.Add(call);
				call.UpdateCallState(CALL_STATE.Ringing, call);
			}
			else if (evt.subclass_name == "portaudio::callheld" || evt.subclass_name == "portaudio::callresumed") {
				String paid_str = evt.get_header("variable_pa_call_id");
				if (String.IsNullOrEmpty(paid_str))
					return;
				int portaudio_id = Int32.Parse(paid_str);
				Call call = (from c in calls where c.portaudio_id == portaudio_id select c).SingleOrDefault();
				if (call == null)
					return;
				if (evt.subclass_name == "portaudio::callresumed")
					call.UpdateCallState(CALL_STATE.Answered, call);
				else
					call.UpdateCallState(call.state == CALL_STATE.Ringing ? CALL_STATE.Hold_Ringing : CALL_STATE.Hold, call == active_call ? null : active_call);
			}
		}

		private static void HandleChannelAnswerEvent(FSEvent evt, String uuid) {
			Call call = (from c in calls where c.leg_b_uuid == uuid select c).SingleOrDefault();
			if (call == null || call.state == CALL_STATE.Answered)
				return;

			if (call.state == CALL_STATE.Ringing || (call.state == CALL_STATE.Hold_Ringing && !call.is_outgoing))
				call.UpdateCallState(CALL_STATE.Answered, call.is_outgoing ? active_call : call);
			else if (call.state == CALL_STATE.Hold_Ringing)
				call.UpdateCallState(CALL_STATE.Hold, active_call);
			else
				throw new Exception("Unknown state, call answered but was not in a state of hold_ring or ringing");
		}
		#endregion

		private static System.Threading.Timer duration_timer;
		public Call() {

			PropertyChanged += Call_PropertyChanged;
			start_time = DateTime.Now;
			if (duration_timer == null)
				duration_timer = new System.Threading.Timer(DurationTimerFired, null, 1000, 1000);
			if (broker == null)
				broker = Broker.get_instance();
		}
		private static void DurationTimerFired(object obj) {
			if (Application.Current != null)
				Application.Current.Dispatcher.BeginInvoke((Action)(UpdateDurations));
		}
		private static void UpdateDurations() {
			foreach (Call call in calls) {
				if (call.call_ended)
					continue;
				call.duration = DateTime.Now - call.start_time;
			}
		}
		private void UpdateCallState(CALL_STATE new_state) {
			UpdateCallState(new_state, active_call);
		}
		private void UpdateCallState(CALL_STATE new_state, Call new_active_call) {
			bool actually_make_active = active_call != new_active_call;
			bool actually_update_state = state != new_state;
			bool set_call_ended = new_state != CALL_STATE.Answered && new_state != CALL_STATE.Ringing && new_state != CALL_STATE.Hold && new_state != CALL_STATE.Hold_Ringing && call_ended == false;
			ActiveCallChangedArgs args = null;

			if (actually_update_state)
				_state = new_state;
			if (actually_make_active) {
				args = new ActiveCallChangedArgs { new_active_call = new_active_call, old_active_call = active_call };
				active_call = new_active_call;
			}
			if (set_call_ended) {
				call_ended = true;
				_end_time = DateTime.Now;
			}

			if (args != null)
				ActiveCallChanged(null, args);

			if (actually_update_state)
				RaisePropertyChanged("state");
			if (set_call_ended) {
				RaisePropertyChanged("call_ended");
				RaisePropertyChanged("end_time");
			}
		}


		private MenuItem CreateMenuItem(String header, Action action){
			MenuItem item = new MenuItem { Header = header };
			item.Click += (o, args) => action();
			return item;
		}
		public ContextMenu CallRightClickMenu() {
			ContextMenu menu = new ContextMenu();
			menu.Opened += call_ContextMenuOpened;
		
			return menu;
		}


		private static Broker broker;
		public void SetOurAudioLevel(int level){
			if (level > 4 || level < -4)
				return;
			Utils.bgapi_exec("uuid_audio", leg_b_uuid + " start write level " + level);
		}
		public void SetOtherPartyAudioLevel(int level){
			if (level > 4 || level < -4)
				return;
			Utils.bgapi_exec("uuid_audio", leg_b_uuid + " start read level " + level);
		}
		public void RemoveCallFromHistory(){
			if (!call_ended)
				return;
			if (calls.Contains(this))
				calls.Remove(this);
		}


		private static Regex DestNumberRegex;
		private static Regex DestSipRegex;
		private void SetCallInfoFromEvent(FSEvent evt) {


			leg_a_uuid = evt.get_header("Unique-ID");
			leg_b_uuid = evt.get_header("Other-Leg-Unique-ID");
			if (is_outgoing) {
				bool is_sip_direct = false;
				if (DestNumberRegex == null)
					DestNumberRegex  = new Regex("sofia/gateway/([0-9]+)/(.+)", RegexOptions.Compiled);
				Match match = DestNumberRegex.Match(evt.get_header("Caller-Destination-Number"));
				if (! match.Success){
					if (DestSipRegex == null)
						DestSipRegex = new Regex("gw_ref=([0-9]+)}sofia/softphone/(.+)", RegexOptions.Compiled);
					match = DestSipRegex.Match(evt.get_header("Caller-Destination-Number"));
					is_sip_direct = true;
				}
				
				if (match.Success && match.Groups.Count == 3) {
					String gw_id = match.Groups[1].Value;
					account = (from a in Account.accounts where a.gateway_id == gw_id select a).SingleOrDefault();
					other_party_number = is_sip_direct ? "sip:" + match.Groups[2].Value : match.Groups[2].Value;
				}
			}
			else {
				other_party_name = evt.get_header("Caller-Caller-ID-Name");
				other_party_number = evt.get_header("Caller-Caller-ID-Number");
			}
			if (String.IsNullOrEmpty(other_party_name))
				other_party_name = other_party_number;

			String paid_str = evt.get_header("variable_pa_call_id");
			if (!String.IsNullOrEmpty(paid_str))
				portaudio_id = Int32.Parse(paid_str);
		}

		public void answer() {
			PortAudio.Answer(portaudio_id);
		}
		public void hangup() {
			PortAudio.Hangup(portaudio_id);
		}
		public void hangup(String reason) {
			note = reason;
			hangup();
		}
		public void hold() {
			if (state == CALL_STATE.Answered || (state == CALL_STATE.Ringing && this == active_call && is_outgoing))
				PortAudio.HoldAll();
		}
		public void unhold() {
			if (state == CALL_STATE.Hold)
				PortAudio.SwitchTo(portaudio_id);
		}
		public void TransferPrompt(){
			String res = InputBox.GetInput("Transfer To", "Enter a number to transfer to.", "");
			if (String.IsNullOrEmpty(res))
				return;
			Transfer(res);
		}

		public void Transfer(String number) {
			if (String.IsNullOrEmpty(number) || call_ended)
				return;
			if (String.IsNullOrEmpty(note))
				note = "Call transferred to: " + number;
			Utils.bgapi_exec("uuid_deflect", leg_b_uuid + " " + number);
		}
		public bool CanSendToVoicemail() {
			if (account == null)//TODO: FIX:: BAD//why are we null
				return false;
			String url = account.getSendVoicemailURL();
			return !String.IsNullOrEmpty(url);
		}
		public void SendToVoicemail() {
			if (!CanSendToVoicemail())
				return;
			note = "Call sent to voicemail";
			Transfer(account.getSendVoicemailURL());
		}
		public void send_dtmf(string dtmf) {
			dtmfs += dtmf;
			PortAudio.SendDTMF(dtmf);
		}
		public void switch_to() {
			if (active_call != this)
				PortAudio.SwitchTo(portaudio_id);
		}
		public void create_outgoing_call() {
			account.CreateCall(other_party_number);
		}
		public void DefaultAction() {
			switch (state) {
				case CALL_STATE.Hold_Ringing:
				case CALL_STATE.Hold:
						switch_to();
					break;
				case CALL_STATE.Ringing:
					if (is_outgoing)
						switch_to();
					else
						answer();
					break;
				case CALL_STATE.Missed:
				case CALL_STATE.Failed:
				case CALL_STATE.Ended:
				case CALL_STATE.Busy:
					create_outgoing_call();
					break;
			}
		}
	}

}
