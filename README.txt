FSClient is a Windows sip client that uses Embedded freeswitch.  It can handle multiple calls simultaneously, headset support, contactbook support(basic), and multiple sip accounts at once.  It takes a bit of time to load on startup (generally 5-15 seconds) due to initially spinning up freeswich.  This is normal.

Layout:
	There are 3 main areas Calls, Current, Accounts.
	Calls - This show any active calls (always at the top) or previous calls made.  Hover over a call to display information about it. Double clicking a call will make that call active if the call is not ended. If the call is ended then it will redial that number on the same account the call came in on.  Right click on a call shows the contact book options.
	Current - The current area is in the center it shows the current calls, the dialpad and any current actions that can be taken.  The XFER button is to transfer,  DND will reject any incoming calls, Speaker will switch the audio device from the main devices to the speakerphone devices (set in options).  Yellow on a button means its enabled.
	Accounts - Accounts shows your sip accounts, right click here to add a new one, once an account is added click the checkbox to enable it.  Unchecking it will disable that account until re-checked.  Note that there is also the 'default account' if you only have one this will be it, otherwise you should choose the default account by clicking it and clicking 'Set Default' you cannot set an account to default that is not enabled.  The default account is what account is used to dial out by default.  To specify an account to dial out on put the '#' and the account number infront of the dial string so: #48001231234  will dial on account #4.

Options
	Reload devices rescans for any hardware changes (ie you plug or unplug an audio device).
	Headset Device - If you had a supported headset you can select it here.  This just enables the link/buttons/controls/caller id on your headset to work with FSCLient you do not need to do this to just use it as a microphone/speaker.
	Main Input/Output - this is the normal sound devices used
	Speakerphone Input/Output - these are the sound devices used when you hit the speakerphone button.
	Ring Device - this is what you will hear ringing on when there is an incoming call.
	Show incoming call balloons - systray popup balloons that show incoming calls and allow you to take actions
	Clear Key Presses In Call Stats On Disconnect - by default FSClient stores the DTMF presses during a call and shows them to you when you hover over a call afterwards.  If you enter sensitive data you can uncheck this and it will stop doing this (and clear it for current calls).
	Only allow numeric input for dialing - With this enabled when you type or paste letters they are converted to numbers automatically (like a phone keypad) rather than allowed.  If you want to do non-numeric dial strings uncheck this.


Plugins
	Headset Plugins
		There are two native headset plugins provided for Jabra and Plantronics headsets.  If you do not have one of these you can still leave the plugin in the directory it will not harm anything.  If you do have one of these headsets you will then be able to select the headset in the options.  This will automatically open/close the audio link, support muting and buttons, caller id, etc in the headset.  You may need the runtime/sdk for the headset installed for it to work.

	Contact Plugins
		There is one contact plugin provided by default, SimpleXML.  This plugin allows you to attach a name/alias to a phone number so future calls show this information for the number.  Right click and click edit on a call to edit the name/alias.  The plugin stores all the names in an xml file in the AppData\Local folder for the app.  Note you cannot have more than one contact plugin in the plugins folder or else only the first will be used.  There are two ways to write a contact plugin, you can inherit from the SimpleContactPluginBase or IContactPlugin classes.  SimpleContactPluginBase is just a base class that inherits from IContactPlugin to make things a bit easier.  It uses basically the same interface as SimpleXML but you could replace SimpleXML with something to tie it to outlook, a ldap address book etc.
	
	Plugin Development
		FSClient was made to make it easy to extend through plugins.  The two plugin types supported are noted above.  Writing a plugin is exceptionally easy, you simply inherit the base interface fill in a few functions and are good to go.  Use the above for examples.  As for how easy, well the SimpleXML Contact Plugin was written in under an hour and under 100 lines of code.  Writing plugins is easiest in a .net language (although you can write them in any language).  FSClient will try to load any plugins in the plugin dir.  

	Plugin Troubleshooting
		FSClient will generally silently handle errors that occur with plugins.  If you are a plugin developer catching your own exceptions, or breakpointing where FSCLient catches them maybe a good idea.   To see what exceptions happen look at the plugins.log file in the AppData\Local folder for the application.  Note some plugins throw exceptions if you just don't support them which is normal.