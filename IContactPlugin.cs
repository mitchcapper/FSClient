using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FSClient.Controls;

namespace FSClient {
	public class ContactPluginManager : PluginManagerBase {

		public static ContactPluginManager GetPluginManager(SettingsPluginDataCollection settings){
			ContactPluginManager manager = new ContactPluginManager();
			manager.LoadSettings(settings);
			return manager;
		}
		public static IEnumerable<MenuItem> ContactMenuItems { get; set; }

		private List<ContactPluginData> plugins = new List<ContactPluginData>();
		private ContactPluginData active_plugin;
		private class ContactPluginData : PluginData {
			public ContactPluginData(PluginData data) : base(data){
				
			}
			public ContactPluginData(): base(){}
			public IContactPlugin contact_plugin {
				get { return plugin as IContactPlugin; }
				set { plugin = value; }
			}
		}
		public override IEnumerable<PluginData> GetPlugins() {
			return plugins;
		}
		protected override void SetPlugins(IEnumerable<PluginData> plugins){
			if (this.plugins.Count > 0)
				throw new Exception("Cannot call SetPlugins if plugins have already been loaded");
			foreach (PluginData data in plugins)
				this.plugins.Add(new ContactPluginData(data));

		}

		private void ContactInit(){
			ContactMenuItems = active_plugin.contact_plugin.ContactRightClickMenu();
			OurAutoCompleteBox box = Broker.get_instance().GetContactSearchBox();
			if (active_plugin.contact_plugin.HandleSearchBox(box))
				box.Visibility = Visibility.Visible;
			active_plugin.state = PluginData.PluginDataState.LOADED;
		}
		private void HandleError(ContactPluginData plugin, Exception e, PluginData.PluginDataState failed_state = PluginData.PluginDataState.DISABLED_ERROR) {
			Utils.PluginLog(PluginManagerName(), "Plugin \"" + plugin.contact_plugin.ProviderName() + "\" had an error Due to: " + e.Message);
			plugin.state = failed_state;
			plugin.last_error = e.Message;
			active_plugin = null;

		}
	
		public ContactPluginManager(){
			OurAutoCompleteBox box = Broker.get_instance().GetContactSearchBox();
			if (Application.Current == null)
				return;
			Application.Current.Dispatcher.BeginInvoke((Action)(() => {
				box.Visibility = Visibility.Collapsed;
			}));
		
		}

		private bool no_plugins_in_config;
		public override void LoadPlugins(){
			no_plugins_in_config = plugins.Count == 0;
			LoadActualPlugins("Contact",typeof (IContactPlugin), plugins);
			

			
			if (active_plugin != null){
				Call.calls.CollectionChanged += calls_CollectionChanged;
				Conference.users.CollectionChanged += confusers_CollectionChanged;
				Call.CallRightClickMenuShowing += calls_RightClickMenuShowing;
				Broker.get_instance().XFERMenuOpenedHandler += broker_xferMenuOpened;
			}
		}

		private void confusers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e){
			if (e.NewItems == null || active_plugin == null)
				return;
			bgresolve_worker_init();
			lock (pending_bg_queue.SyncRoot) {
				foreach (ConferenceUser u in e.NewItems) {
					if (bgResolveWorker.IsBusy || pending_bg_queue.Count > 0)
						pending_bg_queue.Enqueue(u);
					else
						bgResolveWorker.RunWorkerAsync(u);
				}

			}
		}
		public string GetDefaultXFERAppend() {
			return (active_plugin?.contact_plugin?.xfer_default_append) ?? "";
		}
		private void broker_xferMenuOpened(Call active_call, ContextMenu menu, List<MenuItem> items_to_add) {
			if (active_plugin == null)
				return;
			var to_add = active_plugin.contact_plugin.XFERMenu(active_call, menu);
			foreach (var itm in to_add)
				items_to_add.Add(itm);
			
		}

		protected override void HandlePluginLoadException(PluginData data, Exception e){
			base.HandlePluginLoadException(data, e);
			active_plugin = null;
		}
		protected override void HandlePluginLoadReflectionException(PluginData data, ReflectionTypeLoadException ex) {
			base.HandlePluginLoadReflectionException(data, ex);
			active_plugin = null;
		}
		public override string PluginManagerName(){
			return "Contact Plugin Manager";
		}

		protected override void PluginLoadAddPlugin(PluginData plugin){
			plugins.Add(plugin as ContactPluginData);
		}

		protected override void PluginLoadRegisterPlugin(PluginData plugin){
			ContactPluginData data = plugin as ContactPluginData;
			
			if (active_plugin != null)
				throw new Exception("Can only handle one contact plugin at a time right now and the current one is: " + active_plugin.plugin.ProviderName());
			try {
				data.contact_plugin.Initialize();
				active_plugin = data;
				Application.Current.Dispatcher.BeginInvoke((Action)ContactInit);
			} catch (Exception e) {
				HandleError(data, e, PluginData.PluginDataState.ERROR_LOADING);
			}
			
		}

		protected override PluginData NewPluginData(String dll){
			bool enabled = false;
			if (no_plugins_in_config && dll == "SimpleXmlContactPlugin.dll")
				enabled = true;
			return new ContactPluginData(){enabled = enabled};
		}

		private void calls_RightClickMenuShowing(object sender, Call.CallRightClickEventArgs e){
			if (active_plugin == null)
				return;
			var items = active_plugin.contact_plugin.CallRightClickMenu(e.call, e.menu);
			foreach (var item in items)
				e.menu.Items.Add(item);
		}


		private void calls_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { //We want to background resolve things incase a plugin does something that takes awhile
			if (e.NewItems == null || active_plugin == null)
				return;
			bgresolve_worker_init();
			lock (pending_bg_queue.SyncRoot) {
				foreach (Call c in e.NewItems) {
					if (bgResolveWorker.IsBusy || pending_bg_queue.Count > 0)
						pending_bg_queue.Enqueue(c);
					else
						bgResolveWorker.RunWorkerAsync(c);
				}

			}
		}
		#region background resolution
		private void bgresolve_worker_init() {
			if (bgResolveWorker == null) {
				bgResolveWorker = new BackgroundWorker();
				bgResolveWorker.DoWork += bgResolveWorker_DoWork;
				bgResolveWorker.RunWorkerCompleted += bgResolveWorker_RunWorkerCompleted;
			}
		}
		private void bgresolve_dequeue() {
			lock (pending_bg_queue.SyncRoot) {
				if (pending_bg_queue.Count > 0 && !bgResolveWorker.IsBusy)
					bgResolveWorker.RunWorkerAsync(pending_bg_queue.Dequeue());
			}
		}
		private void bgResolveWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			Application.Current.Dispatcher.BeginInvoke((Action)(bgresolve_dequeue));
		}

		private void bgResolveWorker_DoWork(object sender, DoWorkEventArgs e) {

			Call c = e.Argument as Call;
			if (c == null){
				ConferenceUser user = (ConferenceUser)e.Argument;
				active_plugin.contact_plugin.ResolveNumber(user.party_number, alias => { if (user.party_name == user.party_number) user.party_name = alias; });
				return;
			}

			active_plugin.contact_plugin.ResolveNumber(c.other_party_number, alias => { if (c.other_party_name == c.other_party_number) c.other_party_name = alias; });
		}

		private static Queue pending_bg_queue = new Queue();
		private static BackgroundWorker bgResolveWorker;
		#endregion

		
		~ContactPluginManager() {
			Dispose();
		}

		private bool disposed;
		public override void Dispose(){
			if (!disposed){
				disposed = true;
				foreach (ContactPluginData plugin in plugins){
					try{
						if (plugin.contact_plugin != null)
							plugin.contact_plugin.Terminate();
					}
					catch (Exception e){
						Utils.PluginLog(PluginManagerName(), "Error terminating a plugin: " + e.Message);
					}
				}
			}

			GC.SuppressFinalize(this);
		}
	}
	public abstract class IContactPlugin : IPlugin {
		public delegate void NumberResolved(String DisplayName);
		public abstract void ResolveNumber(String number, NumberResolved on_resolved);
		public abstract IEnumerable<MenuItem> CallRightClickMenu(Call call, ContextMenu parent_menu);
		public virtual IEnumerable<MenuItem> XFERMenu(Call call, ContextMenu parent_menu) { return null; }
		public virtual string xfer_default_append {get;protected set;}
		public abstract IEnumerable<MenuItem> ContactRightClickMenu();
		public abstract bool HandleSearchBox(OurAutoCompleteBox box);

	}
}
