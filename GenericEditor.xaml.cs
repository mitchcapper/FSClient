using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace FSClient {
	/// <summary>
	/// Interaction logic for GenericEditor.xaml
	/// </summary>
	public partial class GenericEditor : Window {
		public GenericEditor() {
			this.InitializeComponent();

			// Insert code required on object creation below this point.
		}
		private Dictionary<FieldValue, UIElement> elements_to_ui = new Dictionary<FieldValue, UIElement>();
		private Dictionary<FieldValue, TabItem> element_to_page = new Dictionary<FieldValue, TabItem>();
		public void Init(String title, IEnumerable<FieldValue> values) {
			txtTitle.Text = title;
			Title = title;
			Dictionary<string, StackPanel> tabs = new Dictionary<string, StackPanel>();
			foreach (FieldValue value in values) {
				String cat = value.field.category;
				if (String.IsNullOrEmpty(cat))
					cat = "Default";
				StackPanel page;
				if (!tabs.TryGetValue(cat, out page)) {
					TabItem item = new TabItem();
					item.Header = cat;
					tabControlMain.Items.Add(item);
					page = tabs[cat] = new StackPanel();
					tabs[cat].Orientation = Orientation.Vertical;
					ScrollViewer viewer = new ScrollViewer();
					item.Content = viewer;
					tabs[cat].Tag = item;
					viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
					viewer.Content = tabs[cat];
				}
				element_to_page[value] = page.Tag as TabItem;
				StackPanel row = new StackPanel();
				row.Margin = new Thickness(0, 3, 5, 0);
				row.Orientation = Orientation.Horizontal;

				TextBlock txt = new TextBlock();
				txt.Text = value.field.display_name + ":     ";
				txt.FontWeight = FontWeights.Bold;
				txt.TextAlignment = TextAlignment.Right;
				txt.Width = 200;
				row.Children.Add(txt);
				elements_to_ui[value] = CreateElementValuer(value);
				row.Children.Add(elements_to_ui[value]);
				page.Children.Add(row);

			}
		}
		private void CreateSortableContextMenu(ListBox box){
			ContextMenu menu = new ContextMenu();
			var item = new MenuItem() { Header = "Move Up" };
			item.Click += (s,e) => SortableContextMenuMove(box,-1,s,e);
			menu.Items.Add(item);
			item = new MenuItem() { Header = "Move Down" };
			item.Click += (s, e) => SortableContextMenuMove(box, 1, s, e);
			menu.Items.Add(item);
			box.ContextMenu = menu;
			box.ContextMenuOpening += (s,e) => SortableMenuOpening(box,menu,s,e);
			box.PreviewMouseDown += box_MouseDown;
			
		}

		void box_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
			if (e.RightButton == MouseButtonState.Pressed)
				e.Handled = true;
		}
		

						
		private void SortableMenuOpening(ListBox box,ContextMenu menu,object sender, ContextMenuEventArgs e){
			menu.DataContext = ((FrameworkElement)e.OriginalSource).DataContext;
			object elem = menu.DataContext;
		}


		private void SortableContextMenuMove(ListBox box, int move_by, object sender, RoutedEventArgs e){
			MenuItem item = (sender as MenuItem);
			if (item == null || item.DataContext == null)
				return;
			object to_move = item.DataContext;
			for (int pos = 0; pos < box.Items.Count; pos++){
				if (box.Items[pos] == to_move){
					if (pos + move_by < 0 || pos + move_by >= box.Items.Count)
						return;
					bool selected = box.SelectedItems.Contains(to_move);
					box.Items.Remove(to_move);
					box.Items.Insert(pos + move_by, to_move);
					if (selected)
						box.SelectedItems.Add(to_move);
					return;
				}	
			}
			
		}

		private UIElement CreateElementValuer(FieldValue value) {
			Control ret = null;
			switch (value.field.type) {
				case Field.FIELD_TYPE.Int:
					TextBox ibox = new TextBox();
					ibox.Text = value.value;
					ibox.Width = 50;
					ret = ibox;
					break;
				case Field.FIELD_TYPE.MultiItem:
				case Field.FIELD_TYPE.MultiItemSort:
					ListBox listBox = new ListBox();
					listBox.SelectionMode = SelectionMode.Multiple;
					if (value.field.type == Field.FIELD_TYPE.MultiItemSort)
						CreateSortableContextMenu(listBox);
					listBox.Height = 100;
					listBox.Width = 190;
					String[] vals = value.value.Split(',');
					foreach (String val in vals) {
						Field.FieldOption opt = Field.FieldOption.GetByValue(value.field.options, val);
						if (opt != null){
							listBox.Items.Add(opt);
							listBox.SelectedItems.Add(opt);
						}
					}
					foreach (Field.FieldOption option in value.field.options) {
						if (value.field.Validator != null && !String.IsNullOrEmpty(value.field.Validator(option.value)))
							continue;
						if (listBox.Items.Contains(option))
							continue;
						listBox.Items.Add(option);
					}
					ret = listBox;
					break;
				case Field.FIELD_TYPE.String:
					TextBox box = new TextBox();
					box.Text = value.value;
					box.Width = 200;
					ret = box;
					break;
				case Field.FIELD_TYPE.Password:
					PasswordBox pbox = new PasswordBox();
					pbox.Password = value.value;
					pbox.Width = 200;
					ret = pbox;
					break;
				case Field.FIELD_TYPE.Bool:
					CheckBox cbox = new CheckBox();
					cbox.IsChecked = (value.value == "true");
					ret = cbox;
					break;
				case Field.FIELD_TYPE.Combo:
					ComboBox comboBox = new ComboBox();
					foreach (Field.FieldOption option in value.field.options) {
						if (value.field.Validator == null || String.IsNullOrEmpty(value.field.Validator(option.value)))
							comboBox.Items.Add(option);
					}
					comboBox.SelectedItem = Field.FieldOption.GetByValue(value.field.options, value.value);
					if (comboBox.SelectedIndex == -1)
						comboBox.SelectedIndex = 0;
					ret = comboBox;
					break;
			}
			AutomationProperties.SetName(ret, value.field.display_name);
			return ret;
		}
		private void btnCancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
			Close();
		}
		private String IntValidator(String str) {
			int trash;
			if (Int32.TryParse(str, out trash) == false)
				return "Number must be an integer";
			return null;
		}
		private void btnSave_Click(object sender, RoutedEventArgs e) {
			foreach (KeyValuePair<FieldValue, UIElement> kvp in elements_to_ui) {
				String val = GetValueFromUI(kvp.Key, kvp.Value);


				String err = "";
				if (kvp.Key.field.Validator != null)
					err = kvp.Key.field.Validator(val);
				else if (kvp.Key.field.type == Field.FIELD_TYPE.Int)
					err = IntValidator(val);

				if (!String.IsNullOrEmpty(err)) {
					String cat = kvp.Key.field.category;
					if (String.IsNullOrEmpty(cat))
						cat = "Default";
					element_to_page[kvp.Key].IsSelected = true;
					MessageBox.Show("The field \"" + kvp.Key.field.display_name + "\"(" + cat + " Tab) Is valid due to: " + err);
					return;
				}
			}
			foreach (KeyValuePair<FieldValue, UIElement> kvp in elements_to_ui) {
				String val = GetValueFromUI(kvp.Key, kvp.Value);
				kvp.Key.value = val;
			}
			DialogResult = true;
			Close();
		}
		private String GetValueFromUI(FieldValue value, UIElement elem) {
			String val = null;
			switch (value.field.type) {
				case Field.FIELD_TYPE.MultiItem:
				case Field.FIELD_TYPE.MultiItemSort:
					ListBox listBox = (elem as ListBox);
					val = "";
					foreach (Object obj in listBox.Items) {
						if (listBox.SelectedItems.Contains(obj) == false)
							continue;
						Field.FieldOption opt2 = obj as Field.FieldOption;
						if (opt2 != null) {
							if (!String.IsNullOrEmpty(val))
								val += ",";
							val += opt2.value;
						}
					}
					break;
				case Field.FIELD_TYPE.Int:
				case Field.FIELD_TYPE.String:
					val = ((TextBox) elem).Text;
					break;
				case Field.FIELD_TYPE.Password:
					val = ((PasswordBox) elem).Password;
					break;
				case Field.FIELD_TYPE.Bool:
					val = (((CheckBox) elem).IsChecked == true) ? "true" : "false";
					break;
				case Field.FIELD_TYPE.Combo:
					Field.FieldOption opt = ((ComboBox) elem).SelectedItem as Field.FieldOption;
					if (opt != null)
						val = opt.value;
					break;
			}
			return val;
		}
	}
}