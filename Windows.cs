using System;

namespace FSClient {
	class Windows {
		private static System.Windows.Forms.NotifyIcon systray_icon;
		private static System.Drawing.Icon dnd_icon;
		private static System.Drawing.Icon normal_icon;
		public static void systray_icon_remove(){
			systray_icon.Visible = false;
		}
		public static void systray_icon_setup() {
			if (systray_icon != null)
				return;
			systray_icon = new System.Windows.Forms.NotifyIcon();
			systray_icon.Visible = true;

			systray_icon.Click += systray_icon_Click;

			dnd_icon = Properties.Resources.phone_dnd;
			normal_icon = Properties.Resources.phone;
			systrayicon_SetIcon(normal_icon);
			Broker.get_instance().DNDChanged += BrokerDNDChanged;
		}
		private static void BrokerDNDChanged(object sender, bool data) {
			systrayicon_SetIcon(data ? dnd_icon : normal_icon);
		}
		private static void systray_icon_Click(object sender, EventArgs e) {
			MainWindow.get_instance().BringToFront();
		}
		private static void systrayicon_SetIcon(System.Drawing.Icon ico) {
			App.Current.Dispatcher.BeginInvoke((Action)(() => {
				systray_icon.Icon = ico;
				systray_icon.Text = ico == dnd_icon ? "FSClient DND" : "FSClient Available";
			}));
		}
	}
}
