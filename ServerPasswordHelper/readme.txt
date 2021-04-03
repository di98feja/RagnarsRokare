ServerPasswordHelper.readme

This mod does not modify gameplay in any way.
All it does is saving you from typing the server IP and/or the server password each time you connect.
Your password is not sent or stored anywhere but the shortcut itself.

Made and tested with Valheim v0.148.7

Installation:
Copy the RagnarsRokare_ServerPasswordHelper.dll file to your BepInEx plugin folder under Valheim

Usage:
-Create a shortcut to the Valheim.exe and place it on your desktop or activity bar.
-Edit the shortcut by right-clicking the shortcut and selecting Properties.
-Add the command line argument "pwd" in the "Target" textbox after the valheim.exe itself:
 "C:\Steam\steamapps\common\Valheim\valheim.exe" pwd <myPassw0rd>

The password itself must follow the pwd-argument separated by a space.

You can also add other standard arguments like:
"C:\Steam\steamapps\common\Valheim\valheim.exe" +connect <serveradress:port> pwd <myPassw0rd>
This will allow you connect to the server without any typing.


Built in is also an autosave that remembers the last server IP-adress entered into the connect dialog.

The code can be found at https://github.com/di98feja/RagnarsRokare

We hope you find this little mod useful!
// Barg and Morg
