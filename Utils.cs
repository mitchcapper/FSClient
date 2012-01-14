using System;
using System.Configuration;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Xml;
using FreeSWITCH.Native;
using System.IO;
using System.ComponentModel;
using System.Collections;


namespace FSClient {

	public class Utils {
		private static BackgroundWorker bgWorker;
		private static Timer bg_watcher;
		private static Api API;
		public delegate void ObjectEventHandler<T>(object sender, T data);
		public static string api_exec(String cmd, String args) {
			if (API == null)
				API = new Api();
			return API.Execute(cmd, args);
		}
		private class BGArgs {
			public string cmd;
			public string args;
		}
		private static Api BGAPI;
		private static BGArgs current_exec;
		private static Queue pending_bg_queue = new Queue();
		public static void bgapi_exec(String cmd, String args) {
			if (bgWorker == null) {
				bg_watcher = new Timer(30 * 1000);
				bg_watcher.Elapsed += new ElapsedEventHandler(bg_watcher_Elapsed);
				bgWorker = new BackgroundWorker();
				bgWorker.DoWork += bgWorker_DoWork;
				bgWorker.RunWorkerCompleted += bgWorker_RunWorkerCompleted;
				bg_watcher.Start();
			}
			BGArgs newBGArgs = new BGArgs { args = args, cmd = cmd };
			lock (pending_bg_queue.SyncRoot) {
				if (bgWorker.IsBusy || pending_bg_queue.Count > 0)
					pending_bg_queue.Enqueue(newBGArgs);
				else
					bgWorker.RunWorkerAsync(newBGArgs);
			}
		}
		private static BGArgs last_exec_check;
		static void bg_watcher_Elapsed(object sender, ElapsedEventArgs e) {
			if (current_exec != null && current_exec == last_exec_check)
				MessageBox.Show("Warning freeswitch is most likely deadlocked, something has been pending in the bg queue for > 30 seconds, currently executing: " + current_exec.cmd + " " + current_exec.args);
			last_exec_check = current_exec;
		}

		private static void bgapi_dequeue() {
			lock (pending_bg_queue.SyncRoot) {
				if (pending_bg_queue.Count > 0 && !bgWorker.IsBusy)
					bgWorker.RunWorkerAsync(pending_bg_queue.Dequeue());
			}
		}
		static void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			Application.Current.Dispatcher.BeginInvoke((Action)(bgapi_dequeue));
		}

		static void bgWorker_DoWork(object sender, DoWorkEventArgs e) {

			current_exec = (BGArgs)e.Argument;
			if (BGAPI == null)
				BGAPI = new Api();
			e.Result = BGAPI.Execute(current_exec.cmd, current_exec.args);
			current_exec = null;
		}

		public static void add_xml_param(XmlNode node, String name, String value) {
			XmlNode param_node = XmlUtils.AddNodeNode(node, "param");
			XmlUtils.AddNodeAttrib(param_node, "name", name);
			XmlUtils.AddNodeAttrib(param_node, "value", value);
		}
		public static string plugins_dir() {
			return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins");
		}
		private static TextWriter plugin_writer;
		public static void PluginLog(String source, String str) {
			if (plugin_writer == null) {
				try {
					plugin_writer = new StreamWriter(GetUserDataPath() + "\\plugins.log", false);

				} catch (Exception e) {
					MessageBox.Show("Unable to init plugins.log in plugins dir due to: " + e.Message);
					return;
				}
			}
			if (! String.IsNullOrWhiteSpace(str))
				plugin_writer.WriteLine(DateTime.Now + " - " + source + ": " + str);
			plugin_writer.Flush();
		}

		private static string _user_data_dir;
		public static string GetUserDataPath() {
			if (_user_data_dir == null) {
				Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
				FileInfo finfo = new FileInfo(config.FilePath);
				_user_data_dir = finfo.DirectoryName;
				if (!finfo.Exists) {
					DirectoryInfo dinfo = new DirectoryInfo(_user_data_dir);
					dinfo.Create();
				}
			}
			return _user_data_dir;
		}

#if DEBUG_LOG
		private static TextWriter writer;
#endif
		public static void DebugWrite(String str) {
#if DEBUG_LOG
			if (writer == null)
				writer = new StreamWriter(GetUserDataPath() + "\\debug.log");
			writer.WriteLine(str);
			writer.Flush();
#endif
		}
		public static void DebugEventDump(FSEvent evt) {
#if DEBUG_LOG_EVENTS
			String event_dump = evt.event_id.ToString();
			if (!String.IsNullOrEmpty(evt.subclass_name))
				event_dump += " " + evt.subclass_name;
			event_dump += ":\n";
			switch_event_header hdr = evt.first_header;
			while (hdr != null) {
				event_dump += "\t" + hdr.name + ": " + hdr.value + "\n";
				hdr = hdr.next;
			}
			DebugWrite(event_dump + "\n");
#endif
		}
	}
}
