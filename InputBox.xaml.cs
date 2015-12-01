using System;
using System.Windows;
using System.Windows.Automation;
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
			AutomationProperties.SetName(box.txtInput, desc);
			box.txtInput.Text = default_value;
			if (box.ShowDialog() != true)
				return null;

			return box.txtInput.Text;
		}
		public static string[] GetTwoInput(String title, String desc, String label1, String default_value1, String label2, String default_value2) {
			InputBox box = new InputBox();
			box.lblDesc.Content = desc;
			box.Title = title;
			box.rowInput.Height = new GridLength(60);
			box.Height = 170;
			AutomationProperties.SetName(box.txtInput, desc);
			box.txtInput.Text = default_value1;
			box.txtInput2.Text = default_value2;
			if(box.ShowDialog() != true)
				return null;

			return new[] { box.txtInput.Text, box.txtInput2.Text };
		}
		private void btnOk_Click(object sender, RoutedEventArgs e) {
			DialogResult = true;
			Close();
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

	}
}
