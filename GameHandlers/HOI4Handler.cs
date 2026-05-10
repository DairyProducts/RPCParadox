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
    private const string DISCORD_APP_ID = "1426482535223005217";
    private const string LARGE_IMAGE    = "hoi4";
    private const string? SMALL_IMAGE   = null;

    private readonly HOI4MemoryScanner _scanner;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _updateThread;
    private readonly Lock _lock = new();

    private string _statusLine1 = "Conquering the World";
    private string _statusLine2 = "";
    private volatile bool _disposed;

    public string DiscordAppId   => DISCORD_APP_ID;
    public string GameName       => "Hearts of Iron IV";
    public string LargeImageKey  => LARGE_IMAGE;
    public string? SmallImageKey => SMALL_IMAGE;

    private static readonly string[] MonthNames =
    [
        "January", "February", "March",     "April",   "May",      "June",
        "July",    "August",   "September", "October", "November", "December"
    ];

    // Input:  "YYYY.MM.DD.HH"
    // Output: "DD Month YYYY, HH:00"
    private static string FormatDate(string raw)
    {
        if (raw.Length != 13) return raw;

        string year  = raw[..4];
        string month = raw[5..7];
        string day   = raw[8..10];
        string hour  = raw[11..13];

        if (!int.TryParse(month, out int monthIndex) || monthIndex < 1 || monthIndex > 12)
            return raw;

        return $"{day} {MonthNames[monthIndex - 1]} {year}, {hour}:00";
    }

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
                    _statusLine2 = $"Date: {FormatDate(gameDate)}";
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
        {
            _updateThread.Join(3000);
        }

        _scanner.Dispose();
        _cts.Dispose();

        Console.WriteLine("[HOI4Handler] Disposed");
    }
}
