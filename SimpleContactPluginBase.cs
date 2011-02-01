using System;
using System.Collections.Generic;
using System.Windows.Controls;

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

		protected void item_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			if (item == null)
				return;
			Call c = item.DataContext as Call;
			if (c == null)
				return;
			String number = NormalizeNumber(c.other_party_number);
			String alias = InputBox.GetInput("Editing Contact", "Edit alias for number: " + number, DefaultEditValue(c));
			alias = IsValidAlias(alias);
			if (alias == null)
				return;

			number_to_alias[number] = alias;

			foreach (Call call in Call.calls)
			{
				if (c.other_party_number == call.other_party_number)
					c.other_party_name = alias;
			}

			UpdateDatabase(number, alias);
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

		}

	}
}
