using System.Windows;

namespace FSClient {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public App() {
			DispatcherUnhandledException += App_DispatcherUnhandledException;
			System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.High;

		}

		void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
			MessageBox.Show("Dispatcher exception of: " + e.Exception.Message, "Dispatcher Exception", MessageBoxButton.OK, MessageBoxImage.Error);
			e.Handled = true;
		}
	}
}
