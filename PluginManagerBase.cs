using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace FSClient {
	[XmlRoot("SettingsPluginDataCollection")]
	public class SettingsPluginDataCollection {
		public SettingsPluginData[] data { get; set; }
		[XmlRoot("SettingsPluginData")]
		public class SettingsPluginData {
			public string dll { get; set; }
			public bool enabled { get; set; }
			public SettingsPluginData() { }
			public SettingsPluginData(PluginManagerBase.PluginData data) {
				dll = data.dll;
				enabled = data.enabled;
			}
			public PluginManagerBase.PluginData GetPluginData() {
				return new PluginManagerBase.PluginData() { dll = dll, enabled = enabled };
			}
		}
		public SettingsPluginDataCollection() {
		}
		public SettingsPluginDataCollection(IEnumerable<PluginManagerBase.PluginData> plugins) {
			data = (from p in plugins select new SettingsPluginData(p)).ToArray();
		}
	}

	public abstract class PluginManagerBase : IDisposable {
		public class PluginData {
			public string dll { get; set; }
			public bool enabled { get; set; }
			public enum PluginDataState { MISSING, SKIPPED, ERROR_LOADING, LOADED, DISABLED_ERROR };
			public PluginDataState state;
			public string last_error;
			[System.Xml.Serialization.XmlIgnoreAttribute]
			public virtual IPlugin plugin { get; set; }
			public PluginData() {
				enabled = true;
				state = PluginDataState.MISSING;
			}
			public PluginData(PluginData data) {
				dll = data.dll;
				enabled = data.enabled;
				state = data.state;
				plugin = data.plugin;
				last_error = data.last_error;
			}
		}
		protected virtual void LoadSettings(SettingsPluginDataCollection settings) {
			SetPlugins(settings.data.Select(settings_data => settings_data.GetPluginData()).ToArray());
		}

		public abstract void LoadPlugins();
		private static List<PossiblePlugin> possible_plugins;
		private class PossiblePlugin {
			private IEnumerable<Type> _types;
			public IEnumerable<Type> types {
				get {
					if (!loaded_types)
						LoadTypes();
					return _types;
				}

			}
			public PossiblePlugin(String full_dll) {
				this.full_dll = full_dll;
				file_info = new FileInfo(full_dll);
			}
			private string full_dll { get; set; }
			public FileInfo file_info;
			private Assembly _asm;
			private bool loaded_types = false;
			public Assembly asm {
				get {
					if (!loaded_types)
						LoadTypes();
					return _asm;
				}
			}
			private void LoadTypes() {
				loaded_types = true;
				_asm = Assembly.LoadFrom(full_dll);
				if (asm == null)
					return;
				_types = asm.GetTypes();
			}
		}
		private static void PluginScan() {
			if (possible_plugins != null)
				return;
			possible_plugins = new List<PossiblePlugin>();

			String plugin_dir = Utils.plugins_dir();
			string[] dlls;
			try {
				dlls = Directory.GetFileSystemEntries(plugin_dir, "*.dll");
				foreach (String full_dll in dlls)
					possible_plugins.Add(new PossiblePlugin(full_dll));
			}
			catch (DirectoryNotFoundException) {
				return;
			}
		}
		protected void LoadActualPlugins(Type plugin_type, IEnumerable<PluginData> plugins) {
			PluginScan();
			foreach (PossiblePlugin pos_plug in possible_plugins) {
				String dll = pos_plug.file_info.Name;

				PluginData data = (from p in plugins where p.dll == dll select p).SingleOrDefault();
				bool add_to_list = false;
				if (data == null) {
					add_to_list = true;
					data = NewPluginData(dll);
					data.dll = dll;
				}
				if (data.enabled == false && !add_to_list) { //if not yet on the list we have to continue to see if we should be on the list
					data.state = PluginData.PluginDataState.SKIPPED;
					continue;
				}
				try {
					Assembly asm = pos_plug.asm;
					if (asm == null || pos_plug.types == null)
						continue;
					foreach (Type type in pos_plug.types) {
						if (type.IsAbstract)
							continue;
						if (!IsTypeOf(type, plugin_type))
							continue;
						if (add_to_list)
							PluginLoadAddPlugin(data);
						if (data.enabled == false) {
							data.state = PluginData.PluginDataState.SKIPPED;
							continue;
						}
						data.plugin = asm.CreateInstance(type.FullName) as IPlugin;
						PluginLoadRegisterPlugin(data);
					}
				}
				catch (ReflectionTypeLoadException ex) {
					HandlePluginLoadReflectionException(data, ex);
				}
				catch (Exception e) {
					HandlePluginLoadException(data, e);
				}
			}
		}

		public abstract string PluginManagerName();
		protected abstract void PluginLoadAddPlugin(PluginData plugin);
		protected abstract void PluginLoadRegisterPlugin(PluginData plugin);
		protected abstract PluginData NewPluginData(String dll);
		protected virtual void HandlePluginLoadReflectionException(PluginData data, ReflectionTypeLoadException ex) {
			String err = "Error creating plugin from dll \"" + data.dll + "\" due to a loader exception error was:\n";
			foreach (Exception e in ex.LoaderExceptions)
				err += e.Message + "\n";
			data.last_error = err;
			data.state = PluginData.PluginDataState.ERROR_LOADING;
			Utils.PluginLog(PluginManagerName(), err);
		}
		protected virtual void HandlePluginLoadException(PluginData data, Exception e) {
			String err = "Error creating plugin from dll \"" + data.dll + "\" of: " + e.Message;
			data.last_error = err;
			data.state = PluginData.PluginDataState.ERROR_LOADING;
			Utils.PluginLog(PluginManagerName(), err);
		}
		protected bool IsTypeOf(Type to_check, Type of) {
			if (to_check == null)
				return false;
			if (to_check == of)
				return true;
			return IsTypeOf(to_check.BaseType, of);
		}
		public virtual SettingsPluginDataCollection GetSettings() {
			return new SettingsPluginDataCollection(GetPlugins());
		}
		public abstract IEnumerable<PluginData> GetPlugins();
		protected abstract void SetPlugins(IEnumerable<PluginData> plugins);

		public virtual void SetPluginEnabled(bool enabled, PluginData plugin) {
			plugin.enabled = enabled;
		}
		public abstract void Dispose();
	}
}
