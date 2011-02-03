using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FSClient.Controls;

namespace FSClient
{
	public abstract class SimpleContactPluginBase : IContactPlugin
	{
		protected abstract void _TryResolveNewNumber(string number, NumberResolved on_resolved);
		protected abstract void LoadDatabase(ref Dictionary<string, string> number_to_alias_db);
		protected abstract void UpdateDatabase(string number, string alias);


		protected virtual void ModifyRightClickMenu(Call call, ContextMenu menu)
		{
			return;
		}
		protected virtual string NormalizeNumber(string number)
		{
			return number;
		}
		protected virtual string IsValidAlias(String str)//return null to abort updating, otherwire return string
		{ 
			return str;
		}
		protected virtual string DefaultEditValue(Call call)
		{
			if (call.other_party_name != call.other_party_number)
				return call.other_party_name;
			return "";
		}

		protected Dictionary<string, string> number_to_alias = new Dictionary<string, string>();

		public override void CallRightClickMenu(Call call, ContextMenu menu) {
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			menu.Items.Add(item);
			ModifyRightClickMenu(call, menu);
		}

		public override IEnumerable<MenuItem> ContactRightClickMenu(){
			List<MenuItem> items = new List<MenuItem>();
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			items.Add(item);
			item = new MenuItem();
			item.Click +=contact_call_click;
			item.Header = "Call";
			items.Add(item);
			return items;
		}

		private void contact_call_click(object sender, RoutedEventArgs e){
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;

			SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry == null)
				return;
			Broker.get_instance().DialString(entry.number);
		}

		protected void item_Click(object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;
			String number;
			String default_edit_value;

			Call c = item.DataContext as Call;
			if (c != null){
				number = c.other_party_number;
				default_edit_value = DefaultEditValue(c);
			}
			else{
				SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;

				if (entry == null)
					return;
				number = entry.number;
				default_edit_value = entry.alias;
			}

			String orig_number = number;
			number = NormalizeNumber(number);
			String alias = InputBox.GetInput("Editing Contact", "Edit alias for number: " + number, default_edit_value);
			alias = IsValidAlias(alias);
			if (alias == null)
				return;

			number_to_alias[number] = alias;

			foreach (Call call in Call.calls)
			{
				if (orig_number == call.other_party_number || number == call.other_party_number)
					call.other_party_name = alias;
			}

			UpdateDatabase(number, alias);
			refresh_search_box();
		}
		public override void ResolveNumber(string number, NumberResolved on_resolved)
		{
			String alias;
			number = NormalizeNumber(number);
			if (number_to_alias.TryGetValue(number, out alias))
			{
				on_resolved(alias);
				return;
			}
			_TryResolveNewNumber(number, on_resolved);
		}
		public override void Initialize()
		{
			LoadDatabase(ref number_to_alias);
	
			refresh_search_box();
		}
		protected void refresh_search_box(){
			if (search_box == null)
				return;
			search_box.ItemsSource = from c in number_to_alias orderby c.Value,c.Key select new SearchAutoCompleteEntry(c.Key, c.Value);
		}
		protected class SearchAutoCompleteEntry{
			public string number;
			public string alias;
			public string display_name;
			public SearchAutoCompleteEntry(String number, String alias){
				this.number = number;
				this.alias = alias;
				if (String.IsNullOrWhiteSpace(alias) || number == alias)
					display_name = number;
				else
					display_name = alias + " - " + number;
			}
			public override string ToString(){
				return display_name;
			}
		}
		protected void search_box_PreviewKeyUp(object sender, KeyEventArgs e){
			if (e.Key == Key.Enter)
				call_current_contact();
			else if (e.Key == Key.Escape)
				Broker.get_instance().MainWindowRemoveFocus(true);
		}

		protected OurAutoCompleteBox search_box;
		private TextBox real_search_box;
		public override bool HandleSearchBox(OurAutoCompleteBox box) {
			search_box = box;

			real_search_box = search_box.GetActualTextbox();
			real_search_box.ContextMenu = new ContextMenu();
			real_search_box.ContextMenuOpening += search_box_ContextMenuOpening;
			
			foreach (MenuItem item in ContactRightClickMenu()){
				real_search_box.ContextMenu.Items.Add(item);
			}
			
			search_box.PreviewKeyUp += search_box_PreviewKeyUp;
			search_box.MouseDoubleClick += search_box_MouseDoubleClick;
			if (number_to_alias.Count > 0)
				refresh_search_box();
			return true;
		}
		protected void call_current_contact(){
			SearchAutoCompleteEntry entry = search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry != null)
				Broker.get_instance().DialString(entry.number);
		}
		void search_box_MouseDoubleClick(object sender, MouseButtonEventArgs e){
			call_current_contact();
		}

		void search_box_ContextMenuOpening(object sender, ContextMenuEventArgs e){
			if (search_box.SelectedItem == null)
				real_search_box.ContextMenu.Visibility = Visibility.Hidden;
			else
				real_search_box.ContextMenu.Visibility = Visibility.Visible;
		}
	}
}
