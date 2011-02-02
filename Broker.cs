using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

using FreeSWITCH.Native;
using FSClient.Controls;
using Timer = System.Timers.Timer;

namespace FSClient {
	class Broker : IDisposable {
		private Broker() {
			_instance = this;
			Utils.PluginLog("", "");//clear file
			headset_plugin_manager = new HeadsetPluginManager();

			NewEvent += Call.NewFSEvent;
			NewEvent += Account.NewEvent;

			Call.CallStateChanged += CallStateChangedHandler;
			Call.ActiveCallChanged += ActiveCallChanged;

			DelayedFunction.DelayedCall("LoadContactManager", initContactManager, 1000);
			init_us();
		}

		public bool fully_loaded;
		private void init_us() {
			if (is_inited)
				return;
			is_inited = true;
			try {
				if (System.IO.File.Exists("log/freeswitch.log")) {
					try {
						System.IO.File.WriteAllText("log/freeswitch.log", "");
					} catch (System.IO.IOException e) {
						MessageBox.Show(
							"Unable to truncate freeswitch.log (most likely due to FSCLient already running) due to: " + e.Message,
							"Truncation Error", MessageBoxButton.OK, MessageBoxImage.Error);
						Environment.Exit(-1);
					}
				}
				Account.LoadSettings();


				IncomingBallons = Properties.Settings.Default.IncomingBallons;
				IncomingTopMost = Properties.Settings.Default.FrontOnIncoming;
				ClearDTMFS = Properties.Settings.Default.ClearDTMFS;
				UseNumberOnlyInput = Properties.Settings.Default.UseNumberOnlyInput;

				if (Properties.Settings.Default.Sofia != null)
					sofia = Properties.Settings.Default.Sofia.GetSofia();
				else
					sofia = new Sofia();

			} catch (Exception e) {
				MessageBoxResult res = MessageBox.Show(
					"Unable to properly load our settings if you continue existing settings may be lost, do you want to continue?(No to exit)\n" +
					e.Message, "Error Loading Settings", MessageBoxButton.YesNo);
				if (res != MessageBoxResult.Yes)
					Environment.Exit(-1);
			}
			Thread t = new Thread(init_freeswitch);
			t.IsBackground = true;
			t.Start();
		}
		private void init_freeswitch() {
			try {//it would be better if this was in the init function but it seems some dll load errors won't be caught if it is.
#if ! NO_FS
				fs_core_init();
#else
				fs_inited = false;
#endif
				if (FreeswitchLoaded != null)
					FreeswitchLoaded(this, null);
				fully_loaded = true;
			} catch (Exception e) {
				while (e.InnerException != null)
					e = e.InnerException;
				MessageBox.Show("Unable to properly init freeswitch core due to:\n" + e.Message + "\n" + e.StackTrace, "Error Starting Freeswitch Core", MessageBoxButton.OK, MessageBoxImage.Error);
				fs_inited = false;
				Environment.Exit(-1);

			}
#if ! NO_FS
			DelayedFunction.DelayedCall("SofiaProfileCheck", sofia.sofia_profile_check, 100);
#endif
		}

		private void initContactManager() {
			contact_plugin_manager = new ContactPluginManager();

		}

		#region Text Input


		public enum KEYBOARD_ACTION { Backspace, Erase, Enter, Escape };
		public void handle_special_action(KEYBOARD_ACTION action) {
			switch (action) {
				case KEYBOARD_ACTION.Backspace:
					if (cur_dial_str.Length > 0 && (Call.active_call == null || Call.active_call.state != Call.CALL_STATE.Answered))
						cur_dial_str = cur_dial_str.Remove(cur_dial_str.Length - 1);
					break;
				case KEYBOARD_ACTION.Escape:
					HangupPressed();
					break;
				case KEYBOARD_ACTION.Enter:
					TalkPressed();
					break;

			}

		}
		public void handle_key_action(char key) {
			cur_dial_str += key;
			if (key != '*' && key != '#' && (key < '0' || key > '9'))
				return;

			if (Call.active_call != null && Call.active_call.state == Call.CALL_STATE.Answered)
				Call.active_call.send_dtmf(key.ToString());
			else {
#if ! NO_FS
				PortAudio.PlayDTMF(key, null, true);
				DelayedFunction.DelayedCall("PortAudioLastDigitHitStreamClose", close_streams, 5000);
#endif
			}
		}
		private void close_streams() {
			if (active_calls == 0)
				PortAudio.CloseStreams();
		}
		#endregion

		public PortAudio.AudioDevice SpeakerInDev { get; private set; }
		public PortAudio.AudioDevice SpeakerOutDev { get; private set; }
		public PortAudio.AudioDevice HeadsetInDev { get; private set; }
		public PortAudio.AudioDevice HeadsetOutDev { get; private set; }
		public PortAudio.AudioDevice RingDev { get; private set; }
		public PortAudio.AudioDevice[] audio_devices;

		public string[] AvailableHeadsets() {
			return headset_plugin_manager.AvailableDevices();
		}
		public string ActiveHeadset() {
			return headset_plugin_manager.ActiveDevice();
		}

		public void SetActiveHeadset(String name) {
			if (String.IsNullOrEmpty(name) || name == "None")
				name = null;
			headset_plugin_manager.SetActiveDevice(name);
		}
		private void LoadAudioSettings() {
			SetSpeakerDevs(Properties.Settings.Default.SpeakerInDev, Properties.Settings.Default.SpeakerOutDev);
			SetHeadsetDevs(Properties.Settings.Default.HeadsetInDev, Properties.Settings.Default.HeadsetOutDev);
			if (!DND)
				SetRingDev(Properties.Settings.Default.RingDev);
			SetActiveHeadset(Properties.Settings.Default.HeadsetDevice);
		}
		private void SaveAudioSettings() {
			if (HeadsetInDev != null)
				Properties.Settings.Default.HeadsetInDev = HeadsetInDev.name;
			if (HeadsetOutDev != null)
				Properties.Settings.Default.HeadsetOutDev = HeadsetOutDev.name;
			if (SpeakerInDev != null)
				Properties.Settings.Default.SpeakerInDev = SpeakerInDev.name;
			if (SpeakerOutDev != null)
				Properties.Settings.Default.SpeakerOutDev = SpeakerOutDev.name;
			if (RingDev != null)
				Properties.Settings.Default.RingDev = RingDev.name;
			Properties.Settings.Default.HeadsetDevice = ActiveHeadset();
		}
		private static PortAudio.AudioDevice AudioNameToDevice(IEnumerable<PortAudio.AudioDevice> devices, string name) {
			if (devices == null)
				return null;
			return devices.FirstOrDefault(device => device.name == name);
		}
		public void SetRingDev(String dev_name) {
			RingDev = AudioNameToDevice(audio_devices, dev_name);
			if (RingDev != null && !DND)
				RingDev.SetRingDev();
		}
		public void SetSpeakerDevs(String indev_name, String outdev_name) {
			SpeakerInDev = AudioNameToDevice(audio_devices, indev_name);
			SpeakerOutDev = AudioNameToDevice(audio_devices, outdev_name);
			if (SpeakerInDev == null || SpeakerOutDev == null)
				return;
			if (SpeakerphoneActive)
				activateCurrentDevs();
		}
		private void activateCurrentDevs() {
			if (SpeakerphoneActive)
				PortAudio.SetInAndOutDev(SpeakerInDev, SpeakerOutDev);
			else
				PortAudio.SetInAndOutDev(HeadsetInDev, HeadsetOutDev);
		}
		public void SetSpeakerOutDev(String name) {
			SpeakerOutDev = AudioNameToDevice(audio_devices, name);
			if (SpeakerphoneActive && SpeakerOutDev != null)
				SpeakerOutDev.SetOutDev();
		}
		public void SetHeadsetDevs(String indev_name, String outdev_name) {
			HeadsetInDev = AudioNameToDevice(audio_devices, indev_name);
			HeadsetOutDev = AudioNameToDevice(audio_devices, outdev_name);
			if (HeadsetInDev != null && HeadsetOutDev != null) {
				if (!SpeakerphoneActive)
					activateCurrentDevs();
			}
		}

		public void reload_audio_devices(bool and_settings, bool no_save) {
			if (and_settings && !no_save)
				SaveAudioSettings();
			audio_devices = PortAudio.get_devices(true);
			if (and_settings)
				LoadAudioSettings();
			activateCurrentDevs();
		}
		public void DialTone() {
			//if (Call.active_call != null)
			//    Call.active_call.hold();
			//OffHook = true;
			//Utils.bgapi_exec("pa", "play tone_stream://%(10000,0,350,440);loops=20");
		}
		public void DialString(String str){
			if (string.IsNullOrWhiteSpace(str))
				return;
			MainWindowRemoveFocus();

			if (str.StartsWith("#") && str.Length > 2) {
				String acct_num = str.Substring(1, 1);
				str = str.Substring(2);
				Account acct = (from a in Account.accounts where a.guid == acct_num select a).SingleOrDefault();
				if (acct != null) {
					acct.CreateCall(str);
					return;
				}
			}
			if (Account.default_account == null) {
				MessageBox.Show("no default account, make sure you have added one or more accounts (right click in the account area to add) and they are enabled (checked)");
				return;
			}
			Account.default_account.CreateCall(str);
		}
		public void TalkPressed() {
			if (Call.active_call != null) {
				if (Call.active_call.state == Call.CALL_STATE.Ringing && Call.active_call.is_outgoing == false)
					Call.active_call.answer();
				else
					DialTone();
			} else {
				if (String.IsNullOrEmpty(cur_dial_str))
					DialTone();
				else{
					DialString(cur_dial_str);
					cur_dial_str = "";
				}
			}
		}
		public void HangupPressed() {
			if (Call.active_call != null) {
				if (Call.active_call.state == Call.CALL_STATE.Ringing) {
					Call.active_call.hangup(Call.active_call.is_outgoing ? "User Cancelled" : "User Ignored Call");
				} else
					Call.active_call.hangup("User Ended");
			} else
				cur_dial_str = "";
		}
		public void FlashPressed() {
			throw new NotImplementedException();
		}
		public void TalkTogglePressed() {
			if (Call.active_call != null && (Call.active_call.state != Call.CALL_STATE.Ringing || Call.active_call.is_outgoing))
				HangupPressed();
			else
				TalkPressed();
		}
		public void RedialPressed() {
			throw new NotImplementedException();
		}


		#region deconstructors
		~Broker() {
			Dispose();
		}
		private bool disposed;
		public void Dispose() {
			if (!disposed) {
				disposed = true;
				if (fully_loaded)
					SaveSettings();
				headset_plugin_manager.Dispose();
				if (fully_loaded && fs_inited)
					freeswitch.switch_core_destroy();
				is_inited = false;
			}
			GC.SuppressFinalize(this);
		}
		#endregion
		public void SaveSettings() {
			try {
				SaveAudioSettings();
				Account.SaveSettings();
				Properties.Settings.Default.IncomingBallons = IncomingBallons;
				Properties.Settings.Default.FrontOnIncoming = IncomingTopMost;
				Properties.Settings.Default.ClearDTMFS = ClearDTMFS;
				Properties.Settings.Default.UseNumberOnlyInput = UseNumberOnlyInput;

				Properties.Settings.Default.Sofia = new SettingsSofia(sofia);
				Properties.Settings.Default.Save();
			} catch (Exception e) {//if there is an error doing saving lets skip saving any settings to avoid overriding something else
				MessageBox.Show("Error saving settings out: " + e.Message + "\n" + e.StackTrace);
			}
		}
		private void UpdateStatus() {
			int cur_active_calls = Call.live_call_count();
			bool is_call_active = (Call.active_call != null);
			bool devices_ready = cur_active_calls > 0;
			bool is_call_answered = (from c in Call.calls where c.state == Call.CALL_STATE.Answered || (c.state == Call.CALL_STATE.Ringing && c.is_outgoing) select true).Count() > 0;
			bool is_active_call_ringing = Call.active_call != null && Call.active_call.is_outgoing == false && Call.active_call.state == Call.CALL_STATE.Ringing;

			bool actually_set_active_calls = active_calls != cur_active_calls;
			bool actually_set_call_active = call_active != is_call_active;
			bool actually_set_CanEnd = actually_set_call_active;
			bool actually_set_DevicesReady = DevicesReady != devices_ready;
			bool actually_set_active_call_ringing = active_call_ringing != is_active_call_ringing;
			bool actually_set_call_answered = call_answered != is_call_answered;

			if (actually_set_active_calls)
				_active_calls = cur_active_calls;
			if (actually_set_call_active)
				_call_active = is_call_active;
			if (actually_set_CanEnd)
				_CanEnd = is_call_active;
			if (actually_set_DevicesReady)
				_DevicesReady = devices_ready;
			if (actually_set_active_call_ringing)
				_active_call_ringing = is_active_call_ringing;
			if (actually_set_call_answered)
				_call_answered = is_call_answered;


			if (actually_set_active_calls && active_callsChanged != null)
				active_callsChanged(this, cur_active_calls);
			if (actually_set_call_active && call_activeChanged != null)
				call_activeChanged(this, is_call_active);
			if (actually_set_CanEnd && CanEndChanged != null)
				CanEndChanged(this, CanEnd);
			if (actually_set_DevicesReady && DevicesReadyChanged != null)
				DevicesReadyChanged(this, devices_ready);
			if (actually_set_active_call_ringing && active_call_ringingChanged != null)
				active_call_ringingChanged(this, is_active_call_ringing);
			if (actually_set_call_answered && call_answeredChanged != null)
				call_answeredChanged(this, is_call_answered);

		}
		private void ActiveCallChanged(object sender, Call.ActiveCallChangedArgs e) {
			if (Call.active_call != null)
				cur_dial_str = Call.active_call.dtmfs;
			else
				cur_dial_str = "";
		}
		private void HandleCallWaiting(Timer timer, Call c) {
			if (c.state != Call.CALL_STATE.Ringing || Call.active_call == c) {
				if (timer != null) {
					timer.Stop();
					timer.Dispose();
					return;
				}
			}
			if (timer == null) {
				timer = new Timer(4000);
				timer.Elapsed += (s, e) => HandleCallWaiting(timer, c);
				timer.Start();
			}
			if (Call.active_call != null && Call.active_call.state == Call.CALL_STATE.Answered)
				PortAudio.PlayInUUID(Call.active_call.leg_a_uuid, "tone_stream://%(200,100,440);loops=2;");



		}
		private void CallStateChangedHandler(object sender, Call.CallPropertyEventArgs args) {
			if (args.call.state == Call.CALL_STATE.Ringing && !args.call.is_outgoing) {
				if (IncomingTopMost) {
					MainWindow.get_instance().BringToFront();
				}
				if (IncomingBallons && !DND) {
					IncomingCallNotification.ShowCallNotification(args.call);
					if (Call.active_call != args.call && Call.active_call != null)
						HandleCallWaiting(null, args.call);
				}
			}
			if (DND && args.call != null && args.call.is_outgoing == false && args.call.state == Call.CALL_STATE.Ringing)
				args.call.hangup("Call ignored due to DND");
			if (args.call != null && args.call.call_ended && ClearDTMFS)
				args.call.dtmfs = "";

			DelayedFunction.DelayedCall("broker_updatestatus", UpdateStatus, 500);
		}
		#region properties

		public bool IncomingBallons;
		public bool IncomingTopMost;

		public bool UseNumberOnlyInput {
			get { return _UseNumberOnlyInput; }
			set {
				if (value == _UseNumberOnlyInput)
					return;
				_UseNumberOnlyInput = value;
				if (UseNumberOnlyInputChanged != null)
					UseNumberOnlyInputChanged(this, value);
			}
		}
		private bool _UseNumberOnlyInput;
		public Utils.ObjectEventHandler<bool> UseNumberOnlyInputChanged;

		public bool ClearDTMFS {
			get { return _ClearDTMFS; }
			set {
				if (value == _ClearDTMFS)
					return;
				_ClearDTMFS = value;
				if (ClearDTMFS) {
					foreach (Call c in Call.calls)
						c.dtmfs = "";
				}
				if (ClearDTMFSChanged != null)
					ClearDTMFSChanged(this, value);
			}
		}
		private bool _ClearDTMFS;
		public Utils.ObjectEventHandler<bool> ClearDTMFSChanged;

		public bool SpeakerphoneActive {
			get { return _SpeakerphoneActive; }
			set {
				if (value == _SpeakerphoneActive)
					return;
				_SpeakerphoneActive = value;
				activateCurrentDevs();
				if (SpeakerphoneActiveChanged != null)
					SpeakerphoneActiveChanged(this, value);
			}
		}
		private bool _SpeakerphoneActive;
		public Utils.ObjectEventHandler<bool> SpeakerphoneActiveChanged;
		public bool call_answered {
			get { return _call_answered; }
		}
		private bool _call_answered;
		public Utils.ObjectEventHandler<bool> call_answeredChanged;
		public bool call_active {
			get { return _call_active; }
			set {
				if (value == _call_active)
					return;
				_call_active = value;
				if (call_activeChanged != null)
					call_activeChanged(this, value);
			}
		}
		private bool _call_active;
		public Utils.ObjectEventHandler<bool> call_activeChanged;

		public bool active_call_ringing {
			get { return _active_call_ringing; }
		}
		private bool _active_call_ringing;
		public Utils.ObjectEventHandler<bool> active_call_ringingChanged;


		public int active_calls {
			get { return _active_calls; }
		}
		private int _active_calls;
		public Utils.ObjectEventHandler<int> active_callsChanged;


		public bool CanEnd //true when hangup would have an effect, an active call or a ringing call, dial tone
		{
			get { return _CanEnd; }
		}
		private bool _CanEnd;
		public Utils.ObjectEventHandler<bool> CanEndChanged;

		public bool DevicesReady//true when we know ther user may be doing something soon, call ringing, call active, calls on hold
		{
			get { return _DevicesReady; }
		}
		private bool _DevicesReady;
		public Utils.ObjectEventHandler<bool> DevicesReadyChanged;

		public bool Muted {
			get { return _Muted; }
			set {
				if (value == _Muted)
					return;
				_Muted = value;
				PortAudio.set_mute(value);
				if (MutedChanged != null)
					MutedChanged(this, value);
			}
		}
		private bool _Muted;
		public Utils.ObjectEventHandler<bool> MutedChanged;
		public bool DND {
			get { return _DND; }
			set {
				if (value == _DND)
					return;
				_DND = value;
				if (value) {
					foreach (Call call in Call.calls) {
						if (call.state == Call.CALL_STATE.Ringing && !call.is_outgoing)
							call.hangup("Call ignored due to DND");
					}
					PortAudio.ClearRingDev();
				} else if (RingDev != null)
					RingDev.SetRingDev();
				if (DNDChanged != null)
					DNDChanged(this, value);
			}
		}
		private bool _DND;
		public Utils.ObjectEventHandler<bool> DNDChanged;

		public string cur_dial_str {
			get { return _cur_dial_str; }
			set {
				if (value == _cur_dial_str)
					return;
				_cur_dial_str = value;
				if (cur_dial_strChanged != null)
					cur_dial_strChanged(this, value);
			}
		}
		private string _cur_dial_str;
		public Utils.ObjectEventHandler<string> cur_dial_strChanged;
		#endregion



		private static Broker _instance;
		public static Broker get_instance() {
			return _instance ?? (_instance = new Broker());
		}

		ContactPluginManager contact_plugin_manager;
		HeadsetPluginManager headset_plugin_manager;
		Sofia sofia;
		public void edit_sofia() {
			sofia.edit();
		}
		public void reload_sofia(Sofia.RELOAD_CONFIG_MODE mode) {
			sofia.reload_config(mode);
		}
		public static EventHandler<EventArgs> FreeswitchLoaded;
		public static EventHandler<FSEvent> NewEvent;
		private void event_handler(FreeSWITCH.EventBinding.EventBindingArgs args) {

			if (BroadcastHandler == null)
				BroadcastHandler = new BroadcastEventDel(BroadcastEvent);
			Application.Current.Dispatcher.BeginInvoke(BroadcastHandler, new object[] { new FSEvent(args.EventObj) });
		}

		public OurAutoCompleteBox GetContactSearchBox() {
			return MainWindow.get_instance().GetContactSearchBox();
		}
		public void MainWindowRemoveFocus(){
			MainWindow.get_instance().RemoveFocus();
		}
		private delegate void BroadcastEventDel(FSEvent evt);
		BroadcastEventDel BroadcastHandler;
		private void BroadcastEvent(FSEvent evt) {
			if (NewEvent != null)
				NewEvent(this, evt);
		}

		#region Config Generation
		XmlDocument sofia_doc;
		private string get_sofia_xml() {

			sofia_doc = new XmlDocument();
			XmlNode doc = sofia_doc.CreateElement("document");
			sofia_doc.AppendChild(doc);
			XmlUtils.AddNodeAttrib(doc, "type", "freeswitch/xml");
			XmlNode sect = XmlUtils.AddNodeNode(doc, "section");
			XmlUtils.AddNodeAttrib(sect, "name", "configuration");
			sofia.gen_config(sect);
			//sofia_doc.Save(@"c:\temp\out.xml");
			return sofia_doc.OuterXml;
		}
		private string xml_search(FreeSWITCH.SwitchXmlSearchBinding.XmlBindingArgs args) {
			if (args.KeyName == "name" && args.KeyValue == "sofia.conf" && args.Section == "configuration" && args.TagName == "configuration") {
				return get_sofia_xml();
			}
			return null;
		}
		private static IDisposable search_bind;
		#endregion
		private bool is_inited;
		private bool fs_inited;
		private static IDisposable event_bind;
		
		private void fs_core_init() {
			fs_inited = true;
			freeswitch.switch_core_set_globals();
			String err = "";
			const uint flags = (uint)(switch_core_flag_enum_t.SCF_USE_SQL | switch_core_flag_enum_t.SCF_USE_AUTO_NAT);
			switch_status_t res = freeswitch.switch_core_init(flags, switch_bool_t.SWITCH_FALSE, ref err);
			search_bind = FreeSWITCH.SwitchXmlSearchBinding.Bind(xml_search, switch_xml_section_enum_t.SWITCH_XML_SECTION_CONFIG);
			event_bind = FreeSWITCH.EventBinding.Bind("FSClient", switch_event_types_t.SWITCH_EVENT_ALL, null, event_handler, true);
			freeswitch.switch_core_init_and_modload(flags, switch_bool_t.SWITCH_FALSE, ref err);
			reload_audio_devices(true, true);
		}

	}
}
