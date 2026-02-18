/*
    IGameHandler.cs - Interface for game handlers
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace RPCParadox2.GameHandlers;

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
    string ?SmallImageKey { get; }

    /// <summary>
    /// Gets the current status lines for Discord Rich Presence
    /// </summary>
    (string Line1, string Line2) GetStatus();

    /// <summary>
    /// Returns true if the game process is still running
    /// </summary>
    bool IsRunning { get; }
}