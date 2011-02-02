using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using FSClient.Controls;

namespace FSClient {
	public class ContactPluginManager : IDisposable {
		private bool IsTypeOf(Type to_check, Type of) {
			if (to_check == null)
				return false;
			if (to_check == of)
				return true;
			return IsTypeOf(to_check.BaseType, of);
		}

		public static IEnumerable<MenuItem> ContactMenuItems { get; set; }

		private List<IContactPlugin> plugins = new List<IContactPlugin>();
		public void RegisterPlugin(IContactPlugin plugin) {
			if (plugins.Count > 0)
				throw new Exception("Can only handle one contact plugin at a time right now and the current one is: " + plugins[0].ProviderName());
			try {
				plugins.Add(plugin);
				plugin.Initialize();
				Application.Current.Dispatcher.BeginInvoke((Action)ContactInit);
				

			} catch (Exception e) {
				HandleError(plugin, e);
			}
		}
		private void ContactInit(){
			IContactPlugin plugin = plugins[0];
			ContactMenuItems = plugin.ContactRightClickMenu();
			OurAutoCompleteBox box = Broker.get_instance().GetContactSearchBox();
			if (plugin.HandleSearchBox(box))
				box.Visibility = Visibility.Visible;
		}
		private void HandleError(IContactPlugin plugin, Exception e) {
			Utils.PluginLog("Contact Plugin Manager", "Plugin \"" + plugin.ProviderName() + "\" had an error Due to: " + e.Message);

		}
	
		public ContactPluginManager() {
			OurAutoCompleteBox box = Broker.get_instance().GetContactSearchBox();
			Application.Current.Dispatcher.BeginInvoke((Action) (() => {
			                                                     	box.Visibility = Visibility.Collapsed;
			                                                     }));
			
			

			String plugin_dir = Utils.plugins_dir();
			string[] dlls;
			try {
				dlls = Directory.GetFileSystemEntries(plugin_dir, "*.dll");
			} catch (DirectoryNotFoundException) {
				return;
			}
			foreach (String dll in dlls) {
				try {
					Assembly asm = Assembly.LoadFrom(dll);
					if (asm == null)
						continue;
					foreach (Type type in asm.GetTypes()) {
						if (type.IsAbstract)
							continue;
						if (!IsTypeOf(type, typeof(IContactPlugin)))
							continue;

						RegisterPlugin(asm.CreateInstance(type.FullName) as IContactPlugin);
					}
				} catch (ReflectionTypeLoadException ex) {
					String err = "Error creating contact plugin from dll \"" + dll + "\" due to a loader exception, make sure you have the headset runtime/api/sdk installed error was:\n";
					foreach (Exception e in ex.LoaderExceptions)
						err += e.Message + "\n";
					Utils.PluginLog("Contact Plugin Manager", err);
				} catch (Exception e) {
					Utils.PluginLog("Contact Plugin Manager", "Error creating contact plugin from dll \"" + dll + "\" of: " + e.Message);
				}
			}
			Call.calls.CollectionChanged += calls_CollectionChanged;
			Call.CallRightClickMenuShowing += calls_RightClickMenuShowing;
		}

		private void calls_RightClickMenuShowing(object sender, Call.CallRightClickEventArgs e){
			if (plugins.Count == 0)
				return;
			plugins[0].CallRightClickMenu(e.call, e.menu);
		}


		private void calls_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { //We want to background resolve things incase a plugin does something that takes awhile
			if (e.NewItems == null || plugins.Count != 1)
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

			Call c = (Call)e.Argument;
			plugins[0].ResolveNumber(c.other_party_number, alias => { if (c.other_party_name == c.other_party_number) c.other_party_name = alias; });
		}

		private static Queue pending_bg_queue = new Queue();
		private static BackgroundWorker bgResolveWorker;
		#endregion

		
		~ContactPluginManager() {
			Dispose();
		}

		private bool disposed;
		public void Dispose(){
			if (!disposed){
				disposed = true;
				foreach (IContactPlugin plugin in plugins){
					try{
						plugin.Terminate();
					}
					catch (Exception e){
						Utils.PluginLog("Contact Plugin Manager", "Error terminating a plugin: " + e.Message);
					}
				}
			}

			GC.SuppressFinalize(this);
		}
	}
	public abstract class IContactPlugin {
		public delegate void NumberResolved(String DisplayName);

		public abstract void ResolveNumber(String number, NumberResolved on_resolved);
		public abstract void CallRightClickMenu(Call call, ContextMenu menu);
		public abstract IEnumerable<MenuItem> ContactRightClickMenu();
		public abstract void Initialize();
		public abstract void Terminate();
		public abstract string ProviderName();
		public abstract bool HandleSearchBox(OurAutoCompleteBox box);

	}
}
