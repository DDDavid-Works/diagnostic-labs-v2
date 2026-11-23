# Setting up a Diagnostic Labs station

For the lab's IT staff. One program, **StationSetup.exe**, prepares a PC to run Diagnostic Labs. It takes a few minutes per PC and needs no administrator rights on the station.

## What you need

- **StationSetup.exe** on a USB stick. It is in the server kit, and in `C:\DiagnosticLabs\setup` on the server after an update that carried it.
- The **server's name or IP address**, shown on the server by StationSetup's **Server** tab.
- The **station password**, the password of the server's `dlupdate` account. It is also shown on the Server tab (as administrator).
- The station: **Windows 10 (22H2) or Windows 11, 64-bit**, on the same network as the server. Windows 7 and 8 cannot run Diagnostic Labs.

Nothing else needs to be installed first: no .NET and no database drivers.

## On the server (once, or when you lose the password)

1. Copy StationSetup.exe to the server and double-click it. Open the **Server** tab.
2. Press **Restart as administrator** and answer Yes.
3. Read the top lines: they give the server's name and IP address that stations type in.
4. Under **Station password**:
   - if a password is saved, tick **Show** to read it;
   - if none is saved (the first time), press **Set a new password...**. Note it in the lab's password book.
5. Every line in the check list should have a green tick. A red cross says what to fix.

Setting a new password does not stop Diagnostic Labs on stations that are already set up, but they stop receiving updates until StationSetup is run on them again with the new password. Only change it when you have to.

## On each station

1. Sign in to Windows **as the person who will use Diagnostic Labs**. The share login is stored per Windows user. If several Windows users share the PC, repeat these steps for each of them.
2. Run StationSetup.exe from the USB stick. Stay on **This station**.
3. Type the server name (or IP address) and the station password, then press **Set up this PC**.
4. Wait for the green ticks. The last step copies the program (about 160 MB) and shows a progress bar.
5. Press **Open Diagnostic Labs**, or use the new **Diagnostic Labs** shortcut on the desktop or in the Start menu. The sign-in window should appear.

From then on, always start the program from that shortcut: it brings in new versions by itself.

### When a step fails

| Step | What usually helps |
|---|---|
| Find the server | Check the spelling. Type the server's IP address instead of its name. |
| Reach the file share (port 445) | The server is off, on another network, or its network is set to Public. Set both PCs' networks to **Private**. |
| Read the program on the server | **Password refused:** check it on the server's Server tab. **Already connected with another account:** sign out of Windows, sign in again, and run setup again. |
| Reach the database | SQL Server is not running on the server, or the firewall blocks it. If the message says the station cannot find the server's name, the database settings on the server must use its IP address (ask the developer). |
| Copy Diagnostic Labs | If Diagnostic Labs is open, close it and run setup again. |

Each run is written to `%LOCALAPPDATA%\DiagnosticLabs\station-setup.log` (button **Open log**). The log never contains the password.

### Running it again

Setup can be run again at any time, for example after a password change, a new server IP, or a broken shortcut. It fills in the server from last time, repairs what is missing, and leaves the station's database settings as they are.

## What setup changes on a station

All of it is for the signed-in Windows user only:

- a saved Windows credential for the server (the same as `cmdkey /add:SERVER /user:SERVER\dlupdate`). You can see it in Control Panel > Credential Manager > Windows Credentials;
- the launcher, in `%LOCALAPPDATA%\DiagnosticLabs\launcher`;
- the program, in `%LOCALAPPDATA%\DiagnosticLabs\app`, with the database settings in `appsettings.Local.json` there;
- the **Diagnostic Labs** shortcuts on the desktop and in the Start menu.

To remove Diagnostic Labs from a PC, delete those two folders and the shortcuts, and remove the credential in Credential Manager.

## Where the password is kept

On the server, a copy of the station password is kept in `C:\ProgramData\DiagnosticLabs\station-password.dat`. It is encrypted to that PC and only administrators can open it. That copy is what the Server tab shows. Passwords set before StationSetup existed (by Install-Server.ps1) were never saved there, so the first time you have to set a new one.
