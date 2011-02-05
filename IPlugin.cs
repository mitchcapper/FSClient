using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FSClient {
	public abstract class IPlugin {
		public abstract string ProviderName();
		public abstract void Initialize();
		public abstract void Terminate();
		public virtual bool ShowOptionsButton() {
			return false;
		}
		public virtual void EditOptions() { }
	}
}
