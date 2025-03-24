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
