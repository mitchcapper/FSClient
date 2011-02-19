using System;
using System.Linq;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace FSClient {
	[XmlRoot("settingsSofia")]
	public class SettingsSofia {
		public SettingsField[] fields { get; set; }
		public Sofia GetSofia() {
			Sofia sofia = new Sofia();
			foreach (SettingsField field in fields) {
				FieldValue val = FieldValue.GetByName(sofia.values, field.name);
				if (val != null)
					val.value = field.value;
			}
			return sofia;
		}
		public SettingsSofia() {
		}
		public SettingsSofia(Sofia sofia) {
			fields = (from fv in sofia.values select new SettingsField(fv)).ToArray();
		}
	}

	public class Sofia {
		public static Field[] fields = {

										   /*Default*/
											new Field(Field.FIELD_TYPE.MultiItem,"Codec Preferences","codec-prefs","codec-prefs","CELT@48000h@10i,PCMU,PCMA,GSM","","CELT@32000h","CELT@48000h@10i","PCMA","PCMU","GSM","G722","G7221@16000h","G7221@32000h","AAL2-G726-16","AAL2-G726-24","AAL2-G726-32","AAL2-G726-40","BV16","BV32","DVI4@16000h@40i","DVI4@8000h@20i","G726-16","G726-24","G726-32","G726-40","L16","LPC","iLBC@30i","speex@16000h@20i","speex@32000h@20i","speex@8000h@20i"),
											new Field(Field.FIELD_TYPE.Combo,"Inbound Codec Negotiation","inbound-codec-negotiation","inbound-codec-negotiation","generous","","generous","greedy","scrooge"),
											new Field(Field.FIELD_TYPE.String,"External RTP IP","ext-rtp-ip","ext-rtp-ip","stun:stun.freeswitch.org",""),
											new Field(Field.FIELD_TYPE.String,"External SIP IP","ext-sip-ip","ext-sip-ip","stun:stun.freeswitch.org",""),
											new Field(Field.FIELD_TYPE.String,"RTP IP","rtp-ip","rtp-ip","auto",""),
											new Field(Field.FIELD_TYPE.String,"SIP IP","sip-ip","sip-ip","auto",""),
											new Field(Field.FIELD_TYPE.String,"Hold Music","hold-music","hold-music","local_stream://moh",""),
											new Field(Field.FIELD_TYPE.String,"User Agent","user-agent-string","user-agent-string","FreeSWITCH/FSClient",""),
											new Field(Field.FIELD_TYPE.Bool,"SIP Trace","sip-trace","sip-trace","false",""),
											new Field(Field.FIELD_TYPE.Combo,"Debug Level","debug","debug","0","","0","1","2","3","4","5","6","7","8","9"),
										   
											/*NAT*/
											new	Field(Field.FIELD_TYPE.Combo,"Apply Nat ACL","apply-nat-acl","apply-nat-acl","rfc1918","NAT",new Field.FieldOption{display_value="rfc1918", value="rfc1918"},new Field.FieldOption{display_value="None", value=""}),
											new Field(Field.FIELD_TYPE.Bool,"Agressive Nat Detection","aggressive-nat-detection","aggressive-nat-detection","true","NAT"),
                                            new Field(Field.FIELD_TYPE.Combo, "Force RPort", "NDLB-force-rport", "NDLB-force-rport", "false", "NAT", "false","safe","true"), 

											/*Security*/
											new Field(Field.FIELD_TYPE.Bool,"TLS","tls","tls","false","Security"),
											new Field(Field.FIELD_TYPE.Combo,"TLS Verify Policy","tls-verify-policy","tls-verify-policy","subjects_out","Security",new Field.FieldOption{display_value="None", value=""},new Field.FieldOption{display_value="Outbound Certificates", value="out"},new Field.FieldOption{display_value="Outbound Certs & Hostnames", value="subjects_out"}),
											new Field(Field.FIELD_TYPE.Combo,"TLS Version","tls-version","tls-version","sslv23","Security","sslv23","tlsv1"),
											new Field(Field.FIELD_TYPE.String,"TLS Bind Params","tls-bind-params","tls-bind-params","transport=tls","Security"),
											new Field(Field.FIELD_TYPE.Int,"TLS SIP Port","tls-sip-port","tls-sip-port","12347","Security"),
											new Field(Field.FIELD_TYPE.String,"TLS Certificate Directory","tls-cert-dir","tls-cert-dir","conf/ssl","Security"),
											new Field(Field.FIELD_TYPE.Int,"TLS Max Verify Depth","tls-verify-depth","tls-verify-depth","2","Security"),
											new Field(Field.FIELD_TYPE.Bool,"TLS No Certificate Date Validation","tls-no-verify-date","tls-no-verify-date","false","Security"),

											/*Advanced*/
											new Field(Field.FIELD_TYPE.String,"Challenge Realm","challenge-realm","challenge-realm","auto_from","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"SIP Port","sip-port","sip-port","12346","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"DTMF Duration","dtmf-duration","dtmf-duration","2000","Advanced"),
											new Field(Field.FIELD_TYPE.Bool,"STUN Enabled","stun-enabled","stun-enabled","true","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"Jitter Buffer","auto-jitterbuffer-msec","auto-jitterbuffer-msec","60","Advanced"),

											/*Advanced Less Important*/
											new Field(Field.FIELD_TYPE.Int,"Max Proceeding","max-proceeding","max-proceeding","3","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"Sip Auth Nonce TTL","nonce-ttl","nonce-ttl","60","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"rfc2833-pt","rfc2833-pt","rfc2833-pt","101","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"RTP Hold Timeout Seconds","rtp-hold-timeout-sec","rtp-hold-timeout-sec","1800","Advanced"),
											new Field(Field.FIELD_TYPE.Int,"RTP Tiemout Seconds","rtp-timeout-sec","rtp-timeout-sec","300","Advanced"),
											new Field(Field.FIELD_TYPE.String,"RTP Timer Name","rtp-timer-name","rtp-timer-name","soft","Advanced"),



									   };
		public FieldValue[] values = FieldValue.FieldValues(fields);
		public void gen_config(XmlNode config_node) {

			XmlNode global_settings = XmlUtils.AddNodeNode(config_node, "global_settings");
			Utils.add_xml_param(global_settings, "auto-restart", "true");
			Utils.add_xml_param(global_settings, "log-level", "0");
			XmlNode profiles = XmlUtils.AddNodeNode(config_node, "profiles");
			XmlNode profile = XmlUtils.AddNodeNode(profiles, "profile");
			XmlUtils.AddNodeAttrib(profile, "name", "softphone");
			XmlNode gateways = XmlUtils.AddNodeNode(profile, "gateways");
			Account.create_gateway_nodes(gateways,FieldValue.GetByName(values, "tls").value == "true");
			XmlNode settings = XmlUtils.AddNodeNode(profile, "settings");

			Utils.add_xml_param(settings, "context", "public");
			Utils.add_xml_param(settings, "dialplan", "xml");
			Utils.add_xml_param(settings, "disable-register", "true");
			foreach (FieldValue value in values) {
				if (!String.IsNullOrEmpty(value.field.xml_name)){
					String param_value = value.value;
					if (value.field.name == "tls" && value.value == "true") {//lets make sure that we have a cafile.pem
						
						String base_dir = FieldValue.GetByName(values, "tls-cert-dir").value;
						if (String.IsNullOrWhiteSpace(base_dir))
							base_dir = "conf/ssl";
								//this is what freeswitch uses by default if its empty, if this changes this code needs to be updated
						base_dir = base_dir.Replace('/', '\\'); //windows file path
						if (base_dir[base_dir.Length - 1] != '\\')
							base_dir += '\\';
						if (!System.IO.File.Exists(base_dir + "cafile.pem")){
							MessageBox.Show("Your sofia settings have TLS enabled however you do not have a cafile.pem in your cert folder, this will most likely cause the entire softphone profile not to load so I am disabling TLS in the profile for now");
							param_value = "false";
						}

					}
					Utils.add_xml_param(settings, value.field.xml_name, param_value);
				}
			}

			DelayedFunction.DelayedCall("SofiaProfileCheck", sofia_profile_check, 1200);
		}

		public enum RELOAD_CONFIG_MODE {
			SOFT,
			HARD,
			MODULE
		} ;
		public void reload_config(RELOAD_CONFIG_MODE mode) {
			switch (mode) {
				case RELOAD_CONFIG_MODE.SOFT:
					Utils.api_exec("sofia", "profile softphone rescan reloadxml");
					break;
				case RELOAD_CONFIG_MODE.HARD:
					Utils.api_exec("sofia", "profile softphone restart reloadxml");
					break;
				case RELOAD_CONFIG_MODE.MODULE:
					Utils.api_exec("reload", "mod_sofia");
					break;
			}
		}

		private bool master_profile_ok;
		public void sofia_profile_check() {
			master_profile_ok = true;
			String res = Utils.api_exec("sofia", "status profile softphone");
			if (res.Trim() == "Invalid Profile!") {
				MessageBox.Show("Warning the master sofia profile was not able to load and the phone will most likely _not_ work, make sure the local bind port (" + FieldValue.GetByName(values, "sip-port").value + ") is free(set under the Advanced tab of in the sofia settings) and FSClient is allowed through your firewall, otherwise check the freeswitch.log for more details.  You can try reloading the sofia profile by editing the sofia settings and clicking save to see if fixed.");
				master_profile_ok = false;
			}

		}
		public void edit() {
			if (Broker.get_instance().active_calls != 0) {
				MessageBoxResult mres = MessageBox.Show("Warning editing sofia settings will cause sofia to restart and will drop any active calls, do you want to continue?", "Restart Warning", MessageBoxButton.YesNo);
				if (mres != MessageBoxResult.Yes)
					return;
			}
			GenericEditor editor = new GenericEditor();
			editor.Init("Editing Sofia Settings", values);
			editor.ShowDialog();
			if (editor.DialogResult == true) {
				if (master_profile_ok)
					reload_config(RELOAD_CONFIG_MODE.HARD);
				else
					reload_config(RELOAD_CONFIG_MODE.MODULE);
			}
		}
	}
}

