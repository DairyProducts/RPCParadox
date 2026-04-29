/*
    HOI4Handler.cs - Game handler for Hearts of Iron IV
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using RPCParadox.Memory;

namespace RPCParadox.GameHandlers;

internal sealed class HOI4Handler : IGameHandler
{
    private const string DISCORD_APP_ID = "PLACEHOLDER_HOI4_APP_ID";
    private const string LARGE_IMAGE    = "hoi4";
    private const string? SMALL_IMAGE  = null;

    private readonly HOI4MemoryScanner _scanner;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _updateThread;
    private readonly Lock _lock = new();

    private string _statusLine1 = "Leading a Nation";
    private string _statusLine2 = "";
    private volatile bool _disposed;

    public string DiscordAppId  => DISCORD_APP_ID;
    public string GameName      => "Hearts of Iron IV";
    public string LargeImageKey => LARGE_IMAGE;
    public string? SmallImageKey => SMALL_IMAGE;

    public HOI4Handler()
    {
        _scanner = new HOI4MemoryScanner();
        _updateThread = new Thread(UpdateGameDataLoop)
        {
            Name         = "HOI4HandlerUpdate",
            IsBackground = true
        };
        _updateThread.Start();
    }

    private void UpdateGameDataLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            string? gameDate = _scanner.GetGameDate();

            lock (_lock)
            {
                if (gameDate != null)
                {
                    _statusLine1 = "In Game";
                    _statusLine2 = $"Date: {gameDate}";
                }
                else
                {
                    _statusLine1 = "In the Main Menu";
                    _statusLine2 = "";
                }
            }

            try
            {
                Task.Delay(5000, _cts.Token).Wait();
            }
            catch (AggregateException)
            {
                break;
            }
        }
    }

    public (string Line1, string Line2) GetStatus()
    {
        lock (_lock)
        {
            return (_statusLine1, _statusLine2);
        }
    }

    public bool IsRunning => _scanner.IsProcessRunning;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cts.Cancel();

        if (_updateThread.IsAlive)
            _updateThread.Join(3000);

        _scanner.Dispose();
        _cts.Dispose();

        Console.WriteLine("[HOI4Handler] Disposed");
    }
}
