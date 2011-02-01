using System;
using System.Windows;
using System.Windows.Input;

namespace FSClient {
	/// <summary>
	/// Interaction logic for InputBox.xaml
	/// </summary>
	public partial class InputBox : Window {
		public InputBox() {
			InitializeComponent();

			Loaded += InputBox_Loaded;
		}

		void InputBox_Loaded(object sender, RoutedEventArgs e) {
			txtInput.Focus();
		}
		public static string GetInput(String title, String desc, String default_value) {
			InputBox box = new InputBox();
			box.lblDesc.Content = desc;
			box.Title = title;
			box.txtInput.Text = default_value;
			if (box.ShowDialog() != true)
				return null;

			return box.txtInput.Text;
		}
		private void btnOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
			Close();
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

		private void txtInput_KeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Return)
				btnOk_Click(null, null);
		}
	}
}
