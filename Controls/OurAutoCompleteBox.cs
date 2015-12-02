using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FSClient.Controls {
	public class OurAutoCompleteBox : AutoCompleteBox {
		public enum OurAutoCompleteFilterMode {
			None,
			StartsWith,
			StartsWithCaseSensitive,
			StartsWithOrdinal,
			StartsWithOrdinalCaseSensitive,
			Contains,
			ContainsCaseSensitive,
			ContainsOrdinal,
			ContainsOrdinalCaseSensitive,
			Equals,
			EqualsCaseSensitive,
			EqualsOrdinal,
			EqualsOrdinalCaseSensitive,
			Custom,
			ContainsSplit
		}
		private string last_search_term;
		private string[] last_words;
		private bool MultiTextFilter(string search, string item) {
			if(search != last_search_term) {
				last_search_term = search;
				last_words = search.Split(' ');
			}
			return last_words.All(word => item.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) != -1);
		}
		public new OurAutoCompleteFilterMode FilterMode {
			get { return (OurAutoCompleteFilterMode)GetValue(FilterModeProperty); }
			set {
				SetValue(FilterModeProperty, value);
			}
		}
		public static new readonly DependencyProperty FilterModeProperty = DependencyProperty.Register("FilterMode", typeof(OurAutoCompleteFilterMode), typeof(OurAutoCompleteBox), new PropertyMetadata((object)OurAutoCompleteFilterMode.StartsWith, new PropertyChangedCallback(OurAutoCompleteBox.OnFilterModePropertyChanged)));

		private static void OnFilterModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			OurAutoCompleteBox autoCompleteBox = d as OurAutoCompleteBox;
			AutoCompleteBox base_box = autoCompleteBox;
			var mode = (OurAutoCompleteFilterMode)e.NewValue;
			switch(mode) {
				case OurAutoCompleteFilterMode.ContainsSplit:
					base_box.FilterMode = AutoCompleteFilterMode.Custom;
					autoCompleteBox.TextFilter = autoCompleteBox.MultiTextFilter;
					break;
				default:
					base_box.FilterMode = (AutoCompleteFilterMode)mode;
					break;
			}
		}
		private TextBox the_textbox;
		private void find_textbox() {
			if(the_textbox != null)
				return;
			the_textbox = Template.FindName("Text", this) as TextBox;
			if(the_textbox != null)
				the_textbox.TabIndex = TabIndex;
		}
		public TextBox GetActualTextbox() {
			find_textbox();
			return the_textbox;
		}
		public bool TextBoxHasFocus() {
			find_textbox();
			if(the_textbox == null)
				return false;
			return the_textbox.IsFocused;
		}
		public void TextBoxFocus() {
			find_textbox();
			if(the_textbox != null)
				the_textbox.Focus();
		}
	}
}
