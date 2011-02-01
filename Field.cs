using System;
using System.Collections.Generic;
using System.Linq;
namespace FSClient {
	public class SettingsField {
		public String name { get; set; }
		public String value { get; set; }
		public SettingsField() {
		}
		public SettingsField(FieldValue fv) {
			name = fv.field.name;
			value = fv.value;
		}
	}
	public class Field {
		public class FieldOption {
			public string display_value;
			public string value;
			public override string ToString() {
				return display_value;
			}
			public static FieldOption GetByValue(FieldOption[] options, String value) {
				return (from o in options where o.value == value select o).SingleOrDefault();
			}
		}
		public enum FIELD_TYPE { String, Int, Bool, Combo, MultiItem, Password };
		public FIELD_TYPE type;
		public delegate string validate_field_del(String value);
		public validate_field_del Validator;
		public static Field GetByName(Field[] fields, string name) {
			return (from f in fields where f.name == name select f).SingleOrDefault();
		}
		public Field(FIELD_TYPE type, string display_name, string name, string xml_name, string default_value, string category) {
			this.type = type;
			this.display_name = display_name;
			this.name = name;
			this.default_value = default_value;
			this.category = category;
			this.xml_name = xml_name;
		}
		public Field(FIELD_TYPE type, string display_name, string name, string xml_name, string default_value, string category, params string[] option_values) {
			this.type = type;
			this.display_name = display_name;
			this.name = name;
			this.default_value = default_value;
			this.category = category;
			this.xml_name = xml_name;
			foreach (string value in option_values)
				AddOption(value);
		}
		public Field(FIELD_TYPE type, string display_name, string name, string xml_name, string default_value, string category, params FieldOption[] option_values) {
			this.type = type;
			this.display_name = display_name;
			this.name = name;
			this.default_value = default_value;
			this.category = category;
			this.xml_name = xml_name;
			foreach (FieldOption value in option_values)
				_options.Add(value);
		}
		public FieldValue GetDefaultValue() {
			return new FieldValue { field = this, value = default_value };
		}
		public string display_name;
		public string name;
		public string xml_name;
		public string category;
		public string default_value;
		private List<FieldOption> _options = new List<FieldOption>();
		public FieldOption[] options {
			get {
				return _options.ToArray();
			}
		}
		public void AddOption(string value) {
			AddOption(value, value);
		}
		public void AddOption(string value, string display_value) {
			_options.Add(new FieldOption { display_value = display_value, value = value });
		}

	}
	public class FieldValue : ObservableClass {
		public Field field;

		public string value {
			get { return _value; }
			set {
				if (value == _value)
					return;
				_value = value;
				RaisePropertyChanged("value");
			}
		}
		private string _value;

		public override string ToString() {
			return value;
		}
		public static FieldValue[] FieldValues(Field[] fields) {
			FieldValue[] ret = (from f in fields select f.GetDefaultValue()).ToArray();
			return ret;
		}
		public static void SetValues(FieldValue[] values, params string[] name_value_pairs) {
			for (int i = 0; i < name_value_pairs.Length; i += 2)
				GetByName(values, name_value_pairs[i]).value = name_value_pairs[i + 1];
		}
		public static FieldValue GetByName(FieldValue[] values, string name) {
			return (from v in values where v.field.name == name select v).SingleOrDefault();
		}
		public static FieldValue GetByDisplayName(FieldValue[] values, string name) {
			return (from v in values where v.field.display_name == name select v).SingleOrDefault();
		}
	}
}
