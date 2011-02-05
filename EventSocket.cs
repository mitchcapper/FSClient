using System;
using System.Linq;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace FSClient {
	
		[XmlRoot("settingsEventSocket")]
		public class SettingsEventSocket {
			public SettingsField[] fields { get; set; }
			public EventSocket GetEventSocket() {
				EventSocket socket = new EventSocket();
				foreach (SettingsField field in fields) {
					FieldValue val = FieldValue.GetByName(socket.values, field.name);
					if (val != null)
						val.value = field.value;
				}
				return socket;
			}
			public SettingsEventSocket() {
			}
			public SettingsEventSocket(EventSocket socket) {
				fields = (from fv in socket.values select new SettingsField(fv)).ToArray();
			}
		}
	public class EventSocket{
		public static Field[] fields = {
		                               	new	Field(Field.FIELD_TYPE.String, "Listen IP","listen-ip","listen-ip","127.0.0.1",""),
										new Field(Field.FIELD_TYPE.Int,"Listen Port","listen-port","listen-port","8022",""),
										new Field(Field.FIELD_TYPE.String,"Password","password","password","ClueCon",""),
										new Field(Field.FIELD_TYPE.String,"Inbound ACL","apply-inbound-acl","apply-inbound-acl","",""),
										new Field(Field.FIELD_TYPE.Bool,"Nat Map","nat-map","nat-map","false",""),
										new Field(Field.FIELD_TYPE.Combo,"Debug Mode","debug","debug","0","",new Field.FieldOption{display_value="true", value="1"},new Field.FieldOption{display_value="false", value="0"}),
	};
		public FieldValue[] values = FieldValue.FieldValues(fields);
		public void gen_config(XmlNode config_node){
			XmlNode settings = XmlUtils.AddNodeNode(config_node, "settings");
			foreach (FieldValue value in values) {
				if (!String.IsNullOrEmpty(value.field.xml_name))
					Utils.add_xml_param(settings, value.field.xml_name, value.value);
			}
		}
		public void reload_config(){
			Utils.bgapi_exec("reload", "mod_event_socket");
			
		}
		public void edit() {
			GenericEditor editor = new GenericEditor();
			editor.Init("Editing Event Socket Settings", values);
			editor.ShowDialog();
			if (editor.DialogResult == true){
				MessageBoxResult mres = MessageBox.Show("Do you want to reload the event socket settings now, doing so will drop anyone connected to the event socket?", "Reload Module Warning", MessageBoxButton.YesNo);
				if (mres != MessageBoxResult.Yes)
					return;
				reload_config();
			}
		}
	}
}
