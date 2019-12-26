using System;
using System.Runtime.InteropServices;
/*
This class taken directly from keepass slimmed downed and slightly modified: 
https://github.com/dlech/KeePass2.x/blob/9b57541e6fcb49cb5f12029fb8e553295cf153c4/KeePass/UI/DpiUtil.cs
	KeePass Password Safe - The Open-Source Password Manager
	Copyright (C) 2003-2019 Dominik Reichl <dominik.reichl@t-online.de>
*/

namespace FSClient {

	public static class DpiUtil {
		internal const int LOGPIXELSX = 88;
		internal const int LOGPIXELSY = 90;
		[DllImport("User32.dll")]
		internal static extern IntPtr GetDC(IntPtr hWnd);
		[DllImport("Gdi32.dll")]
		internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
		[DllImport("User32.dll")]
		internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		public static int ScaleIntX(double i) {
			EnsureInitialized();
			return (int)Math.Round((double)i * m_dScaleX);
		}

		public static int ScaleIntY(double i) {
			EnsureInitialized();
			return (int)Math.Round((double)i * m_dScaleY);
		}
		public static int DeScaleIntX(double i) {
			EnsureInitialized();
			return (int)Math.Round((double)i / m_dScaleX);
		}

		public static int DeScaleIntY(double i) {
			EnsureInitialized();
			return (int)Math.Round((double)i / m_dScaleY);
		}
		private static void EnsureInitialized() {
			if (m_bInitialized) return;

			try {
				IntPtr hDC = GetDC(IntPtr.Zero);
				if (hDC != IntPtr.Zero) {
					m_nDpiX = GetDeviceCaps(hDC,
						LOGPIXELSX);
					m_nDpiY = GetDeviceCaps(hDC,
						LOGPIXELSY);
					if ((m_nDpiX <= 0) || (m_nDpiY <= 0)) {
						System.Diagnostics.Debug.Assert(false);
						m_nDpiX = StdDpi;
						m_nDpiY = StdDpi;
					}

					if (ReleaseDC(IntPtr.Zero, hDC) != 1) {
						System.Diagnostics.Debug.Assert(false);
					}
				} else { System.Diagnostics.Debug.Assert(false); }
			} catch (Exception) { System.Diagnostics.Debug.Assert(false); }

			m_dScaleX = (double)m_nDpiX / (double)StdDpi;
			m_dScaleY = (double)m_nDpiY / (double)StdDpi;

			m_bInitialized = true;
		}
		private const int StdDpi = 96;

		private static bool m_bInitialized = false;

		private static int m_nDpiX = StdDpi;
		private static int m_nDpiY = StdDpi;

		private static double m_dScaleX = 1.0;
		public static double FactorX {
			get {
				EnsureInitialized();
				return m_dScaleX;
			}
		}

		private static double m_dScaleY = 1.0;
		public static double FactorY {
			get {
				EnsureInitialized();
				return m_dScaleY;
			}
		}

	}

}