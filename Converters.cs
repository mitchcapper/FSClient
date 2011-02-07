using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FSClient {
	public class StateConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {

			if (value == null)
				return Colors.White.ToString();
			String ret = Colors.White.ToString();
			Call.CALL_STATE state = (Call.CALL_STATE)value;
			switch (state) {
				case Call.CALL_STATE.Answered:
					ret = "#FF4EFF00";
					break;
				case Call.CALL_STATE.Busy:
					ret = "#FFFFAF00";
					break;
				case Call.CALL_STATE.Ended:
					ret = "#FFAFAFAF";
					break;
				case Call.CALL_STATE.Failed:
					ret = "#FF000000";
					break;
				case Call.CALL_STATE.Hold:
					ret = "#FFF1FF00";
					break;
				case Call.CALL_STATE.Missed:
					ret = "#FFFF0000";
					break;
				case Call.CALL_STATE.Hold_Ringing:
				case Call.CALL_STATE.Ringing:
					ret = "#FF00B3FF";
					break;
			}
			return ret;
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
	public class AccountDefaultConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return Colors.White.ToString();
			bool is_default = (bool)value;
			return is_default ? "#FF00FFE2" : Colors.White.ToString();
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class EnglishDirectionConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return "";
			bool is_outgoing = (bool)value;
			return is_outgoing ? "Outgoing" : "Incoming";
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
	public class DirectionConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return "";
			bool is_outgoing = (bool)value;
			return is_outgoing ? "<-" : "->";
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
	public class DurationTimeConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return "";
			TimeSpan duration = (TimeSpan)value;
			return duration.Minutes + ":" + duration.Seconds.ToString("00");
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
	public class ShortDateTimeConverter : IValueConverter {
		public static string Convert(DateTime time) {
			if (time == DateTime.MinValue)
				return "";
			return time.ToString("ddd, dd MMM yyyy HH:mm:ss tt");
		}
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return "";
			return Convert((DateTime)value);

		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
	public class BoolToVisibilityConverter : IValueConverter {
		public static Visibility Convert(bool value) {
			 return value ? Visibility.Visible : Visibility.Hidden;
		}
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null)
				return "";
			return Convert((bool)value);

		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
