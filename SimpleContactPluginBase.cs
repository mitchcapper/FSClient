using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FSClient.Controls;

namespace FSClient {
	public abstract class SimpleContactPluginBase : SimpleContactPluginBaseAsync {//Shim class to support older plugins without requiring them to use aync methods
		protected abstract void LoadDatabase(ref Dictionary<string, string> number_to_alias_db);
		protected abstract void LoadXFERDatabase(ref Dictionary<string, string> number_to_xfer_db);
		protected abstract void UpdateDatabase(string number, string alias);
		protected abstract void UpdateXFERDatabase(string number, string xfer_name);

		protected virtual string EditAlias(Call c, String number, String default_value) {
			return base.EditAliasAsync(c, number, default_value).Result;
		}

		protected override Task<string> EditAliasAsync(Call c, string number, string default_value) {
			var res = EditAlias(c, number, default_value);
			return Task.FromResult(res);
		}

		protected virtual void DeleteNumber(string number) {
			base.DeleteNumberAsync(number);
		}
		protected virtual void DeleteXFER(string number) {
			base.DeleteXFERAsync(number);
		}

		protected override Task DeleteXFERAsync(string number) {
			DeleteXFER(number);
			return completed_task;
		}

		protected override Task DeleteNumberAsync(string number) {
			DeleteNumber(number);
			return completed_task;
		}

		protected override Task UpdateXFERDatabaseAsync(string number, string xfer_name) {
			UpdateXFERDatabase(number, xfer_name);
			return completed_task;
		}

		protected override Task UpdateDatabaseAsync(string number, string alias) {
			UpdateDatabase(number, alias);
			return completed_task;
		}

		protected override Task<Dictionary<string, string>> LoadXFERDatabaseAsync(Dictionary<string, string> number_to_xfer_db) {
			LoadXFERDatabase(ref number_to_xfer_db);
			return Task.FromResult(number_to_xfer_db);
		}

		protected override Task<Dictionary<string, string>> LoadDatabaseAsync(Dictionary<string, string> number_to_alias_db) {
			LoadDatabase(ref number_to_alias_db);
			return Task.FromResult(number_to_alias_db);
		}
	}

	public abstract class SimpleContactPluginBaseAsync : IContactPlugin {
		protected abstract void _TryResolveNewNumber(string number, NumberResolved on_resolved);
		protected abstract Task<Dictionary<string, string>> LoadDatabaseAsync(Dictionary<string, string> number_to_alias_db);
		protected abstract Task<Dictionary<string, string>> LoadXFERDatabaseAsync(Dictionary<string, string> number_to_xfer_db);
		protected abstract Task UpdateDatabaseAsync(string number, string alias);

		protected abstract Task UpdateXFERDatabaseAsync(string number, string xfer_name);


		protected virtual void ModifyRightClickMenu(Call call, ContextMenu orig_menu, List<MenuItem> items) {
			return;
		}
		protected virtual void ModifyXFERMenu(Call call, ContextMenu orig_menu, List<MenuItem> items) {
			return;
		}
		protected virtual string NormalizeNumber(string number) {
			return number;
		}
		protected virtual Task DeleteNumberAsync(string number) {
			number_to_alias.Remove(number);
			return completed_task;
		}
		protected virtual Task DeleteXFERAsync(string number) {
			number_to_xfer.Remove(number);
			return completed_task;
		}

		protected static Task completed_task = Task.Delay(0);
		protected virtual bool CanDeleteContact() {
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
		protected virtual string DefaultEditValue(Call call) {
			if (call.other_party_name != call.other_party_number)
				return call.other_party_name;
			return "";
		}

		protected Dictionary<string, string> number_to_alias = new Dictionary<string, string>();
		protected Dictionary<string, string> number_to_xfer = new Dictionary<string, string>();

		public override IEnumerable<MenuItem> CallRightClickMenu(Call call, ContextMenu orig_menu) {
			var ret = new List<MenuItem>();
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			ret.Add(item);
			ModifyRightClickMenu(call, orig_menu, ret);
			return ret;
		}
		public override IEnumerable<MenuItem> XFERMenu(Call call, ContextMenu orig_menu) {
			MenuItem item;
			var ret = new List<MenuItem>();
			bool on_call = call != null;
			foreach (KeyValuePair<string, string> kvp in number_to_xfer) {
				item = new MenuItem();
				String num = kvp.Key;
				if (on_call) {
					item.Click += (s, e) => {
						orig_menu.IsOpen = false;
						call.Transfer(num);
					};
				}
				item.Header = kvp.Value + " (" + num + ")";
				ret.Add(item);


				MenuItem sub_item = new MenuItem();
				if (!on_call) {
					sub_item.Click += edit_xfer_click;
					sub_item.Header = "Edit Alias";
					sub_item.DataContext = kvp;
					item.Items.Add(sub_item);
					if (CanDeleteXFER()) {
						sub_item = new MenuItem();
						sub_item.Click += del_xfer_click;
						sub_item.Header = "Delete Alias";
						sub_item.DataContext = kvp;
						item.Items.Add(sub_item);
					}
				}
			}
			if (!on_call) {
				item = new MenuItem();
				item.Click += add_xfer_click;
				item.Header = "Add Transfer Alias";
				ret.Add(item);
			}
			ModifyXFERMenu(call, orig_menu, ret);
			return ret;
		}

		private async void add_xfer_click(object sender, RoutedEventArgs e) {
			String number = InputBox.GetInput("Adding Transfer Alias", "What number should the transfer go to?", "");
			number = IsValidXFERNumber(number);
			if (String.IsNullOrWhiteSpace(number))
				return;
			String alias = InputBox.GetInput("Adding Transfer Alias", "Alias for transfer number: " + number, "");
			alias = IsValidAlias(alias);
			if (String.IsNullOrWhiteSpace(alias))
				return;
			number_to_xfer[number] = alias;
			await UpdateXFERDatabaseAsync(number, alias);
		}

		protected async void del_xfer_click(object sender, RoutedEventArgs e) {
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;
			KeyValuePair<string, string> kvp = (KeyValuePair<string, string>)item.DataContext;
			await DeleteXFERAsync(kvp.Key);
		}

		protected async void edit_xfer_click(object sender, RoutedEventArgs e) {
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

			await UpdateXFERDatabaseAsync(number, alias);
		}


		public override IEnumerable<MenuItem> ContactRightClickMenu() {
			List<MenuItem> items = new List<MenuItem>();
			MenuItem item = new MenuItem();
			item.Click += item_Click;
			item.Header = "Edit Contact";
			items.Add(item);
			if (CanDeleteContact()) {
				item = new MenuItem();
				item.Click += delete_item_click;
				item.Header = "Delete Contact";
				items.Add(item);
			}
			item = new MenuItem();
			item.Click += contact_call_click;
			item.Header = "Call";
			items.Add(item);
			item = new MenuItem();
			item.Header = "Call On Account";
			item.SubmenuOpened += call_on_account_item_SubmenuOpened;
			MenuItem item2 = new MenuItem { Header = "Call On Default Account", IsEnabled = false };
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
			foreach (var acct in Account.accounts) {
				MenuItem new_item = new MenuItem();
				new_item.Header = acct.ToString();
				new_item.Click += contact_call_click;
				new_item.Tag = acct;
				item.Items.Add(new_item);
			}
			first.Visibility = item.Items.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
		}




		private async void delete_item_click(object sender, RoutedEventArgs e) {
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;

			SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry == null)
				return;
			String number = entry.number;
			number = NormalizeNumber(number);
			await DeleteNumberAsync(number);
			refresh_search_box();
		}

		private void contact_call_click(object sender, RoutedEventArgs e) {
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
		protected virtual Task<string> EditAliasAsync(Call c, String number, String default_value) {
			var res = InputBox.GetInput("Editing Contact", "Edit alias for number: " + number, default_value);
			return Task.FromResult(res);
		}
		protected async void item_Click(object sender, RoutedEventArgs e) {
			try {
				MenuItem item = sender as MenuItem;
				if (item == null)
					return;
				String number;
				String default_edit_value;

				Call c = item.DataContext as Call;
				if (c != null) {
					number = c.other_party_number;
					default_edit_value = DefaultEditValue(c);
				} else {
					SearchAutoCompleteEntry entry = item.DataContext as SearchAutoCompleteEntry ?? search_box.SelectedItem as SearchAutoCompleteEntry;

					if (entry == null)
						return;
					number = entry.number;
					default_edit_value = entry.alias;
				}

				String orig_number = number;
				number = NormalizeNumber(number);
				String alias = await EditAliasAsync(c, number, default_edit_value);
				alias = IsValidAlias(alias);
				if (alias == null)
					return;

				number_to_alias[number] = alias;

				foreach (Call call in Call.calls) {
					if (orig_number == call.other_party_number || number == call.other_party_number)
						call.other_party_name = alias;
				}
				try {
					await UpdateDatabaseAsync(number, alias);
				} catch (Exception ex) {
					MessageBox.Show("Unable to update alias due to: " + ex.Message);
				}
				refresh_search_box();
			} catch (Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}
		public override void ResolveNumber(string number, NumberResolved on_resolved) {
			String alias;
			number = NormalizeNumber(number);
			if (number_to_alias.TryGetValue(number, out alias)) {
				on_resolved(alias);
				return;
			}
			_TryResolveNewNumber(number, on_resolved);
		}
		public override async void Initialize() {
			try {
				number_to_alias = await LoadDatabaseAsync(number_to_alias);
				number_to_xfer = await LoadXFERDatabaseAsync(number_to_xfer);
				refresh_search_box();


			} catch (Exception e) {
				MessageBox.Show("Initialization exception for plugin " + this.GetType() + " of: " + e.Message);
			}
		}
		protected void refresh_search_box() {
			if (search_box == null)
				return;
			search_box.ItemsSource = (from c in number_to_alias orderby c.Value, c.Key select new SearchAutoCompleteEntry(c.Key, c.Value)).ToArray();
		}
		protected class SearchAutoCompleteEntry {
			public string number;
			public string alias;
			public string display_name;
			public SearchAutoCompleteEntry(String number, String alias) {
				this.number = number;
				this.alias = alias;
				if (String.IsNullOrWhiteSpace(alias) || number == alias)
					display_name = number;
				else
					display_name = alias + " - " + number;
			}
			public override string ToString() {
				return display_name;
			}
		}
		protected void search_box_PreviewKeyUp(object sender, KeyEventArgs e) {
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

			foreach (MenuItem item in ContactRightClickMenu()) {
				real_search_box.ContextMenu.Items.Add(item);
			}

			search_box.PreviewKeyUp += search_box_PreviewKeyUp;
			search_box.MouseDoubleClick += search_box_MouseDoubleClick;
			if (number_to_alias.Count > 0)
				refresh_search_box();
			return true;
		}


		protected void call_current_contact() {
			SearchAutoCompleteEntry entry = search_box.SelectedItem as SearchAutoCompleteEntry;
			if (entry != null)
				Broker.get_instance().DialString(entry.number);
		}
		void search_box_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			call_current_contact();
		}

		void search_box_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
			if (search_box.SelectedItem == null)
				real_search_box.ContextMenu.Visibility = Visibility.Hidden;
			else
				real_search_box.ContextMenu.Visibility = Visibility.Visible;
		}
	}
}
