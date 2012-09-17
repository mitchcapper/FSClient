using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace FSClient.Controls {
	public class OurAutoCompleteBox : AutoCompleteBox {
	
		private TextBox the_textbox;
		private void find_textbox(){
			if (the_textbox != null)
				return;
			the_textbox = Template.FindName("Text", this) as TextBox;
			if (the_textbox != null)
				the_textbox.TabIndex = TabIndex;
		}
		public TextBox GetActualTextbox() {
			find_textbox();
			return the_textbox;
		}
		public bool TextBoxHasFocus(){
			find_textbox();
			if (the_textbox == null)
				return false;
			return the_textbox.IsFocused;
		}
		public void TextBoxFocus(){
			find_textbox();
			if (the_textbox != null)
				the_textbox.Focus();
		}
	}
}
