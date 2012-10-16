using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FSClient.Controls;

namespace FSClient {
	public partial class MainWindow : Window {
		private Broker broker;
		public MainWindow() {
			_instance = this;
			InitializeComponent();
			listCalls.DataContext = Call.calls;
			itemsAccounts.ItemsSource = Account.accounts;
			borderConference.DataContext = Conference.instance;
			borderConference.ContextMenu = Conference.instance.menu;
			Loaded += MainWindow_Loaded;
		}

		private void ActiveCallChanged(object sender, Call.ActiveCallChangedArgs e) {
			Dispatcher.BeginInvoke((Action)(() => {
				CurrentCallInfo.DataContext = Call.active_call;
				status.dial_str = Call.active_call != null ? Call.active_call.dtmfs : "";
				if (Call.active_call == null) {
					CurrentCallInfo.Visibility = Visibility.Hidden;
					CurrentStatusInfo.Visibility = Visibility.Visible;
				}
				else {
					CurrentStatusInfo.Visibility = Visibility.Hidden;
					CurrentCallInfo.Visibility = Visibility.Visible;
				}
			}));
		}
		private void CallStateChanged(object sender, Call.CallPropertyEventArgs e) {
			listCalls.Items.SortDescriptions.Clear();
			listCalls.Items.SortDescriptions.Add(new SortDescription("sort_order", ListSortDirection.Descending));
		}
		void accounts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
			if (e.OldItems != null) {
				RefreshStatusDefaultAccount();
				RefreshStatusAccountTotals();
			}
			if (e.NewItems == null)
				return;
			foreach (Account acct in e.NewItems) {
				acct.PropertyChanged += acct_PropertyChanged;
			}
			RefreshStatusDefaultAccount();
			RefreshStatusAccountTotals();
		}
		private void RefreshStatusAccountTotals() {
			status.total_accounts = (from a in Account.accounts where a.enabled == true select a).Count();
			status.active_accounts = (from a in Account.accounts where a.state == "REGED" select a).Count();
		}
		private void RefreshStatusDefaultAccount() {
			Account primary = (from a in Account.accounts where a.is_default_account == true select a).FirstOrDefault();
			status.primary_account = primary == null ? "" : primary.ToString();
		}
		void acct_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "gateway_id") {
				itemsAccounts.Items.SortDescriptions.Clear();
				itemsAccounts.Items.SortDescriptions.Add(new SortDescription("gateway_id", ListSortDirection.Ascending));
			}
			else if (e.PropertyName == "is_default_account") {
				RefreshStatusDefaultAccount();
			}
			else if (e.PropertyName == "state" || e.PropertyName == "enabled") {
				RefreshStatusAccountTotals();
			}

		}

		private void CanEndChanged(object sender, bool data) {
			Dispatcher.BeginInvoke((Action)(() => {
				btnHangup.IsEnabled = broker.CanEnd;
			}));
		}
		private void SpeakerActiveChanged(object sender, bool data) {
			Dispatcher.BeginInvoke((Action)(() => {
				btnSpeaker.Foreground = data ? enabled_brush : disabled_brush;
				AutomationProperties.SetName(btnDND, data ? "Disable Speakerphone" : "Enable Speakerphone");
			}));
		}
		private void MuteChanged(object sender, bool data) {
			Dispatcher.BeginInvoke((Action)(() => {
				btnMute.Foreground = data ? enabled_brush : disabled_brush;
				AutomationProperties.SetName(btnMute, data ? "Disable Mute" : "Enable Mute");
			}));
		}
		private void DNDChanged(object sender, bool data) {
			Dispatcher.BeginInvoke((Action)(() => {
				btnDND.Foreground = data ? enabled_brush : disabled_brush;
				Title = "FSClient " + (data ? " - DND" : "") + " " + version_str;
				AutomationProperties.SetName(btnDND, data ? "Disable DND" : "Enable DND");
			}));
		}
		private void CallActiveChanged(object sender, bool data) {
			Dispatcher.BeginInvoke((Action)(() => {
				btnHold.IsEnabled = broker.call_active;
				btnTransfer.IsEnabled = broker.call_active;
				txtNumber.IsReadOnly = broker.call_active;
			}));

		}



		#region TextInput

		private enum TEXT_INPUT_MODE { NUMBERS_ONLY, FULL };
		private void handle_key_action(char key) {
			if (txtNumber.IsReadOnly == false && txtNumber.IsKeyboardFocused) {
				int ind = txtNumber.CaretIndex;
				if (txtNumber.SelectionStart >= 0)
					status.dial_str = status.dial_str.Remove(txtNumber.SelectionStart, txtNumber.SelectionLength);
				status.dial_str = status.dial_str.Insert(ind, key.ToString());
				txtNumber.CaretIndex = ind + 1;
			}
			else
				status.dial_str += key;
			//status.dial_str += key;
			if (key != '*' && key != '#' && (key < '0' || key > '9') && (key < 'A' || key > 'D'))
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
			if (broker.active_calls == 0)
				PortAudio.CloseStreams();
		}
		public void TalkPressed(){
			if (Call.active_call != null) {
				if (Call.active_call.state == Call.CALL_STATE.Ringing && Call.active_call.is_outgoing == false)
					Call.active_call.answer();
			}
			else {
				if (! String.IsNullOrEmpty(status.dial_str)){
					broker.DialString(status.dial_str);
					status.dial_str = "";
				}
			}
		}
		private TEXT_INPUT_MODE text_mode = TEXT_INPUT_MODE.NUMBERS_ONLY;
		private void simple_text_mode_char_convert(char c) {
			if (c == '*' || c == '#' || (c >= '0' && c <= '9')) {
				handle_key_action(c);
				return;
			}
			switch (Char.ToUpper(c)) {
				case 'A':
				case 'B':
				case 'C':
					handle_key_action('2');
					break;
				case 'D':
				case 'E':
				case 'F':
					handle_key_action('3');
					break;
				case 'G':
				case 'H':
				case 'I':
					handle_key_action('4');
					break;
				case 'J':
				case 'K':
				case 'L':
					handle_key_action('5');
					break;
				case 'M':
				case 'N':
				case 'O':
					handle_key_action('6');
					break;
				case 'P':
				case 'Q':
				case 'R':
				case 'S':
					handle_key_action('7');
					break;
				case 'T':
				case 'U':
				case 'V':
					handle_key_action('8');
					break;
				case 'W':
				case 'X':
				case 'Y':
				case 'Z':
					handle_key_action('9');
					break;
			}
		}

		private bool text_interception_enabled = true;
		private bool text_interception_but_enter = false;
		private bool HandleTextInput(String text) {
			char[] chars = text.ToCharArray();
			bool handled = false;
			foreach (Char c in chars) {
				if (c >= 32 && c < 127) {
					if (text_mode == TEXT_INPUT_MODE.NUMBERS_ONLY || (Call.active_call != null && Call.active_call.state == Call.CALL_STATE.Answered))
						simple_text_mode_char_convert(c);
					else
						handle_key_action(c);
					handled = true;
				}
			}
			return handled;
		}
		void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e) {
			if (!broker.fully_loaded)
				return;
			if (!text_interception_enabled) {
				e.Handled = false;
				return;
			}
			e.Handled = HandleTextInput(e.Text);
			if (e.Handled)
				KeyInputFocusMove();

		}
		public static T GetVisualChild<T>(Visual parent) where T : Visual {
			T child = default(T);
			int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < numVisuals; i++) {
				Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
				child = v as T;
				if (child == null) {
					child = GetVisualChild<T>(v);
				}
				if (child != null) {
					break;
				}
			}
			return child;
		}
		private ToolTip last_tooltip_open;
		private Timer tooltip_auto_close = new Timer(1000 * 20);
		private void CloseOpenTooltip() {
			if (last_tooltip_open != null && last_tooltip_open.IsOpen)
				last_tooltip_open.IsOpen = false;
			last_tooltip_open = null;
		}
		private void KeyboardControlLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs keyboardFocusChangedEventArgs) {
			Control cntrl = sender as Control;
			if (cntrl == null)
				return;
			cntrl.LostKeyboardFocus -= KeyboardControlLostKeyboardFocus;
			CloseOpenTooltip();
		}
		private void ForceFocus(UIElement element){
			if (element.IsKeyboardFocused)
				DoNothingButton.Focus();
			element.Focus();
		}
		private void ConferenceMessageBox(Conference c) {
			String conf_members="";
			foreach (var u in Conference.users) {
				if (u.is_us)
					continue;
				String num_add = u.party_name != u.party_number ? " - " + u.party_number : "";
				String state = ConfStateConverter.StateConvert(u.state);
				if (!string.IsNullOrWhiteSpace(state))
					state = " (" + state + ")";
				conf_members += "Member " + u.party_name + state + num_add + "\n";
			}
			MessageBox.Show(conf_members, "Conference with " + Conference.users.Where(u => !u.is_us).Count() + " others");
		}
		private void CallMessageBox(Call c) {
			MessageBox.Show(
					"Number: " + c.other_party_number + 
					"\nStart Time: " + ShortDateTimeConverter.Convert(c.start_time) +
					"\nEnd Time: " + ShortDateTimeConverter.Convert(c.end_time) +
					"\nDuration: " + DurationTimeConverter.Convert(c.duration) + 
					"\nAccount: " + c.account + 
					"\nNote: " + c.note +
					"\nKeys: " + c.dtmfs

					
				, c.state + " " + EnglishDirectionConverter.Convert(c.is_outgoing) + " Call " + c.other_party_name);
		}
		private void TryShowTooltip(Control orig_cntrl) {
			if (tooltip_auto_close == null) {
				tooltip_auto_close = new Timer(1000 * 20);
				tooltip_auto_close.Elapsed += (o, evt) => CloseOpenTooltip();
				tooltip_auto_close.AutoReset = false;
			}
			if (orig_cntrl == null || orig_cntrl.ToolTip == null)
				return;
			ToolTip tt = (ToolTip)orig_cntrl.ToolTip;
			if (tt == last_tooltip_open && tt.IsOpen) {
				CloseOpenTooltip();
				return;
			}
			CloseOpenTooltip();
			tt.PlacementTarget = orig_cntrl;
			tt.Placement = PlacementMode.Right;
			tt.PlacementRectangle = new Rect(0, orig_cntrl.ActualHeight, 0, 0);
			tt.IsOpen = orig_cntrl.IsKeyboardFocusWithin;
			last_tooltip_open = tt;
			orig_cntrl.LostKeyboardFocus += KeyboardControlLostKeyboardFocus;
			tooltip_auto_close.Start();
		}
		private bool HandleShortcutKey(KeyEventArgs e) {
			bool handled = true;
			UIElement focus_element = null;
			switch (e.Key) {
				case Key.M:
					btnMute_Click(null, null);
					break;
				case Key.E:
					btnHangup_Click(null, null);
					break;
				case Key.D1:
				case Key.D2:
				case Key.D3:
				case Key.D4:
				case Key.D5:
				case Key.D6:
				case Key.D7:
				case Key.D8:
				case Key.D9:
				case Key.D0:
				case Key.NumPad0:
				case Key.NumPad1:
				case Key.NumPad2:
				case Key.NumPad3:
				case Key.NumPad4:
				case Key.NumPad5:
				case Key.NumPad6:
				case Key.NumPad7:
				case Key.NumPad8:
				case Key.NumPad9:
				case Key.L:
					if (e.Key != Key.L){
						String parse = e.Key.ToString();
						int selected_item = Int32.Parse(parse.Substring(parse.Length-1)) - 1;
						if (selected_item == -1)
							selected_item = 9;
						if (selected_item < listCalls.Items.Count)
							listCalls.SelectedIndex = selected_item;
					}
					if (listCalls.SelectedItem == null && listCalls.Items.Count > 0)
						listCalls.SelectedIndex = 0;
					if (listCalls.SelectedItem != null)
						focus_element = ((ListBoxItem)listCalls.ItemContainerGenerator.ContainerFromItem(listCalls.SelectedItem));
					else
						focus_element = listCalls;
					break;
				case Key.D:
					focus_element = btnKeypadOne;
					break;
				case Key.OemQuestion:
					Control corig_cntrl = e.OriginalSource as Control;
					if (corig_cntrl == null)
						break;
					Conference conf = corig_cntrl.DataContext as Conference;
					if (conf != null) {
						ConferenceMessageBox(conf);
						break;
					}
					Call c = corig_cntrl.DataContext as Call;
					if (c == null)
						break;
					CallMessageBox(c);
					break;
				case Key.H:
					TryShowTooltip(e.OriginalSource as Control);
					break;
				case Key.F:
					if (txtSearchBox.GetActualTextbox().IsKeyboardFocused)
						txtSearchBox.GetActualTextbox().SelectAll();
					else
						focus_element = txtSearchBox.GetActualTextbox();
					break;
				case Key.A:
					if (itemsAccounts.Items.Count > 0)
						focus_element = GetVisualChild<CheckBox>(((UIElement)itemsAccounts.ItemContainerGenerator.ContainerFromItem(itemsAccounts.Items[0])));
					else
						focus_element = itemsAccounts;
					break;
				default:
					handled = false;
					break;
			}
			if (focus_element != null)
				ForceFocus(focus_element);
			e.Handled = handled;
			return e.Handled;
		}
		private bool IsItAChild(FrameworkElement element,FrameworkElement parent){
			if (element == null)
				return false;
			if (element == parent)
				return true;
			if (element.Parent == null)
				return false;
			return IsItAChild(element.Parent as FrameworkElement, parent);
		}
		void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (!broker.fully_loaded)
				return;
			bool cntrl_pressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
			if (cntrl_pressed) {
				if (HandleShortcutKey(e)){
					return;
				}

			}

			if (!text_interception_enabled) {
				e.Handled = false;
				return;
			}
			if (e.Key == Key.Return) {

				if (text_interception_but_enter && IsItAChild(e.OriginalSource as FrameworkElement, listCalls)) //if we lost focus to another app we will not get the lost focus event so lets double check
					text_interception_but_enter = false;
				if (text_interception_but_enter) {
					e.Handled = false;
					return;
				}
				TalkPressed();
			}
			else if (e.Key == Key.Back){
				KeyInputFocusMove();
//				if (status.dial_str.Length > 0 && (Call.active_call == null || Call.active_call.state != Call.CALL_STATE.Answered))
	//				status.dial_str = status.dial_str.Remove(status.dial_str.Length - 1,1);
				return;
			}
			else if (e.Key == Key.Escape)
				HangupPressed();
			else if (e.Key == Key.V && cntrl_pressed) {
				KeyInputFocusMove();
				HandleTextInput(Clipboard.GetText());
			}
			else
				return;
			e.Handled = true;
		}
		private void KeyInputFocusMove(){
			if (!broker.call_active)
				txtNumber.Focus();
		}


		#endregion
		private static MainWindow _instance;
		public static MainWindow get_instance() {
			return _instance;
		}

		private void CallRingingChanged(object sender, bool data) {
			Dispatcher.Invoke((Action)(() => {
				if (data && Call.active_call != null && Call.active_call.CanSendToVoicemail())
					btnSendVoicemail.Visibility = Visibility.Visible;
				else
					btnSendVoicemail.Visibility = Visibility.Hidden;
			}));
		}
		void MainWindow_Loaded(object sender, RoutedEventArgs e) {
			PreviewTextInput += MainWindow_PreviewTextInput;
			txtNumber.PreviewKeyDown += txtNumber_PreviewKeyDown;
			//txtNumber.PreviewTextInput += MainWindow_PreviewTextInput;
			PreviewKeyDown += MainWindow_PreviewKeyDown; //return must be handled seperately as buttons are triggered on down it seems
			MouseUp += MainWindow_MouseUp;

			Call.CallStateChanged += CallStateChanged;
			Call.ActiveCallChanged += ActiveCallChanged;
			Account.accounts.CollectionChanged += accounts_CollectionChanged;
			Broker.FreeswitchLoaded += FreeswitchLoaded;
			broker = Broker.get_instance();
			DataContext = status;
			AccountDefaultConverter.normal_account_color = (SolidColorBrush)Resources["GridRowSpecialFGColor"];
			AccountDefaultConverter.default_account_color = (SolidColorBrush)Resources["RowHighlightFGColor"];
			broker.call_activeChanged += CallActiveChanged;
			broker.active_call_ringingChanged += CallRingingChanged;
			broker.MutedChanged += MuteChanged;
			broker.DNDChanged += DNDChanged;
			broker.CanEndChanged += CanEndChanged;
			broker.themeChanged += ThemeChanged;
			broker.UseNumberOnlyInputChanged += UseNumberOnlyInputChanged;
			UseNumberOnlyInputChanged(null, false);//trigger an update
			broker.SpeakerphoneActiveChanged += SpeakerActiveChanged;
			CurrentCallInfo.Visibility = Visibility.Hidden;
			Windows.systray_icon_setup();
			switch (broker.GUIStartup) {
				case "Calls":
					borderAccounts.Visibility = Visibility.Hidden;
					break;
				case "Accounts":
					borderCalls.Visibility = Visibility.Hidden;
					break;
				case "Dialpad":
					borderAccounts.Visibility = Visibility.Hidden;
					borderCalls.Visibility = Visibility.Hidden;
					break;
			}
			ResizeForm();
			btnTransfer.ContextMenu = broker.XFERContextMenu();
			borderTransfer.ContextMenu = broker.XFERContextMenu();
			if (broker.theme != "Steel")
				ReloadTheme();

			AcceptEnterForDoubleClick(btnConferenceCall, btnConferenceDoubleClick);
			SpeakerActiveChanged(null, false);
			MuteChanged(null, false);
			DNDChanged(null, false);
		}

		void txtNumber_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Space){
				HandleTextInput(" ");
				e.Handled = true;
			}
		}

		private void ThemeChanged(object sender, string data) {
			ReloadTheme();
		}

		void MainWindow_MouseUp(object sender, MouseButtonEventArgs e) {
			if (txtSearchBox.TextBoxHasFocus()) {
				DependencyObject parent = e.OriginalSource as UIElement;
				while (parent != null && !(parent is OurAutoCompleteBox))
					parent = VisualTreeHelper.GetParent(parent);
				if (parent == null)
					RemoveFocus();
			}

		}

		public void RemoveFocus(bool ResetContactSearchText = false) {
			txtNumber.Focus(); //should really divert focus a better way
			if (ResetContactSearchText)
				ResetContactSearchStr();
		}


		private String version_str = "";
		private void FreeswitchLoaded(object sender, EventArgs e) {
			Dispatcher.BeginInvoke((Action)(() => {
				busyAnimation.Visibility = Visibility.Hidden;
				version_str = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				Title = "FSClient " + version_str;
			}));
		}



		private void UseNumberOnlyInputChanged(object sender, bool data) {
			text_mode = broker.UseNumberOnlyInput ? TEXT_INPUT_MODE.NUMBERS_ONLY : TEXT_INPUT_MODE.FULL;
		}

		public void BringToFront() {
			Show();
			BringIntoView();
			WindowState = WindowState.Normal;
			Topmost = true;
			Topmost = false;
		}
		private void btnCall_Click(object sender, RoutedEventArgs e) {
			TalkPressed();

		}

		private void btnOptions_Click(object sender, RoutedEventArgs e) {
			Options opt = new Options();
			opt.ShowDialog();
		}

		private void btnHangup_Click(object sender, RoutedEventArgs e) {
			broker.HangupPressed();
		}
		private void listCalls_DoubleClick(object sender, object original_source) {
			FrameworkElement elem = original_source as FrameworkElement;
			if (elem == null)
				return;
			Call call = elem.DataContext as Call;
			if (call == null)
				return;
			call.DefaultAction();
		}
		private void listCalls_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			listCalls_DoubleClick(sender, e.OriginalSource);
		}

		SolidColorBrush enabled_brush = new SolidColorBrush(Colors.Yellow);
		SolidColorBrush disabled_brush = new SolidColorBrush(Colors.White);
		private void ReloadTheme() {
			String theme = broker.theme;
			if (theme != "Black" && theme != "RoyalBlue" && theme != "White")
				theme = "Steel";
			var rDictionary = new ResourceDictionary();
			rDictionary.Source = new Uri("/FSClient;component/Themes/" + theme + ".xaml", UriKind.Relative);
			Resources.MergedDictionaries[1] = rDictionary;
			AccountDefaultConverter.normal_account_color = (SolidColorBrush)Resources["GridRowSpecialFGColor"];
			AccountDefaultConverter.default_account_color = (SolidColorBrush)Resources["RowHighlightFGColor"];
			var arr = itemsAccounts.Items.SortDescriptions.ToArray();
			itemsAccounts.Items.SortDescriptions.Clear();
			foreach (var desc in arr)
				itemsAccounts.Items.SortDescriptions.Add(desc);
		}
		private void btnMute_Click(object sender, RoutedEventArgs e) {
			broker.Muted = !broker.Muted;
			ForceFocus(btnMute);
		}

		private void btnDND_Click(object sender, RoutedEventArgs e) {
			broker.DND = !broker.DND;
			ForceFocus(btnDND);
		}

		private void Window_Closing(object sender, CancelEventArgs e) {
			try{
				Windows.systray_icon_remove();
				Conference.instance.EndConference();
				foreach (var call in Call.calls)
					if (!call.call_ended)
						call.hangup();
			}catch{}
			try{
				broker.Dispose();
			}
			catch { }
		}

		private void btnSpeaker_Click(object sender, RoutedEventArgs e) {
			broker.SpeakerphoneActive = !broker.SpeakerphoneActive;
			ForceFocus(btnSpeaker);
		}


		private void AccountNew_Click(object sender, RoutedEventArgs e) {
			Account acct = new Account();
			Account.AddAccount(acct);
			if (!acct.edit())
				Account.RemoveAccount(acct);

		}

		private void AccountEdit_Click(object sender, RoutedEventArgs e) {
			Account acct = ((FrameworkElement)e.OriginalSource).DataContext as Account;
			if (acct == null)
				return;
			acct.edit();
		}

		private void AccountSetDefault_Click(object sender, RoutedEventArgs e) {
			Account acct = ((FrameworkElement)e.OriginalSource).DataContext as Account;
			if (acct == null)
				return;
			acct.is_default_account = true;
		}

		private void AccountDelete_Click(object sender, RoutedEventArgs e) {
			Account acct = ((FrameworkElement)e.OriginalSource).DataContext as Account;
			if (acct == null)
				return;
			Account.RemoveAccount(acct);
		}



		private void btnDialpad_Click(object sender, RoutedEventArgs e) {
			Button btn = sender as Button;
			if (btn != null)
				handle_key_action(btn.Content.ToString()[0]);
			PhonePadButton btn2 = sender as PhonePadButton;
			if (btn2 != null)
				handle_key_action(btn2.Number[0]);

		}

		private void btnHold_Click(object sender, RoutedEventArgs e) {
			if (Call.active_call != null)
				Call.active_call.hold();
		}

		private void btnSendVoicemail_Click(object sender, RoutedEventArgs e) {
			if (Call.active_call != null)
				Call.active_call.SendToVoicemail();
		}

		private void btnTransfer_Click(object sender, RoutedEventArgs e) {
			if (Call.active_call != null)
				Call.active_call.TransferPrompt();


		}
		#region ContactSearchBox
		public OurAutoCompleteBox GetContactSearchBox() {
			return txtSearchBox;
		}

		private const string contact_search_text = "Contact Search";
		private void txtSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			text_interception_enabled = false;
			txtSearchBox.Opacity = 1;
			if (txtSearchBox.Text == contact_search_text)
				txtSearchBox.Text = "";
		}

		private void txtSearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			text_interception_enabled = true;
			txtSearchBox.Opacity = 0.8;

			if (String.IsNullOrWhiteSpace(txtSearchBox.Text))
				ResetContactSearchStr();
		}
		public void ResetContactSearchStr() {
			txtSearchBox.Text = contact_search_text;
		}

		private bool ContactMenuOpen;
		private void contactSearchConextMenu_Closed(object sender, RoutedEventArgs e) {
			ContactMenuOpen = false;
			txtSearchBox.IsDropDownOpen = false;
		}

		private void contactSearchConextMenu_Loaded(object sender, RoutedEventArgs e) {
			ContactMenuOpen = true;
		}

		private void txtSearchBox_DropDownClosing(object sender, RoutedPropertyChangingEventArgs<bool> e) {
			if (ContactMenuOpen)
				e.Cancel = true;
		}
		#endregion
		private bool border_calls_was_visible = true;
		private void ResizeForm() {
			int border_calls_width = 228;
			int accounts_left = 237;
			int total_width = 243;
			int body_left = 3;
			if (borderAccounts.Visibility == Visibility.Visible) {
				total_width += 196;
				AutomationProperties.SetName(btnAccountsTab, "Accounts Pane Hide");
			}
			else {
				AutomationProperties.SetName(btnAccountsTab, "Accounts Pane Show");
			}
			if (borderCalls.Visibility == Visibility.Visible) {
				total_width += border_calls_width;
				body_left += border_calls_width;
				accounts_left += border_calls_width;
				if (!border_calls_was_visible) {
					border_calls_was_visible = true;
					Left -= border_calls_width;
				}
				AutomationProperties.SetName(btnCallsTab, "Calls Pane Hide");
			}
			else if (border_calls_was_visible) {
				border_calls_was_visible = false;
				Left += border_calls_width;
				AutomationProperties.SetName(btnCallsTab, "Calls Pane Show");
			}
			else
				AutomationProperties.SetName(btnCallsTab, "Calls Pane Show");

			Canvas.SetLeft(canvasPhoneBody, body_left);
			Canvas.SetLeft(borderAccounts, accounts_left);
			Width = total_width;
		}
		private void btnCallsTab_Click(object sender, RoutedEventArgs e) {
			if (borderCalls.Visibility == Visibility.Visible)
				borderCalls.Visibility = Visibility.Hidden;
			else
				borderCalls.Visibility = Visibility.Visible;
			ResizeForm();
			ForceFocus(btnCallsTab);
		}
		private void btnAccountsTab_Click(object sender, RoutedEventArgs e) {
			if (borderAccounts.Visibility == Visibility.Visible)
				borderAccounts.Visibility = Visibility.Hidden;
			else
				borderAccounts.Visibility = Visibility.Visible;
			ResizeForm();
			ForceFocus(btnAccountsTab);
		}
		StatusInfo status = new StatusInfo();
		private class StatusInfo : ObservableClass {
			public string dial_str {
				get { return _dial_str; }
				set {
					if (_dial_str == value)
						return;
					_dial_str = value;
					RaisePropertyChanged("dial_str");
				}
			}
			private string _dial_str="";

			public string primary_account {
				get { return _primary_account; }
				set {
					if (_primary_account == value)
						return;
					_primary_account = value;
					RaisePropertyChanged("primary_account");
				}
			}
			private string _primary_account;

			public int active_accounts {
				get { return _active_accounts; }
				set {
					if (_active_accounts == value)
						return;
					_active_accounts = value;
					RaisePropertyChanged("active_accounts");
				}
			}
			private int _active_accounts;

			public int total_accounts {
				get { return _total_accounts; }
				set {
					if (_total_accounts == value)
						return;
					_total_accounts = value;
					RaisePropertyChanged("total_accounts");
				}
			}
			private int _total_accounts;

		}


		private void btnClearAllCalls(object sender, RoutedEventArgs e) {
			Call.ClearCallsFromHistory();
		}

		private void borderConference_MouseDown(object sender, MouseButtonEventArgs e) {
			if (e.ClickCount == 2) {
				Conference.instance.join_conference();
			}
		}

		private void AccountReconnect_Click(object sender, RoutedEventArgs e) {
			Account acct = ((FrameworkElement)e.OriginalSource).DataContext as Account;
			if (acct == null)
				return;
			if (acct.enabled == false)
				acct.enabled = true;
			acct.ReloadAccount();
		}

		private void btnConferenceDoubleClick(object sender, MouseButtonEventArgs e) {
			Conference.instance.join_conference();
		}
		private void AcceptEnterForDoubleClick(UIElement element, Action<object, MouseButtonEventArgs> double_click_handler) {
			element.KeyDown += (sender, args) => {
				if (args.Key != Key.Enter && args.Key != Key.Return) return;
				args.Handled = true;
				double_click_handler(sender, null);
			};
		}

		private void listCalls_OnGotFocus(object sender, RoutedEventArgs e) {
			text_interception_but_enter = true;
		}

		private void listCalls_OnLostFocus(object sender, RoutedEventArgs e) {
			text_interception_but_enter = false;
		}

		private void listCalls_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.Key != Key.Enter && e.Key != Key.Return)
				return;
			e.Handled = true;
			listCalls_DoubleClick(sender, e.OriginalSource);
		}

		private void btnAccountAdd_Click(object sender, RoutedEventArgs e) {
			AccountNew_Click(null, null);
		}

		private void ContactSearchConextMenu_OnOpened(object sender, RoutedEventArgs e) {
			ContextMenu menu = sender as ContextMenu;
			menu.Items.Clear();
			foreach (var item in ContactPluginManager.ContactMenuItems)
				menu.Items.Add(item);


		}

		public void HangupPressed(){
			if (Call.active_call != null) {
				if (Call.active_call.state == Call.CALL_STATE.Ringing) {
					Call.active_call.hangup(Call.active_call.is_outgoing ? "User Cancelled" : "User Ignored Call");
				}
				else
					Call.active_call.hangup("User Ended");
			}
			else
				status.dial_str = "";
		}

		private void txtNumber_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
			txtNumber.CaretIndex = txtNumber.Text.Length;
		}

		private void AccountCheckBox_Checked(object sender, RoutedEventArgs e) {
			if (!broker.fully_loaded)
				return;
			Account account = ((FrameworkElement)e.OriginalSource).DataContext as Account;
			account.ReloadAccount();
		}
	}
}
