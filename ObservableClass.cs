using System;
using System.ComponentModel;

namespace FSClient {
	public class ObservableClass : INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		protected void RaisePropertyChanged(string name) {
			VerifyProperty(name);
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
		[System.Diagnostics.Conditional("DEBUG")]
		private void VerifyProperty(string propertyName) {
			Type type = GetType();
			System.Reflection.PropertyInfo pi = type.GetProperty(propertyName);
			if (pi == null) {
				string msg = "OnPropertyChanged was invoked with invalid property name {0}. {0} is not a public property of {1}.";
				msg = String.Format(msg, propertyName, type.FullName);
				System.Diagnostics.Debug.Fail(msg);
			}
		}

	}
}
