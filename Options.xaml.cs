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

			load_devices(true);
		}
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
				chkIncomingFront.IsChecked = broker.IncomingTopMost;
				chkClearDTMFS.IsChecked = broker.ClearDTMFS;
				chkUseNumbers.IsChecked = broker.UseNumberOnlyInput;
				txtRecordingPath.Text = broker.recordings_folder;

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
			broker.IncomingTopMost = chkIncomingFront.IsChecked == true;
			broker.ClearDTMFS = chkClearDTMFS.IsChecked == true;
			broker.UseNumberOnlyInput = chkUseNumbers.IsChecked == true;
			broker.recordings_folder = txtRecordingPath.Text;
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
