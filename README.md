# RPCParadox
This is a project that implements Discord Rich Presence for Paradox Interactive games using Java. It is still early in development and many intended features are not yet implemented.

## Currently Supported Games
- Stellaris
- Heart of Iron IV

## Current Features
- Basic Rich Presence functionality (game name, time played, icon)
- Automatic detection of supported Paradox games
- Fun toast message on the RPC card

## Planned Features
- More detailed Rich Presence information (e.g., current played country, in-game date, war status)
- Support for additional Paradox games

## Dependencies
- [Discord Game SDK for Java](https://github.com/JnCrMx/discord-game-sdk4j) - tested with version 1.0.0
- [Gson](https://github.com/google/gson) - tested with version 2.10.1

## Possible Future Dependencies
- [JNA](https://mvnrepository.com/artifact/net.java.dev.jna/jna/5.14.0) - Tested with version 5.14.0, may be used for advanced features like reading game memory.
- [JNA Platform](https://mvnrepository.com/artifact/net.java.dev.jna/jna-platform/5.14.0) - Tested with version 5.14.0, may be used for advanced features like reading game memory.

## Building and Running
Tested on Windows 11 version 24H2 with Java SE 21.0.4 2024-07-16 LTS.

1. Clone the repository.
2. Ensure you have the required.
3. Compile and build the project using your preferred Java IDE or build tool, ensuring all dependencies and source files are included in the classpath. Alternatively, you can use the command line to compile and run the project. Make sure to replace `path\\to\\` with the actual paths to your files.
```
java -cp "path\\to\\bin\\folder;path\\to\\jna-5.14.0.jar;path\\to\\discord-game-sdk4j-1.0.0.jar;path\\to\\jna-platform-5.14.0.jar;path\\to\\gson-2.10.1.jar"
```

## Contributing
Feel free to submit issues or fork the repository and submit pull requests. Please ensure that your code includes appropriate documentation. I do plan on eventually implementing all the planned features, but contributions are welcome as they can help speed up the process.

## Disclaimer
This project is not affiliated with or endorsed by Paradox Interactive or Discord. It is solely developed for the purpose of educational and personal use, without the intent of commercial gain. All trademarks and copyrights belong to their respective owners.

## License
This project is licensed under the GPL license. See the [LICENSE](LICENSE) file for details.
See also the [NOTICE](NOTICE.md) file for details on third-party licenses.