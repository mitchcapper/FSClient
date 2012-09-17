using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

namespace FSClient.Controls
{
	/// <summary>
	/// Interaction logic for PhonePadButton.xaml
	/// </summary>
	public partial class PhonePadButton : UserControl
	{
		public PhonePadButton()
		{
			this.InitializeComponent();
			btn.Click += new RoutedEventHandler(btn_Click);
		}

		void btn_Click(object sender, RoutedEventArgs e)
		{
			if (Click != null)
				Click(this, e);
		}
		[Category("Behavior")]
		public event RoutedEventHandler Click;

		public string Number
		{
			get { return lblNumber.Text; }
			set { lblNumber.Text = value; AutomationProperties.SetName(btn, value); AutomationProperties.SetName(this, value);  AutomationProperties.SetItemType(this,"Button"); }
		}
		public string Letters
		{
			get { return lblLetters.Text; }
			set { lblLetters.Text = value; }
		}
		protected AutomationControlType GetAutomationControlTypeCore() {
			return AutomationControlType.Button;
		}

	}
}