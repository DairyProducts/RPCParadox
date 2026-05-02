/*
    StellarisMemoryScanner.cs - Memory scanner for tracking game date in Stellaris
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RPCParadox.Memory;

/// <summary>
/// Resolves a stable pointer chain to read the current in-game epoch from Stellaris
/// and converts it to a date string. Runs a background polling thread.
/// </summary>
internal sealed class StellarisMemoryScanner : IDisposable
{
    #region Native Methods

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(
        IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint   AllocationProtect;
        public IntPtr RegionSize;
        public uint   State;
        public uint   Protect;
        public uint   Type;
    }

    private const uint PROCESS_VM_READ          = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    #endregion

    #region Pointer Chain & Epoch Conversion

    // Single chain: (static offset from module base, pointer offsets to follow)
    // Each offset is a dereference + add; the final address holds the 32-bit epoch int.
    private const long ChainBaseOffset = 0x308C660;
    private const long ChainOffset0    = 0xC0;

    // Epoch 62808000 = 2200.01.01; each day advances the epoch by 24.
    // A year is 360 days (12 months × 30 days).
    private const int EpochBase = 62808000;
    private const int EpochDay  = 24;
    private const int YearDays  = 360;
    private const int MonthDays = 30;
    private const int BaseYear  = 2200;

    private static string? EpochToDate(int epoch)
    {
        if (epoch < EpochBase) return null;

        int days      = (epoch - EpochBase) / EpochDay;
        int year      = BaseYear + days / YearDays;
        int remaining = days % YearDays;
        int month     = 1 + remaining / MonthDays;
        int day       = 1 + remaining % MonthDays;

        if (year > 9999) return null;

        return $"{year:D4}.{month:D2}.{day:D2}";
    }

    #endregion

    #region Fields

    private const int PollIntervalMs  = 3000;
    private const int RetryIntervalMs = 2000;

    private readonly Lock _lock        = new();
    private readonly Lock _processLock = new();
    private readonly Thread _scannerThread;
    private readonly CancellationTokenSource _cts = new();
    private readonly byte[] _pointerBuffer = new byte[8];
    private readonly byte[] _intBuffer     = new byte[4];

    private IntPtr _processHandle = IntPtr.Zero;
    private IntPtr _moduleBase    = IntPtr.Zero;
    private int    _processId;
    private int    _disposedFlag;

    private string? _currentDate;
    #endregion

    #region Constructor

    public StellarisMemoryScanner()
    {
        _scannerThread = new Thread(ScannerLoop)
        {
            Name         = "StellarisMemoryScanner",
            Priority     = ThreadPriority.BelowNormal,
            IsBackground = true,
        };
        _scannerThread.Start();
        Console.WriteLine("[StellarisMemoryScanner] Started");
    }

    #endregion

    #region Public API

    internal string? GetGameDate()
    {
        lock (_lock) return _currentDate;
    }

    internal bool IsTracking
    {
        get { lock (_lock) return _currentDate != null; }
    }

    internal bool IsProcessRunning
    {
        get
        {
            int pid;
            lock (_processLock)
            {
                if (_processHandle == IntPtr.Zero) return false;
                pid = _processId;
            }

            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region Scanner Thread

    private void ScannerLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (!EnsureProcessAttached())
                {
                    ClearTrackedData();
                    Thread.Sleep(RetryIntervalMs);
                    continue;
                }

                string? date = ResolvePointerChain();

                lock (_lock) _currentDate = date;

                Thread.Sleep(PollIntervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StellarisMemoryScanner] Error: {ex.Message}");
                ClearTrackedData();
                Thread.Sleep(RetryIntervalMs);
            }
        }
    }

    #endregion

    #region Pointer Chain Resolution

    private string? ResolvePointerChain()
    {
        IntPtr handle, moduleBase;
        lock (_processLock)
        {
            handle     = _processHandle;
            moduleBase = _moduleBase;
        }

        if (handle == IntPtr.Zero || moduleBase == IntPtr.Zero) return null;

        bool wasTracking;
        lock (_lock) wasTracking = _currentDate != null;

        string? date = TryChain(handle, moduleBase);
        if (date != null)
        {
            if (!wasTracking)
            {
                Console.WriteLine($"[StellarisMemoryScanner] Resolved date: {date}");
            }
            return date;
        }

        if (wasTracking)
        {
            Console.WriteLine("[StellarisMemoryScanner] No chain resolved, player may be in the menu");
        }

        return null;
    }

    private string? TryChain(IntPtr handle, IntPtr moduleBase)
    {
        nint addr = moduleBase + (nint)ChainBaseOffset;

        if (!ReadPointer(handle, addr, out IntPtr ptr)) return null;
        addr = ptr + (nint)ChainOffset0;

        int? epoch = ReadInt32(handle, addr);
        if (epoch == null) return null;

        return EpochToDate(epoch.Value);
    }

    private bool ReadPointer(IntPtr handle, IntPtr address, out IntPtr result)
    {
        result = IntPtr.Zero;
        if (!ReadProcessMemory(handle, address, _pointerBuffer, 8, out int bytesRead) || bytesRead != 8)
            return false;
        result = new IntPtr(BitConverter.ToInt64(_pointerBuffer, 0));
        return result != IntPtr.Zero;
    }

    private int? ReadInt32(IntPtr handle, IntPtr address)
    {
        if (!ReadProcessMemory(handle, address, _intBuffer, 4, out int bytesRead) || bytesRead != 4)
            return null;
        return BitConverter.ToInt32(_intBuffer, 0);
    }

    #endregion

    #region Process Management

    private bool EnsureProcessAttached()
    {
        lock (_processLock)
        {
            if (_processHandle != IntPtr.Zero)
            {
                if (VirtualQueryEx(_processHandle, IntPtr.Zero, out _, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != 0)
                    return true;

                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                _moduleBase    = IntPtr.Zero;
            }

            Process[] processes = Process.GetProcessesByName("stellaris");
            try
            {
                if (processes.Length == 0) return false;

                Process p  = processes[0];
                _processId = p.Id;
                _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _processId);

                if (_processHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[StellarisMemoryScanner] Failed to open process");
                    return false;
                }

                _moduleBase = p.MainModule?.BaseAddress ?? IntPtr.Zero;

                if (_moduleBase == IntPtr.Zero)
                {
                    Console.WriteLine("[StellarisMemoryScanner] Failed to read module base");
                    CloseHandle(_processHandle);
                    _processHandle = IntPtr.Zero;
                    return false;
                }

                Console.WriteLine($"[StellarisMemoryScanner] Attached to Stellaris (PID: {_processId}, base: 0x{_moduleBase:X})");
                return true;
            }
            finally
            {
                foreach (var proc in processes) proc.Dispose();
            }
        }
    }

    private void ClearTrackedData()
    {
        lock (_lock) _currentDate = null;

        lock (_processLock)
        {
            if (_processHandle == IntPtr.Zero) return;
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
            _moduleBase    = IntPtr.Zero;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return;

        Console.WriteLine("[StellarisMemoryScanner] Disposing...");

        _cts.Cancel();
        if (_scannerThread.IsAlive) _scannerThread.Join(3000);
        ClearTrackedData();
        _cts.Dispose();

        GC.SuppressFinalize(this);

        Console.WriteLine("[StellarisMemoryScanner] Disposed");
    }

    #endregion
}
