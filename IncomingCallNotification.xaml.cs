using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;

namespace FSClient {
	/// <summary>
	/// Interaction logic for IncomingCallNotifcation.xaml
	/// </summary>
	public partial class IncomingCallNotification : Window {
		private static List<IncomingCallNotification> windows = new List<IncomingCallNotification>();
		public IncomingCallNotification() {
			this.InitializeComponent();
		}
		private Call call;
		public static void ShowCallNotification(Call call) {
			IncomingCallNotification notifier = new IncomingCallNotification();
			notifier.call = call;
			windows.Add(notifier);
			notifier.Show();
		}
		private System.ComponentModel.PropertyChangedEventHandler prop_changed;
		private void Window_Loaded(object sender, RoutedEventArgs e) {
			PositionWindows();
			lblCaller.Text = call.other_party_name + " - " + call.other_party_number;
			Title = "FSClient Incoming Call " + lblCaller.Text;
			prop_changed = new System.ComponentModel.PropertyChangedEventHandler(call_PropertyChanged);
			call.PropertyChanged += prop_changed;
			btnSendVoicemail.Visibility = call.CanSendToVoicemail() ? Visibility.Visible : Visibility.Hidden;
			btnTransfer.ContextMenu = Broker.get_instance().XFERContextMenu();
			btnTransfer.DataContext = call;
			if (Broker.get_instance().IncomingKeyboardFocus)
				DelayedFunction.DelayedCall("BubbleTop", MakeUsTop, 500);
			Show();
			Topmost = true;
		}
		private void MakeUsTop(){
			Dispatcher.BeginInvoke((Action)(() => Utils.SetForegroundWindow(this)));
		}
		void call_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
			if (e.PropertyName == "state") {
				if (call.state != Call.CALL_STATE.Ringing)
					close_us();
			}
		}
		private static void PositionWindows() {
			double top_offset = 0;
			double left_offset = 0;
			double window_height = 0;
			bool is_first = true;
			foreach (IncomingCallNotification window in windows) {
				if (is_first) {
					is_first = false;
					System.Drawing.Rectangle workingArea = new System.Drawing.Rectangle((int)window.Left, (int)window.Top, (int)window.ActualWidth, (int)window.ActualHeight);
					workingArea = Screen.GetWorkingArea(workingArea);
					left_offset = workingArea.Right - window.ActualWidth;
					top_offset = workingArea.Bottom - window.ActualHeight;
					window_height = window.ActualHeight;
				}
				window.Top = top_offset;
				window.Left = left_offset;
				top_offset -= window_height;
			}
		}
		private void close_us() {
			call.PropertyChanged -= prop_changed;
			Hide();
			windows.Remove(this);
			PositionWindows();
			Close();
		}
		private void btnCall_Click(object sender, RoutedEventArgs e) {
			call.answer();
		}

		private void btnHangup_Click(object sender, RoutedEventArgs e) {
			call.hangup();
		}

		private void btnSendVoicemail_Click(object sender, RoutedEventArgs e) {
			call.SendToVoicemail();
		}

		private void btnTransfer_Click(object sender, RoutedEventArgs e) {
			String res = InputBox.GetInput("Transfer To", "Enter a number to transfer to.", "");
			if (String.IsNullOrEmpty(res))
				return;
			if (call != null)
				call.Transfer(res);
		}
	}
}