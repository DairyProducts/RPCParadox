/*
    HOI4MemoryScanner.cs - Memory scanner for tracking game date in Hearts of Iron IV
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RPCParadox.Memory;

/// <summary>
/// Memory scanner for the Hearts of Iron IV game process.
/// Runs a background thread to find and track game data.
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
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    private const uint PROCESS_VM_READ         = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT              = 0x1000;
    private const uint MEM_PRIVATE             = 0x20000;
    private const uint PAGE_READWRITE          = 0x04;

    #endregion

    #region Fields

    private const int ScanIntervalMs       = 1000;
    private const int TrackIntervalMs      = 3000;
    private const int ValidationDurationMs = 10000;

    // HOI4 dates are YYYY.M.D (min 8) to YYYY.MM.DD (max 10); read 2 extra bytes to detect trailing digits
    private const int MaxDateLength    = 10;
    private const int MinDateLength    = 8;
    private const int ReadBufferLength = MaxDateLength + 2;

    private const int ScanChunkSizeBytes = 524288; // 512 KB per burst

    private readonly object _lock        = new();
    private readonly object _processLock = new();
    private readonly Thread _scannerThread;
    private readonly CancellationTokenSource _cts = new();
    private readonly byte[] _pollBuffer = new byte[ReadBufferLength];

    private IntPtr _processHandle = IntPtr.Zero;
    private int    _processId;
    private int    _disposedFlag;

    private IntPtr  _dateAddress = IntPtr.Zero;
    private string? _currentDate;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new HOI4 memory scanner and starts the background scanning thread.
    /// </summary>
    public HOI4MemoryScanner()
    {
        _scannerThread = new Thread(ScannerLoop)
        {
            Name         = "HOI4MemoryScanner",
            Priority     = ThreadPriority.BelowNormal,
            IsBackground = true
        };
        _scannerThread.Start();

        Console.WriteLine("[HOI4MemoryScanner] Started background scanner thread");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the current tracked game date formatted as YYYY.MM.DD, or null if not available.
    /// </summary>
    internal string? GetGameDate()
    {
        lock (_lock) return _currentDate;
    }

    /// <summary>
    /// Returns true if the scanner is currently tracking a valid date address.
    /// </summary>
    internal bool IsTracking
    {
        get { lock (_lock) return _dateAddress != IntPtr.Zero && _currentDate != null; }
    }

    /// <summary>
    /// Returns true if the HOI4 process is still running (soft check).
    /// </summary>
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
                    Thread.Sleep(ScanIntervalMs * 2);
                    continue;
                }

                IntPtr currentAddress;
                lock (_lock) currentAddress = _dateAddress;

                if (currentAddress != IntPtr.Zero)
                {
                    var result = ReadDateAt(currentAddress);

                    if (result.HasValue)
                    {
                        lock (_lock) _currentDate = result.Value.formatted;
                    }
                    else
                    {
                        Console.WriteLine("[HOI4MemoryScanner] Tracked address invalid, rescanning...");
                        lock (_lock) { _dateAddress = IntPtr.Zero; _currentDate = null; }
                    }

                    Thread.Sleep(TrackIntervalMs);
                }
                else
                {
                    ScanForDateAddress();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HOI4MemoryScanner] Error: {ex.Message}");
                ClearTrackedData();
                Thread.Sleep(ScanIntervalMs * 2);
            }
        }
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
            }

            Process[] processes = Process.GetProcessesByName("hoi4");
            try
            {
                if (processes.Length == 0) return false;

                _processId     = processes[0].Id;
                _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _processId);

                if (_processHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[HOI4MemoryScanner] Failed to open process");
                    return false;
                }

                Console.WriteLine($"[HOI4MemoryScanner] Attached to HOI4 (PID: {_processId})");
                return true;
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
    }

    private void ClearTrackedData()
    {
        lock (_lock) { _dateAddress = IntPtr.Zero; _currentDate = null; }

        lock (_processLock)
        {
            if (_processHandle == IntPtr.Zero) return;
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }

    #endregion

    #region Memory Scanning

    private void ScanForDateAddress()
    {
        Console.WriteLine("[HOI4MemoryScanner] Scanning for date address...");

        var candidates = new Dictionary<IntPtr, int>(); // address -> dateInt (year*10000 + month*100 + day)
        var buffer     = new byte[ScanChunkSizeBytes];
        IntPtr address = IntPtr.Zero;

        DateTime lastProgressReport  = DateTime.MinValue;
        DateTime lastEarlyValidation = DateTime.MinValue;
        long totalBytesScanned = 0;

        while (!_cts.Token.IsCancellationRequested)
        {
            if (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                break;

            DateTime now = DateTime.UtcNow;

            if ((now - lastProgressReport).TotalMilliseconds >= 1000)
            {
                Console.WriteLine($"[HOI4MemoryScanner] Scanning... {totalBytesScanned / (1024 * 1024)}MB scanned (found {candidates.Count} candidates)");
                lastProgressReport = now;
            }

            if (candidates.Count > 0 && (now - lastEarlyValidation).TotalMilliseconds >= 3000)
            {
                lastEarlyValidation = now;
                IntPtr earlyResult = TryValidateCandidates(candidates);
                if (earlyResult != IntPtr.Zero)
                {
                    var date = ReadDateAt(earlyResult);
                    lock (_lock)
                    {
                        _dateAddress = earlyResult;
                        _currentDate = date?.formatted;
                    }
                    Console.WriteLine($"[HOI4MemoryScanner] Early match at 0x{earlyResult:X}: {date?.formatted} ({totalBytesScanned / (1024 * 1024)}MB into scan)");
                    return;
                }
            }

            if (mbi.State == MEM_COMMIT && mbi.Type == MEM_PRIVATE && mbi.Protect == PAGE_READWRITE)
            {
                long regionSize = (long)mbi.RegionSize;
                totalBytesScanned += regionSize;
                ScanRegionForDates(mbi.BaseAddress, (int)Math.Min(regionSize, int.MaxValue), candidates, buffer);
            }

            long nextAddress = (long)mbi.BaseAddress + (long)mbi.RegionSize;
            if (nextAddress <= (long)address) break;

            address = (IntPtr)nextAddress;
        }

        Console.WriteLine($"[HOI4MemoryScanner] Scan complete: {totalBytesScanned / (1024 * 1024)}MB scanned, {candidates.Count} candidates found");

        if (candidates.Count == 0)
        {
            Console.WriteLine("[HOI4MemoryScanner] No date candidates found");
            return;
        }

        Console.WriteLine($"[HOI4MemoryScanner] Validating {candidates.Count} remaining candidates...");
        IntPtr validatedAddress = ValidateCandidates(candidates);

        if (validatedAddress != IntPtr.Zero)
        {
            var date = ReadDateAt(validatedAddress);
            lock (_lock) { _dateAddress = validatedAddress; _currentDate = date?.formatted; }
            Console.WriteLine($"[HOI4MemoryScanner] Tracking date at 0x{validatedAddress:X}: {date?.formatted}");
        }
        else
        {
            Console.WriteLine("[HOI4MemoryScanner] No valid date address found");
        }
    }

    /// <summary>
    /// Single-pass check of existing candidates for date progression.
    /// Returns the first candidate whose date has advanced, or IntPtr.Zero if none have.
    /// </summary>
    private IntPtr TryValidateCandidates(Dictionary<IntPtr, int> candidates)
    {
        var toRemove = new List<IntPtr>();
        IntPtr result = IntPtr.Zero;

        foreach (var (addr, originalDateInt) in candidates)
        {
            var current = ReadDateAt(addr);

            if (current == null)
            {
                toRemove.Add(addr);
                continue;
            }

            if (result == IntPtr.Zero && current.Value.dateInt > originalDateInt)
            {
                Console.WriteLine($"[HOI4MemoryScanner] Candidate 0x{addr:X} progressed: {FormatDateInt(originalDateInt)} -> {current.Value.formatted}");
                result = addr;
            }
        }

        foreach (var addr in toRemove) candidates.Remove(addr);

        return result;
    }

    /// <summary>
    /// Scans a memory region for date pattern matches.
    /// Yields to the scheduler after each chunk.
    /// </summary>
    private void ScanRegionForDates(IntPtr baseAddress, int regionSize, Dictionary<IntPtr, int> candidates, byte[] buffer)
    {
        const int overlap = MaxDateLength - 1;
        int offset = 0;

        while (offset < regionSize)
        {
            if (_cts.Token.IsCancellationRequested) return;

            int bytesToRead    = Math.Min(buffer.Length, regionSize - offset);
            IntPtr readAddress = IntPtr.Add(baseAddress, offset);

            if (!ReadProcessMemory(_processHandle, readAddress, buffer, bytesToRead, out int bytesRead) || bytesRead == 0)
            {
                offset += bytesToRead;
                continue;
            }

            for (int i = 0; i <= bytesRead - MinDateLength; i++)
            {
                // Quick pre-check: dot at position 4 (end of 4-digit year)
                if (buffer[i + 4] != '.') continue;

                var result = TryParseDate(buffer, i, bytesRead - i);
                if (result.HasValue)
                    candidates[IntPtr.Add(readAddress, i)] = result.Value.dateInt;
            }

            offset += bytesToRead - (bytesToRead < buffer.Length ? 0 : overlap);

            Thread.Yield();
        }
    }

    /// <summary>
    /// Validates candidates by polling until one shows date progression.
    /// Returns IntPtr.Zero if no candidate advances (game paused or not in-game).
    /// </summary>
    private IntPtr ValidateCandidates(Dictionary<IntPtr, int> candidates)
    {
        if (candidates.Count == 0) return IntPtr.Zero;

        Console.WriteLine($"[HOI4MemoryScanner] Validating {candidates.Count} candidates...");

        int checksRemaining = ValidationDurationMs / ScanIntervalMs;
        var toRemove = new List<IntPtr>();

        while (checksRemaining > 0 && !_cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(ScanIntervalMs);
            checksRemaining--;

            toRemove.Clear();

            foreach (var (addr, originalDateInt) in candidates)
            {
                var current = ReadDateAt(addr);

                if (current == null)
                {
                    toRemove.Add(addr);
                    continue;
                }

                if (current.Value.dateInt > originalDateInt)
                {
                    Console.WriteLine($"[HOI4MemoryScanner] Candidate 0x{addr:X} progressed: {FormatDateInt(originalDateInt)} -> {current.Value.formatted}");
                    return addr;
                }
            }

            foreach (var addr in toRemove) candidates.Remove(addr);
        }

        Console.WriteLine("[HOI4MemoryScanner] No candidate showed date progression, game may be paused or not in-game");
        return IntPtr.Zero;
    }

    #endregion

    #region Memory Reading

    /// <summary>
    /// Reads and parses a HOI4 date from the given process address.
    /// Returns null if the bytes at that address no longer form a valid date.
    /// </summary>
    private (int dateInt, string formatted)? ReadDateAt(IntPtr address)
    {
        if (!ReadProcessMemory(_processHandle, address, _pollBuffer, ReadBufferLength, out int bytesRead) || bytesRead < MinDateLength)
            return null;

        var result = TryParseDate(_pollBuffer, 0, bytesRead);
        if (!result.HasValue) return null;
        return (result.Value.dateInt, result.Value.formatted);
    }

    #endregion

    #region Date Parsing

    /// <summary>
    /// Tries to parse a HOI4 date (YYYY.M.D through YYYY.MM.DD) from a byte buffer.
    /// Returns (dateInt, formatted) where dateInt = year*10000 + month*100 + day,
    /// and formatted is zero-padded as YYYY.MM.DD.
    /// Returns null if the bytes at the given offset do not form a valid HOI4 date.
    /// </summary>
    private static (int dateInt, string formatted)? TryParseDate(byte[] buf, int offset, int available)
    {
        if (available < MinDateLength) return null;

        // Year: exactly 4 digits
        for (int i = 0; i < 4; i++)
            if (buf[offset + i] < '0' || buf[offset + i] > '9') return null;
        if (buf[offset + 4] != '.') return null;

        int year = (buf[offset + 0] - '0') * 1000 + (buf[offset + 1] - '0') * 100
                 + (buf[offset + 2] - '0') * 10   + (buf[offset + 3] - '0');
        if (year < 1900 || year > 2100) return null;

        int pos = 5;

        // Month: 1 or 2 digits
        if (pos >= available || buf[offset + pos] < '0' || buf[offset + pos] > '9') return null;
        int month = buf[offset + pos] - '0';
        pos++;

        if (pos < available && buf[offset + pos] >= '0' && buf[offset + pos] <= '9')
        {
            month = month * 10 + (buf[offset + pos] - '0');
            pos++;
        }

        if (month < 1 || month > 12) return null;
        if (pos >= available || buf[offset + pos] != '.') return null;
        pos++;

        // Day: 1 or 2 digits
        if (pos >= available || buf[offset + pos] < '0' || buf[offset + pos] > '9') return null;
        int day = buf[offset + pos] - '0';
        pos++;

        if (pos < available && buf[offset + pos] >= '0' && buf[offset + pos] <= '9')
        {
            day = day * 10 + (buf[offset + pos] - '0');
            pos++;
        }

        if (day < 1 || day > 31) return null;

        // The byte immediately after the date must not be a digit (avoids substring matches)
        if (pos < available && buf[offset + pos] >= '0' && buf[offset + pos] <= '9') return null;

        int dateInt = year * 10000 + month * 100 + day;
        string formatted = $"{year:D4}.{month:D2}.{day:D2}";
        return (dateInt, formatted);
    }

    private static string FormatDateInt(int dateInt)
    {
        int year  = dateInt / 10000;
        int month = (dateInt % 10000) / 100;
        int day   = dateInt % 100;
        return $"{year:D4}.{month:D2}.{day:D2}";
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
