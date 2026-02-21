# RPCParadox
This is a project that implements Discord Rich Presence for Paradox Interactive games. It is developed in C# using the .NET framework. The project is currently in its early stages, with basic functionality implemented and plans for more advanced features in the future.

This project previously took the form of a Java application, but was rewritten in C# to take advantage of the .NET framework and its capabilities for desktop applications. The Java version is still available in the `java-old` branch, but it is no longer maintained and is extremely broken. As in, I don't even want to look at it, that's how big of a mess I made it. That is my fault and I am very sorry.


> [!WARNING]  
> This software implements a memory scanner to read the game state of supported Paradox games. I have tried to ensure that this is done in a safe and efficient manner, but there is always a risk of crashes or other issues when interacting with another process's memory (e.g. system instability, anticheat flagging, etc). Use this software at your own risk, and please report any issues you encounter.

[![GitHub Release](https://img.shields.io/github/v/release/DairyProducts/RPCParadox?label=stable%20release)](https://github.com/DairyProducts/RPCParadox/releases)
[![GitHub Release](https://img.shields.io/github/v/release/DairyProducts/RPCParadox?include_prereleases&label=latest%20build&color=orange)](https://github.com/DairyProducts/RPCParadox/releases)
[![GitHub last commit (branch)](https://img.shields.io/github/last-commit/DairyProducts/RPCParadox/main?label=latest%20commit&color=red)](https://github.com/DairyProducts/RPCParadox/commits/main/)

## Currently Supported Games
- Stellaris

## Current Features
- Basic Rich Presence functionality (game name, time played, icon, game date)
- Automatic detection of supported Paradox games
- Fun toast message on the RPC card

## Planned Features
- More detailed Rich Presence information (e.g., current played country, in-game date, war status)
- Support for additional Paradox games
- Autostart with Windows

## Dependencies
- [DiscordRichPresence version 1.5.0.51](https://github.com/Lachee/discord-rpc-csharp) - higher versions currently have a bug where the response from Discord is not properly handled, causing the RPC to not work.

## Building and Running
Tested on Windows 11 24H2, OS Build 26100.7840, .NET version 9.0.304. Windows is currently the only supported target platform.
Note: Discord and RPCParadox must be open before launching your game.

1. Clone the repository:
    ```bash
    git clone github.com/DairyProducts/RPCParadox.git
    ```
2. Make sure you have the .NET SDK installed. You can download it from the [official .NET website](https://dotnet.microsoft.com/download).

3. Build the project using the .NET CLI:
   ```bash
   dotnet build
   ```
   Do this once you have selected the correct directory for the project, which is `RPCParadox`. This will restore the necessary dependencies and compile the application.

4. Run the application, which should appear as an exe file in the `bin/Debug/net9.0` directory. You can also run it directly from the command line:
   ```bash
   dotnet run
   ```


## Contributing
Feel free to submit issues or fork the repository and submit pull requests. Please ensure that your code includes appropriate documentation. I do plan on eventually implementing all the planned features, but contributions are welcome as they can help speed up the process.

## Disclaimer
This project is not affiliated with or endorsed by Paradox Interactive or Discord. It is solely developed for the purpose of educational and personal use, without the intent of commercial gain. All trademarks and copyrights belong to their respective owners.

## License
This project is licensed under the GPL license. See the [LICENSE](LICENSE) file for details.
This project uses packages and dependencies that are licensed under the MIT License. See the [NOTICE](NOTICE) file for details.
