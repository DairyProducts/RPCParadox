/*
    HOI4MemoryScanner.cs - Memory scanner for tracking game date in Hearts of Iron IV
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RPCParadox.Memory;

/// <summary>
/// Resolves a stable pointer chain to read the current in-game date string from HOI4
/// and validates its format. Runs a background polling thread.
/// </summary>
internal sealed class HOI4MemoryScanner : IDisposable
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

    private const uint PROCESS_VM_READ           = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    #endregion

    #region Pointer Chain & Date Parsing

    // Chain: hoi4.exe+0x34FA628 ->[+0x8]
    // The final address holds the date as a raw ASCII string: "YYYY.MM.DD.HH"
    private const long ChainBaseOffset = 0x34FA628;
    private const long ChainOffset0    = 0x8;

    private const int DateStringLength = 13; // "YYYY.MM.DD.HH"

    // HOI4 holds this date in memory while on the main menu
    private const string MenuSentinelDate = "1936.01.01.12";

    private static string? ValidateDateString(byte[] buf)
    {
        // Expected dots at positions 4, 7, 10; digits everywhere else
        for (int i = 0; i < DateStringLength; i++)
        {
            if (i == 4 || i == 7 || i == 10)
            {
                if (buf[i] != (byte)'.') return null;
            }
            else
            {
                if (buf[i] < (byte)'0' || buf[i] > (byte)'9') return null;
            }
        }

        string date = Encoding.ASCII.GetString(buf, 0, DateStringLength);
        return date == MenuSentinelDate ? null : date;
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
    private readonly byte[] _stringBuffer  = new byte[16];

    private IntPtr _processHandle = IntPtr.Zero;
    private IntPtr _moduleBase    = IntPtr.Zero;
    private int    _processId;
    private int    _disposedFlag;

    private string? _currentDate;

    #endregion

    #region Constructor

    public HOI4MemoryScanner()
    {
        _scannerThread = new Thread(ScannerLoop)
        {
            Name         = "HOI4MemoryScanner",
            Priority     = ThreadPriority.BelowNormal,
            IsBackground = true,
        };
        _scannerThread.Start();
        Console.WriteLine("[HOI4MemoryScanner] Started");
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
                Console.WriteLine($"[HOI4MemoryScanner] Error: {ex.Message}");
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
                Console.WriteLine($"[HOI4MemoryScanner] Resolved date: {date}");
            }
            return date;
        }

        if (wasTracking)
        {
            Console.WriteLine("[HOI4MemoryScanner] No chain resolved, player may be in the menu");
        }

        return null;
    }

    private string? TryChain(IntPtr handle, IntPtr moduleBase)
    {
        nint addr = moduleBase + (nint)ChainBaseOffset;

        if (!ReadPointer(handle, addr, out IntPtr ptr)) return null;
        addr = ptr + (nint)ChainOffset0;

        return ReadDateString(handle, addr);
    }

    private bool ReadPointer(IntPtr handle, IntPtr address, out IntPtr result)
    {
        result = IntPtr.Zero;
        if (!ReadProcessMemory(handle, address, _pointerBuffer, 8, out int bytesRead) || bytesRead != 8)
            return false;
        result = new IntPtr(BitConverter.ToInt64(_pointerBuffer, 0));
        return result != IntPtr.Zero;
    }

    private string? ReadDateString(IntPtr handle, IntPtr address)
    {
        if (!ReadProcessMemory(handle, address, _stringBuffer, _stringBuffer.Length, out int bytesRead)
            || bytesRead < DateStringLength)
            return null;

        return ValidateDateString(_stringBuffer);
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

            Process[] processes = Process.GetProcessesByName("hoi4");
            try
            {
                if (processes.Length == 0) return false;

                Process p  = processes[0];
                _processId = p.Id;
                _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _processId);

                if (_processHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[HOI4MemoryScanner] Failed to open process");
                    return false;
                }

                _moduleBase = p.MainModule?.BaseAddress ?? IntPtr.Zero;

                if (_moduleBase == IntPtr.Zero)
                {
                    Console.WriteLine("[HOI4MemoryScanner] Failed to read module base");
                    CloseHandle(_processHandle);
                    _processHandle = IntPtr.Zero;
                    return false;
                }

                Console.WriteLine($"[HOI4MemoryScanner] Attached to HOI4 (PID: {_processId}, base: 0x{_moduleBase:X})");
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

        Console.WriteLine("[HOI4MemoryScanner] Disposing...");

        _cts.Cancel();
        if (_scannerThread.IsAlive) _scannerThread.Join(3000);
        ClearTrackedData();
        _cts.Dispose();

        GC.SuppressFinalize(this);

        Console.WriteLine("[HOI4MemoryScanner] Disposed");
    }

    #endregion
}
