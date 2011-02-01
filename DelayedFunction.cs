using System;
using System.Collections.Generic;
using System.Timers;

namespace FSClient {
	public class DelayedFunction {
		private class DelayedItem {
			public String key;
			public Action action;
			public DateTime end_time;
		}
		private static List<DelayedItem> functions = new List<DelayedItem>();
		private static Timer timer;
		private static bool timer_enabled;
		public static void DelayedCall(String key, Action action, int ms) {
			if (timer == null) {
				timer = new Timer();
				timer.Interval = 200;
				timer.Elapsed += timer_Elapsed;
			}
			lock (functions) {
				for (int i = 0; i < functions.Count; i++) {
					if (functions[i].key == key) {
						functions.RemoveAt(i);
						break;
					}
				}
				functions.Add(new DelayedItem { key = key, action = action, end_time = DateTime.Now.AddMilliseconds(ms) });
				if (!timer_enabled)
					timer_enabled = timer.Enabled = true;
			}
		}

		static void timer_Elapsed(object sender, ElapsedEventArgs e) {
			timer.Enabled = false;
			DateTime current = DateTime.Now;
			List<Action> to_perform = new List<Action>();

			lock (functions) {
				for (int i = 0; i < functions.Count; i++) {
					if (functions[i].end_time > current)
						continue;
					to_perform.Add(functions[i].action);
					functions.RemoveAt(i);
					i--;
				}
			}
			foreach (Action action in to_perform)
				action();

			lock (functions) {
				timer_enabled = timer.Enabled = (functions.Count != 0);
			}
		}
	}
}
