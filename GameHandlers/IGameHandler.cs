/*
    IGameHandler.cs - Interface for game handlers
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

namespace RPCParadox.GameHandlers;

internal interface IGameHandler : IDisposable
{
    /// <summary>
    /// Discord Application ID for this specific game
    /// </summary>
    string DiscordAppId { get; }
    
    /// <summary>
    /// Display name of the game
    /// </summary>
    string GameName { get; }
    
    /// <summary>
    /// Discord large image asset key for this game
    /// </summary>
    string LargeImageKey { get; }
    
    /// <summary>
    /// Discord small image asset key for this game; optional
    /// </summary>
    string? SmallImageKey { get; }

    /// <summary>
    /// Gets the current status lines for Discord Rich Presence
    /// </summary>
    (string Line1, string Line2) GetStatus();

    /// <summary>
    /// Returns true if the game process is still running
    /// </summary>
    bool IsRunning { get; }
}