
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;


namespace UnManaged {
#if ! HOTKEY_PROXY
[Flags]
	public enum KeyModifier {
		None = 0x0000,
		Alt = 0x0001,
		Ctrl = 0x0002,
		NoRepeat = 0x4000,
		Shift = 0x0004,
		Win = 0x0008
	}
#else
	using GlobalHotkeyLib;
#endif

	public class GlobalHotKey : IDisposable {
		private static Dictionary<int, GlobalHotKey> _dictHotKeyToCalBackProc;

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, UInt32 fsModifiers, UInt32 vlc);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		public const int WmHotKey = 0x0312;

		private bool _disposed = false;
		public static Key ParseStringToKey(string key) {
			if (key[0] >= 0 && key[0] <= 9)
				key = "D" + key;
			Enum.TryParse<System.Windows.Input.Key>(key, out var hot_key);
			return hot_key;
		}

		public Key Key { get; private set; }
		public KeyModifier KeyModifiers { get; private set; }
		public Action<GlobalHotKey> Action { get; set; }
		public int Id { get; set; }

		// ******************************************************************
		public GlobalHotKey(Key k, KeyModifier keyModifiers, Action<GlobalHotKey> action, bool register = true) {
			Key = k;
			KeyModifiers = keyModifiers;
			Action = action;
			if (register)
				Register();
			
		}
		public GlobalHotKey() { }
		private static object lock_obj = new object();
		public void UpdateHotKey(Key k, KeyModifier keyModifiers, bool register = true) {
			Key = k;
			KeyModifiers = keyModifiers;
			if (register)
				Register();
		}

		// ******************************************************************
		public bool Register() {
			if (Id != 0)
				Unregister();
			int virtualKeyCode = KeyInterop.VirtualKeyFromKey(Key);
			Id = virtualKeyCode + ((int)KeyModifiers * 0x10000);
			bool result = RegisterHotKey(IntPtr.Zero, Id, (UInt32)KeyModifiers, (UInt32)virtualKeyCode);
			lock (lock_obj) {
				if (_dictHotKeyToCalBackProc == null) {
					_dictHotKeyToCalBackProc = new Dictionary<int, GlobalHotKey>();
					ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);
				}
			}

			_dictHotKeyToCalBackProc.Add(Id, this);
			if (!result)
				throw new Exception("Unable to register hot key");
			//Debug.Print(result.ToString() + ", " + Id + ", " + virtualKeyCode);
			return result;
		}

		// ******************************************************************
		public void Unregister() {
			if (Id == 0)
				return;
			UnregisterHotKey(IntPtr.Zero, Id);
			_dictHotKeyToCalBackProc.Remove(Id);
			Id = 0;
		}

		// ******************************************************************
		private static void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled) {
			if (!handled) {
				if (msg.message == WmHotKey) {
					GlobalHotKey hotKey;

					if (_dictHotKeyToCalBackProc.TryGetValue((int)msg.wParam, out hotKey)) {
						if (hotKey.Action != null) {
							Application.Current.Dispatcher.BeginInvoke((Action)(()=>
								hotKey.Action.Invoke(hotKey)
							));
						}
						handled = true;
					}
				}
			}
		}

		// ******************************************************************
		// Implement IDisposable.
		// Do not make this method virtual.
		// A derived class should not be able to override this method.
		public void Dispose() {
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		// ******************************************************************
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be _disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be _disposed.
		protected virtual void Dispose(bool disposing) {
			// Check to see if Dispose has already been called.
			if (!this._disposed) {
				// If disposing equals true, dispose all managed
				// and unmanaged resources.
				if (disposing) {
					// Dispose managed resources.
					Unregister();
				}

				// Note disposing has been done.
				_disposed = true;
			}
		}
	}

	// ******************************************************************
	

	// ******************************************************************
}
