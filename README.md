# :computer: Pimp My Terminal
## About
This project is inspired by Terminal Visualizers like [OhMyPosh](https://ohmyposh.dev/) and others.
I created executables that work on Windows and Ubuntu systems that will allow you to customize your
[Windows Terminal](https://www.microsoft.com/store/productId/9N0DX20HK701?ocid=libraryshare) and/or Ubuntu Terminal (tmux, zsh, qterminal).

## Install :wrench:
Dependencies:<br>
- Install [DotNet SDK](https://dotnet.microsoft.com/en-us/download) if your Windows install does not have it. You can check if it is installed
by running `dotnet` in powershell.exe or cmd.exe.

```powershell
# Build the C# - WinTerminal Project
git clone https://github.com/EndermanSUPREME/PimpMyTerminal.git
cd .\PimpMyTerminal
cd .\windows\WinTerminal
dotnet restore .
dotnet build
# Execute WinTerminal Binary
.\bin\Debug\net9.0-windows\WinTerminal.exe
```

## Basic Usage
![app-preview](https://github.com/user-attachments/assets/3207f789-6fdd-4834-9a3a-0376fbf09588)
- Click the `Set Source Folder` button to set a folder pointing to the background image collection you wish to display.
- Click the `Selectd Profile` drop-down box to set the Profile you wish to apply these settings to, Microsoft Terminal can have many Profiles.
- Click the `Get Random Image` button to pull an image from your source collection, this image will be set as a background image in your Terminal.
- Use the `Source Image Alpha` scroll-bar to set the transparancy of the image being displayed.
- If you wish to have a slide-show background, use the `Set Timer` boxes to set the delay between image changes and click the `Enable Slide-Show` toggle.

This program allows the user to manually load and save their state, but this program will save the state on exit and load it on launch.

The user is also free to change the Font with the `Selected Font` drop-down box that will be displayed on their selected Profile.
