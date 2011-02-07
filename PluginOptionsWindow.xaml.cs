using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FSClient {
	/// <summary>
	/// Interaction logic for PluginOptionsWindow.xaml
	/// </summary>
	public partial class PluginOptionsWindow : Window {
		public PluginOptionsWindow() {
			InitializeComponent();
			LoadSettings();
		}
		private Broker broker = Broker.get_instance();
		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}

		private void btnSave_Click(object sender, RoutedEventArgs e) {
			SaveSettings();
			DialogResult = true;
			Close();
		}

		private IEnumerable<PluginManagerBase.PluginData> headset_plugins;
		private IEnumerable<PluginManagerBase.PluginData> contact_plugins;
		private List<loaded_plugin_info> loaded_plugins = new List<loaded_plugin_info>();
		
		private void LoadSettings(){
						
			contact_plugins = broker.contact_plugins;
			headset_plugins = broker.headset_plugins;
			original_loaded_str = "";
			MakeLoadedStr(contact_plugins, ref original_loaded_str);
			MakeLoadedStr(headset_plugins, ref original_loaded_str);
			comboContact.Items.Clear();
			listHeadset.Items.Clear();
			loaded_plugins.Clear();
			comboContact.Items.Add("None");

			foreach (PluginManagerBase.PluginData data in contact_plugins){
				if (data.state != PluginManagerBase.PluginData.PluginDataState.MISSING){
					comboContact.Items.Add(data.dll);
					if (data.enabled)
						comboContact.SelectedItem = data.dll;
				}
				if (data.state != PluginManagerBase.PluginData.PluginDataState.SKIPPED && data.state != PluginManagerBase.PluginData.PluginDataState.MISSING)
					loaded_plugins.Add(new loaded_plugin_info(data));
			}
			if (comboContact.SelectedIndex == -1)
				comboContact.SelectedIndex = 0;

			foreach (PluginManagerBase.PluginData data in headset_plugins){
				if (data.state != PluginManagerBase.PluginData.PluginDataState.MISSING){
					listHeadset.Items.Add(data.dll);
					if (data.enabled)
						listHeadset.SelectedItems.Add(data.dll);
				}
				if (data.state != PluginManagerBase.PluginData.PluginDataState.SKIPPED && data.state != PluginManagerBase.PluginData.PluginDataState.MISSING)
					loaded_plugins.Add(new loaded_plugin_info(data));
			}
			itemscontrolEnabledPlugins.ItemsSource = loaded_plugins;
		}
		private class loaded_plugin_info{
			private PluginManagerBase.PluginData plugin;
			public string dll { get { return plugin.dll; } }
			public string name { get { return plugin.plugin.ProviderName(); } }
			public bool error_exists { get { return ! String.IsNullOrEmpty(plugin.last_error); } }
			public bool has_options_button { get { return plugin.plugin.ShowOptionsButton(); } }
			public string error_msg { get { return plugin.last_error ?? ""; } }
			public void ShowPluginOptions(){
				plugin.plugin.EditOptions();
			}
			public loaded_plugin_info(PluginManagerBase.PluginData plugin){
				this.plugin = plugin;
			}
		}
		private void SetEnabled( IEnumerable<PluginManagerBase.PluginData> plugins, IEnumerable<String> enabled_dlls){
			foreach (PluginManagerBase.PluginData data in plugins)
				data.enabled = enabled_dlls.Contains(data.dll);
			
		}
		private string original_loaded_str;
		private void MakeLoadedStr(IEnumerable<PluginManagerBase.PluginData> plugins, ref String base_str) {
			if (String.IsNullOrWhiteSpace(base_str))
				base_str = "";

			foreach (PluginManagerBase.PluginData data in plugins)
					base_str += ":" + data.dll + "-" + data.enabled;
		}
		private void SaveSettings(){
			SetEnabled(contact_plugins, new string[] { comboContact.SelectedItem.ToString() });
			SetEnabled(headset_plugins, listHeadset.SelectedItems.Cast<string>());
			String now = "";
			MakeLoadedStr(contact_plugins, ref now);
			MakeLoadedStr(headset_plugins, ref now);
			if (now != original_loaded_str)
				MessageBox.Show("FSClient must be restarted before the newly enabled/disabled plugin changes will take effect", "Please restart FSClient",MessageBoxButton.OK,MessageBoxImage.Warning);
		}

		private void btn_PluginOptions_Click(object sender, RoutedEventArgs e){
			Button btn = sender as Button;
			if (btn == null || btn.DataContext == null)
				return;
			((loaded_plugin_info) btn.DataContext).ShowPluginOptions();
		}


	}
}
