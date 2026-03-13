/*
    HOI4Handler.cs - Game handler for Hearts of Iron IV
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

namespace RPCParadox.GameHandlers;

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
