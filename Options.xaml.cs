using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using WF=System.Windows.Forms;

namespace FSClient {
	/// <summary>
	/// Interaction logic for Options.xaml
	/// </summary>
	public partial class Options : Window {

		public Options() {
			InitializeComponent();
		}
		private Broker broker = Broker.get_instance();
		private void Window_Loaded(object sender, RoutedEventArgs e) {
			GuiOptions = new List<ComboOption>();
			GuiOptions.Add(new ComboOption { key = "All",name = "Calls, Dialpad and Accounts"});
			GuiOptions.Add(new ComboOption { key = "Calls", name = "Calls and Dialpad" });
			GuiOptions.Add(new ComboOption { key = "Accounts", name = "Dialpad and Accounts" });
			GuiOptions.Add(new ComboOption { key = "Dialpad", name = "Dialpad Only" });
			comboGUIStartup.ItemsSource = GuiOptions;
			themes = new List<ComboOption>();
			themes.Add(new ComboOption {key="Steel",name="Steel" });
			themes.Add(new ComboOption { key = "RoyalBlue", name = "Royal Blue" });
			themes.Add(new ComboOption { key = "Black", name = "Black" });
			themes.Add(new ComboOption { key = "White", name = "White" });
			comboTheme.ItemsSource = themes;

			IncomingCallOptions = new List<ComboOption>();
			IncomingCallOptions.Add(new ComboOption { key = "None", name = "Do Nothing" });
			IncomingCallOptions.Add(new ComboOption { key = "Front", name = "Bring To Front" });
			IncomingCallOptions.Add(new ComboOption { key = "FrontKeyboard", name = "Bring To Front & Keyboard Focus" });
			comboOnIncomingCall.ItemsSource = IncomingCallOptions;

			load_devices(true);

		}
		private class ComboOption{
			public string name;
			public string key;
			public override string ToString() {
				return name;
			}
		}
		private List<ComboOption> GuiOptions;
		private List<ComboOption> themes;
		private List<ComboOption> IncomingCallOptions;
		private void load_devices(bool from_settings) {
			PortAudio.AudioDevice[] devices = broker.audio_devices;
			comboSpeakerInput.ItemsSource = comboHeadsetInput.ItemsSource = (from d in devices where d.inputs > 0 select d).ToArray();
			comboRingDevice.ItemsSource = comboSpeakerOutput.ItemsSource = comboHeadsetOutput.ItemsSource = (from d in devices where d.outputs > 0 select d).ToArray();
			string old_selected_item = comboHeadsetDevice.SelectedItem as string;
			comboHeadsetDevice.Items.Clear();
			comboHeadsetDevice.Items.Add("None");
			foreach (string headset in broker.AvailableHeadsets())
				comboHeadsetDevice.Items.Add(headset);
			comboHeadsetDevice.SelectedItem = old_selected_item;
			if (from_settings) {
				comboHeadsetInput.SelectedItem = broker.HeadsetInDev;
				comboHeadsetOutput.SelectedItem = broker.HeadsetOutDev;
				comboSpeakerInput.SelectedItem = broker.SpeakerInDev;
				comboSpeakerOutput.SelectedItem = broker.SpeakerOutDev;
				comboRingDevice.SelectedItem = broker.RingDev;
				chkIncomingBalloons.IsChecked = broker.IncomingBalloons;
				string incoming_key = "None";
				if (broker.IncomingTopMost)
					incoming_key = broker.IncomingKeyboardFocus ? "FrontKeyboard" : "Front";
				comboOnIncomingCall.SelectedItem = (from g in IncomingCallOptions where g.key == incoming_key select g).FirstOrDefault();
				if (comboOnIncomingCall.SelectedIndex == -1)
					comboOnIncomingCall.SelectedIndex = 1;
				chkClearDTMFS.IsChecked = broker.ClearDTMFS;
				chkUseNumbers.IsChecked = broker.UseNumberOnlyInput;
				chkUpdatesOnStart.IsChecked = broker.CheckForUpdates != "Never";
				chkNAT.IsChecked = broker.UPNPNAT;
				txtRecordingPath.Text = broker.recordings_folder;
				chkDirectSip.IsChecked = broker.DirectSipDial;
				chkAlwaysOnTopDuringCall.IsChecked = broker.AlwaysOnTopDuringCall;
				chkGlobalAlt.IsChecked = broker.global_hotkey.modifiers.HasFlag(UnManaged.KeyModifier.Alt);
				chkGlobalShift.IsChecked = broker.global_hotkey.modifiers.HasFlag(UnManaged.KeyModifier.Shift);
				chkGlobalCntrl.IsChecked = broker.global_hotkey.modifiers.HasFlag(UnManaged.KeyModifier.Ctrl);
				chkGlobalWin.IsChecked = broker.global_hotkey.modifiers.HasFlag(UnManaged.KeyModifier.Win);
				var key_char = broker.global_hotkey.key.ToString();
				if (broker.global_hotkey.key == System.Windows.Input.Key.None)
					key_char = "";
				key_char = key_char.Replace("NumPad", "").Replace("Oem","");
				if (key_char.Length == 2)
					key_char = key_char.Replace("D", "");
				if (key_char.Length > 1)
					key_char = "";
				txtHotKey.Text = key_char;
				comboGUIStartup.SelectedItem = (from g in GuiOptions where g.key == broker.GUIStartup select g).FirstOrDefault();
				if (comboGUIStartup.SelectedIndex == -1)
					comboGUIStartup.SelectedIndex = 0;
				comboTheme.SelectedItem = (from g in themes where g.key == broker.theme select g).FirstOrDefault();
				if (comboTheme.SelectedIndex == -1)
					comboTheme.SelectedIndex = 0;
				comboHeadsetDevice.SelectedItem = broker.ActiveHeadset();
				if (comboHeadsetDevice.SelectedIndex == -1)
					comboHeadsetDevice.SelectedIndex = 0;
			}
		}
		private void SaveSettings() {
			PortAudio.AudioDevice indev = comboHeadsetInput.SelectedItem as PortAudio.AudioDevice;
			PortAudio.AudioDevice outdev = comboHeadsetOutput.SelectedItem as PortAudio.AudioDevice;
			broker.SetHeadsetDevs(indev == null ? "" : indev.name, outdev == null ? "" : outdev.name);

			indev = comboSpeakerInput.SelectedItem as PortAudio.AudioDevice;
			outdev = comboSpeakerOutput.SelectedItem as PortAudio.AudioDevice;
			broker.SetSpeakerDevs(indev == null ? "" : indev.name, outdev == null ? "" : outdev.name);
			outdev = comboRingDevice.SelectedItem as PortAudio.AudioDevice;
			broker.SetRingDev(outdev == null ? "" : outdev.name);
			broker.IncomingBalloons = chkIncomingBalloons.IsChecked == true;
			string incoming_key = (comboOnIncomingCall.SelectedItem as ComboOption).key;
			if (incoming_key == "None")
				broker.IncomingKeyboardFocus = broker.IncomingTopMost = false;
			else{
				broker.IncomingTopMost = true;
				broker.IncomingKeyboardFocus = (incoming_key == "FrontKeyboard");
			}
			broker.ClearDTMFS = chkClearDTMFS.IsChecked == true;
			broker.UPNPNAT = chkNAT.IsChecked == true;
			broker.DirectSipDial = chkDirectSip.IsChecked == true;
			broker.AlwaysOnTopDuringCall = chkAlwaysOnTopDuringCall.IsChecked == true;
			broker.UseNumberOnlyInput = chkUseNumbers.IsChecked == true;
			broker.recordings_folder = txtRecordingPath.Text;
			broker.CheckForUpdates = chkUpdatesOnStart.IsChecked == true ?  "OnStart" : "Never";
			broker.GUIStartup = (comboGUIStartup.SelectedItem as ComboOption).key;
			broker.theme = (comboTheme.SelectedItem as ComboOption).key;
			System.Windows.Input.Key hot_key = System.Windows.Input.Key.None;
			var hot_key_modifier = UnManaged.KeyModifier.None;
			if (chkGlobalAlt.IsChecked == true)
				hot_key_modifier |= UnManaged.KeyModifier.Alt;
			if (chkGlobalCntrl.IsChecked == true)
				hot_key_modifier |= UnManaged.KeyModifier.Ctrl;
			if (chkGlobalShift.IsChecked == true)
				hot_key_modifier |= UnManaged.KeyModifier.Shift;
			if (chkGlobalWin.IsChecked == true)
				hot_key_modifier |= UnManaged.KeyModifier.Win;
			if (!String.IsNullOrWhiteSpace(txtHotKey.Text)) {
				var key = txtHotKey.Text.ToUpper();
				if (key[0] >= 0 && key[0] <= 9)
					key = "D" + key;
				Enum.TryParse<System.Windows.Input.Key>(key,out hot_key);
			}
			broker.SetHotKey(new Broker.HotKeySetting {modifiers = hot_key_modifier,key=hot_key });
			broker.SetActiveHeadset(comboHeadsetDevice.SelectedItem as string);
			broker.SaveSettings();

		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {

		}

		private void btnSave_Click(object sender, RoutedEventArgs e) {
			SaveSettings();
			DialogResult = true;
			Close();
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

		private void btnReloadDevices_Click(object sender, RoutedEventArgs e) {
			broker.reload_audio_devices(false, false);
			load_devices(false);
		}

		private void btnSofiaSettings_Click(object sender, RoutedEventArgs e) {
			broker.edit_sofia();
		}

		private void btnEventSocketSettings_Click(object sender, RoutedEventArgs e){
			broker.edit_event_socket();
		}
		private void btnConferenceSettings_Click(object sender, RoutedEventArgs e) {
			broker.edit_conference();
		}
		private void btnPluginSettings_Click(object sender, RoutedEventArgs e){
			broker.edit_plugins();
		}

		private void btnPathBrowse_Click(object sender, RoutedEventArgs e) {
			WF.FolderBrowserDialog dlg = new WF.FolderBrowserDialog();
			dlg.SelectedPath = txtRecordingPath.Text;
			DialogResult res = dlg.ShowDialog();
			if (res != System.Windows.Forms.DialogResult.OK)
				return;
			txtRecordingPath.Text = dlg.SelectedPath;
		}
	}
}
