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
		protected abstract void LoadXFERDatabase(ref Dictionary<string, string> number_to_xfer_db);
		protected abstract void UpdateDatabase(string number, string alias);

		protected abstract void UpdateXFERDatabase(string number, string xfer_name);


		protected virtual void ModifyRightClickMenu(Call call, ContextMenu menu)
		{
			return;
		}
		protected virtual void ModifyXFERRightClickMenu(Call call, ContextMenu menu) {
			return;
		}
		protected virtual string NormalizeNumber(string number)
		{
			return number;
		}
		protected virtual void DeleteNumber(string number){
			number_to_alias.Remove(number);
		}
		protected virtual void DeleteXFER(string number) {
			number_to_xfer.Remove(number);
		}
		protected virtual bool CanDeleteContact(){
			return true;
		}
		protected virtual bool CanDeleteXFER() {
			return true;
		}
		protected virtual string IsValidAlias(String str)//return null to abort updating, otherwire return string
		{ 
			return str;
		}
		protected virtual string IsValidXFERAlias(String str)//return null to abort updating, otherwire return string
		{
			return str;
		}
		protected virtual string IsValidXFERNumber(String str)//return null to abort updating, otherwire return string
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
		protected Dictionary<string, string> number_to_xfer = new Dictionary<string, string>();

		public override void CallRightClickMenu(Call call, ContextMenu menu) {
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			menu.Items.Add(item);
			ModifyRightClickMenu(call, menu);
		}
		public override void XFERRightClickMenu(Call call, ContextMenu menu) {
			MenuItem item;
			bool on_call = call != null;
			foreach (KeyValuePair<string, string> kvp in number_to_xfer) {
				item = new MenuItem();
				String num = kvp.Key;
				if (on_call){
					item.Click += (s, e) =>{
					              	menu.IsOpen = false;
					              	call.Transfer(num);
					              };
				}
				item.Header = kvp.Value + " (" + num + ")";
				menu.Items.Add(item);
				

				MenuItem sub_item = new MenuItem();
				if (!on_call){
					sub_item.Click += edit_xfer_click;
					sub_item.Header = "Edit Alias";
					sub_item.DataContext = kvp;
					item.Items.Add(sub_item);
					if (CanDeleteXFER()){
						sub_item = new MenuItem();
						sub_item.Click += del_xfer_click;
						sub_item.Header = "Delete Alias";
						sub_item.DataContext = kvp;
						item.Items.Add(sub_item);
					}
				}
			}
			if (!on_call){
				item = new MenuItem();
				item.Click += add_xfer_click;
				item.Header = "Add Transfer Alias";
				menu.Items.Add(item);
			}
			ModifyXFERRightClickMenu(call, menu);
		}

		private void add_xfer_click(object sender, RoutedEventArgs e){
			String number = InputBox.GetInput("Adding Transfer Alias", "What number should the transfer go to?", "");
			number = IsValidXFERNumber(number);
			if (String.IsNullOrWhiteSpace(number))
				return;
			String alias = InputBox.GetInput("Adding Transfer Alias", "Alias for transfer number: " + number, "");
			alias = IsValidAlias(alias);
			if (String.IsNullOrWhiteSpace(alias))
				return;
			number_to_xfer[number] = alias;
			UpdateXFERDatabase(number, alias);
		}

		protected void del_xfer_click(object sender, RoutedEventArgs e) {
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;
			KeyValuePair<string, string> kvp = (KeyValuePair<string, string>) item.DataContext;
			DeleteXFER(kvp.Key);
		}

		protected void edit_xfer_click(object sender, RoutedEventArgs e) {
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;
			KeyValuePair<string, string> kvp = (KeyValuePair<string, string>)item.DataContext;
			String number = kvp.Key;
			String alias = InputBox.GetInput("Editing XFER", "Edit transfer alias for number: " + number, kvp.Value);
			alias = IsValidAlias(alias);
			if (alias == null)
				return;

			number_to_xfer[number] = alias;

			UpdateXFERDatabase(number, alias);
		}


		public override IEnumerable<MenuItem> ContactRightClickMenu(){
			List<MenuItem> items = new List<MenuItem>();
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			items.Add(item);
			if (CanDeleteContact()){
				item = new MenuItem();
				item.Click += delete_item_click;
				item.Header = "Delete Contact";
				items.Add(item);
			}
			item = new MenuItem();
			item.Click +=contact_call_click;
			item.Header = "Call";
			items.Add(item);
			item = new MenuItem();
			item.Header = "Call On Account";
			item.SubmenuOpened += call_on_account_item_SubmenuOpened;
			MenuItem item2 = new MenuItem { Header = "Call On Default Account",IsEnabled = false};
			item2.Click += contact_call_click;
			item.Items.Add(item2);
			items.Add(item);
			return items;
		}

		void call_on_account_item_SubmenuOpened(object sender, RoutedEventArgs e) {
			MenuItem item = sender as MenuItem;
			if (item == null || item.Items.Count == 0)
				return;
			MenuItem first = item.Items[0] as MenuItem;
			if (first == null)
				return;
			item.Items.Clear();
			item.Items.Add(first);
			foreach (var acct in Account.accounts){
				MenuItem new_item = new MenuItem();
				new_item.Header = acct.ToString();
				new_item.Click += contact_call_click;
				new_item.Tag = acct;
				item.Items.Add(new_item);
			}
			first.Visibility = item.Items.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
		}
		
		

		
		private void delete_item_click(object sender, RoutedEventArgs e){
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;

			SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry == null)
				return;
			String number = entry.number;
			number = NormalizeNumber(number);
			DeleteNumber(number);
			refresh_search_box();
		}

		private void contact_call_click(object sender, RoutedEventArgs e){
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;

			SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry == null)
				return;
			Account acct = item.Tag as Account;
			if (acct != null)
				Broker.get_instance().DialString(acct, entry.number);
			else
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
			LoadXFERDatabase(ref number_to_xfer);
			refresh_search_box();
		}
		protected void refresh_search_box(){
			if (search_box == null)
				return;
			search_box.ItemsSource = (from c in number_to_alias orderby c.Value,c.Key select new SearchAutoCompleteEntry(c.Key, c.Value)).ToArray();
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
