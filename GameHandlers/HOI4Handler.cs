/*
    HOI4Handler.cs - Game handler for Hearts of Iron IV
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

// TODO: Implement HOI4 handler
internal sealed class HOI4Handler : IGameHandler
{
    public string DiscordAppId => throw new NotImplementedException();
    public string GameName => "Hearts of Iron IV";
    public string LargeImageKey => throw new NotImplementedException();
    public string? SmallImageKey => null;
    public bool IsRunning => throw new NotImplementedException();

    public (string Line1, string Line2) GetStatus() => throw new NotImplementedException();

    public void Dispose() { }
}
