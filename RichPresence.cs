/*
    RichPresence.cs - Manages Discord Rich Presence for supported games.
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Logging;
using RPCParadox.GameHandlers;

namespace RPCParadox;

/// <summary>
/// Manages Discord Rich Presence for supported games.
/// </summary>
internal sealed class RichPresence : IDisposable
{
    // Game detection interval when no game is running (in milliseconds)
    private const int GAME_CHECK_INTERVAL_MS = 10000;
    
    // Status update interval when a game is running (in milliseconds)
    private const int STATUS_UPDATE_INTERVAL_MS = 5000;

    // Supported game handlers - process name for detection, factory to create handler when game is found
    private static readonly (string ProcessName, Func<IGameHandler> Factory)[] GameHandlers =
    [
        ("stellaris", () => new StellarisHandler()),
        // ("hoi4", () => new HOI4Handler()), // Uncomment when HOI4Handler is implemented
    ];

    private DiscordRpcClient? _client;
    private IGameHandler? _currentHandler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _mainLoopThread;
    private readonly object _lock = new();
    private volatile bool _disposed;
    private DateTime _gameStartTime;
    private readonly Action<string, string>? _onNotify;

    public RichPresence(Action<string, string>? onNotify = null)
    {
        _onNotify = onNotify;
        _mainLoopThread = new Thread(MainLoop)
        {
            Name = "RichPresenceMainLoop",
            IsBackground = true
        };
    }

    /// <summary>
    /// Starts the Rich Presence handler.
    /// </summary>
    public void Start()
    {
        _mainLoopThread.Start();
        Console.WriteLine("[RichPresence] Started");
    }

    private void InitializeDiscordClient(string appId)
    {
        // Dispose existing client if switching games
        if (_client != null)
        {
            _client.ClearPresence();
            _client.Dispose();
            _client = null;
        }
        
        _client = new DiscordRpcClient(appId)
        {
            Logger = new ConsoleLogger { Level = LogLevel.Warning }
        };

        _client.OnReady += (sender, e) =>
        {
            Console.WriteLine($"[RichPresence] Connected to Discord as {e.User.Username}");
        };

        _client.OnPresenceUpdate += (sender, e) =>
        {
            Console.WriteLine("[RichPresence] Presence updated");
        };

        _client.OnError += (sender, e) =>
        {
            Console.WriteLine($"[RichPresence] Error: {e.Message}");
        };

        _client.Initialize();
    }

    private void MainLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                IGameHandler? handler;
                lock (_lock)
                {
                    handler = _currentHandler;
                }

                if (handler == null)
                {
                    DetectAndAttachGame();
                    
                    lock (_lock)
                    {
                        handler = _currentHandler;
                    }
                    
                    if (handler == null)
                    {
                        ClearPresence();
                        Thread.Sleep(GAME_CHECK_INTERVAL_MS);
                    }
                }
                else
                {
                    if (handler.IsRunning)
                    {
                        UpdatePresence();
                        Thread.Sleep(STATUS_UPDATE_INTERVAL_MS);
                    }
                    else
                    {
                        Console.WriteLine("[RichPresence] Game stopped running, disposing handler");
                        string gameName = handler.GameName;
                        DisposeCurrentHandler();
                        _onNotify?.Invoke("Game Closed", $"Detached from {gameName}");
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RichPresence] Error in main loop: {ex.Message}");
                Thread.Sleep(GAME_CHECK_INTERVAL_MS);
            }
        }
    }

    private void DetectAndAttachGame()
    {
        foreach (var (processName, handlerFactory) in GameHandlers)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    Console.WriteLine($"[RichPresence] Detected game: {processName}");
                    
                    foreach (var p in processes)
                    {
                        p.Dispose();
                    }
                    
                    string appId;
                    string gameName;
                    lock (_lock)
                    {
                        _currentHandler = handlerFactory();
                        _gameStartTime = DateTime.UtcNow;
                        appId = _currentHandler.DiscordAppId;
                        gameName = _currentHandler.GameName;
                    }
                    
                    InitializeDiscordClient(appId);
                    _onNotify?.Invoke("Game Detected", $"Now attached to {gameName}");
                    
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RichPresence] Error detecting game: {ex.Message}");
            }
        }
    }

    private void UpdatePresence()
    {
        if (_client == null || _currentHandler == null)
            return;

        var status = _currentHandler.GetStatus();

        var presence = new DiscordRPC.RichPresence
        {
            Details = status.Line1,
            State = status.Line2,
            Timestamps = new Timestamps
            {
                Start = _gameStartTime
            },
            Assets = new Assets
            {
                LargeImageKey = _currentHandler.LargeImageKey,
                LargeImageText = _currentHandler.GameName
            }
        };
        
        // Small image is optional
        if (!string.IsNullOrEmpty(_currentHandler.SmallImageKey))
        {
            presence.Assets.SmallImageKey = _currentHandler.SmallImageKey;
            presence.Assets.SmallImageText = _currentHandler.GameName;
        }

        _client.SetPresence(presence);
    }

    private void ClearPresence()
    {
        _client?.ClearPresence();
    }

    private void DisposeCurrentHandler()
    {
        lock (_lock)
        {
            if (_currentHandler != null)
            {
                _currentHandler.Dispose();
                _currentHandler = null;
            }
        }
        
        ClearPresence();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        Console.WriteLine("[RichPresence] Disposing...");
        
        _cts.Cancel();
        
        if (_mainLoopThread.IsAlive)
        {
            _mainLoopThread.Join(5000);
        }
        
        DisposeCurrentHandler();
        
        if (_client != null)
        {
            _client.ClearPresence();
            _client.Dispose();
            _client = null;
        }
        
        _cts.Dispose();
        
        Console.WriteLine("[RichPresence] Disposed");
    }
}
