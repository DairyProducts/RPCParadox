/*
    StellarisMemoryScanner.cs - Memory scanner for tracking game date in Stellaris
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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace RPCParadox2.Memory;

/// <summary>
/// Memory scanner for the Stellaris game process.
/// Runs a background thread to find and track game data.
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
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_READABLE = 0x02 | 0x04 | 0x20 | 0x40; // PAGE_READONLY | PAGE_READWRITE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE

    #endregion

    #region Fields

    private readonly object _lock = new();
    private readonly object _processLock = new();
    private readonly Thread _scannerThread;
    private readonly CancellationTokenSource _cts = new();

    private IntPtr _processHandle = IntPtr.Zero;
    private int _processId;
    private volatile bool _disposed;

    private IntPtr _dateAddress = IntPtr.Zero;
    private string? _currentDate;

    // Date pattern for Stellaris: YYYY.MM.DD
    private static readonly Regex DatePattern = new(@"^\d{4}\.\d{2}\.\d{2}$", RegexOptions.Compiled);

    private const int ScanIntervalMs = 1000;
    private const int ValidationDurationMs = 10000;
    private const int DateStringLength = 10;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new Stellaris memory scanner and starts the background scanning thread.
    /// </summary>
    public StellarisMemoryScanner()
    {
        _scannerThread = new Thread(ScannerLoop)
        {
            Name = "StellarisMemoryScanner",
            Priority = ThreadPriority.BelowNormal,
            IsBackground = true
        };
        _scannerThread.Start();

        Console.WriteLine("[StellarisMemoryScanner] Started background scanner thread");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the current tracked game date, or null if not available.
    /// </summary>
    internal string? GetGameDate()
    {
        lock (_lock)
        {
            return _currentDate;
        }
    }

    /// <summary>
    /// Returns true if the scanner is currently tracking a valid date address.
    /// </summary>
    internal bool IsTracking
    {
        get
        {
            lock (_lock)
            {
                return _dateAddress != IntPtr.Zero && _currentDate != null;
            }
        }
    }

    /// <summary>
    /// Returns true if the Stellaris process is still running (soft check).
    /// </summary>
    internal bool IsProcessRunning
    {
        get
        {
            lock (_processLock)
            {
                if (_processHandle == IntPtr.Zero)
                    return false;

                try
                {
                    using var process = Process.GetProcessById(_processId);
                    return !process.HasExited && process.ProcessName.Equals("stellaris", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
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
                lock (_lock)
                {
                    currentAddress = _dateAddress;
                }

                if (currentAddress != IntPtr.Zero)
                {
                    string? date = ReadStringAt(currentAddress, DateStringLength);
                    
                    if (date != null && IsValidDate(date))
                    {
                        lock (_lock)
                        {
                            _currentDate = date;
                        }
                    }
                    else
                    {
                        Console.WriteLine("[StellarisMemoryScanner] Tracked address invalid, rescanning...");
                        lock (_lock)
                        {
                            _dateAddress = IntPtr.Zero;
                            _currentDate = null;
                        }
                    }
                }
                else
                {
                    ScanForDateAddress();
                }

                Thread.Sleep(ScanIntervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StellarisMemoryScanner] Error: {ex.Message}");
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
            try
            {
                using var process = Process.GetProcessById(_processId);
                if (!process.HasExited && process.ProcessName.Equals("stellaris", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Process no longer exists
            }

            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }

        Process[] processes = Process.GetProcessesByName("stellaris");
        try
        {
            if (processes.Length == 0)
            {
                return false;
            }

            var stellaris = processes[0];
            _processId = stellaris.Id;
            _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _processId);

            if (_processHandle == IntPtr.Zero)
            {
                Console.WriteLine("[StellarisMemoryScanner] Failed to open process");
                return false;
            }

            Console.WriteLine($"[StellarisMemoryScanner] Attached to Stellaris (PID: {_processId})");
            return true;
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
        }
    }

    private void ClearTrackedData()
    {
        lock (_lock)
        {
            _dateAddress = IntPtr.Zero;
            _currentDate = null;
        }

        lock (_processLock)
        {
            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
        }
    }

    #endregion

    #region Memory Scanning

    /// <summary>
    /// Scans memory for date candidates and validates them over time.
    /// This will take quite a long time on first scan.
    /// </summary>
    private void ScanForDateAddress()
    {
        Console.WriteLine("[StellarisMemoryScanner] Scanning for date address...");

        var candidates = new Dictionary<IntPtr, string>();
        IntPtr address = IntPtr.Zero;

        const int chunkSize = 1048576; // 1MB chunks
        byte[] buffer = new byte[chunkSize];

        // For progress reporting and early validation
        DateTime lastProgressReport = DateTime.MinValue;
        DateTime lastEarlyValidation = DateTime.MinValue;
        const int progressIntervalMs = 1000;
        const int earlyValidationIntervalMs = 3000;
        long totalBytesScanned = 0;

        while (!_cts.Token.IsCancellationRequested)
        {
            if (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                break;

            DateTime now = DateTime.UtcNow;

            // Report progress every second
            if ((now - lastProgressReport).TotalMilliseconds >= progressIntervalMs)
            {
                Console.WriteLine($"[StellarisMemoryScanner] Scanning... {totalBytesScanned / (1024 * 1024)}MB scanned (found {candidates.Count} candidates)");
                lastProgressReport = now;
            }

            // Periodically try to validate existing candidates during the scan
            if (candidates.Count > 0 && (now - lastEarlyValidation).TotalMilliseconds >= earlyValidationIntervalMs)
            {
                lastEarlyValidation = now;
                var earlyResult = TryValidateCandidates(candidates);
                if (earlyResult != IntPtr.Zero)
                {
                    string? date = ReadStringAt(earlyResult, DateStringLength);
                    lock (_lock)
                    {
                        _dateAddress = earlyResult;
                        _currentDate = date;
                    }
                    Console.WriteLine($"[StellarisMemoryScanner] Early match at 0x{earlyResult:X}: {date} ({totalBytesScanned / (1024 * 1024)}MB into scan)");
                    return;
                }
            }

            // Only scan committed, readable memory
            if (mbi.State == MEM_COMMIT && (mbi.Protect & PAGE_READABLE) != 0)
            {
                long regionSize = (long)mbi.RegionSize;
                totalBytesScanned += regionSize;

                ScanRegionForDates(mbi.BaseAddress, (int)Math.Min(regionSize, int.MaxValue), candidates, buffer);

                if (candidates.Count >= 1000)
                    break;
            }

            long nextAddress = (long)mbi.BaseAddress + (long)mbi.RegionSize;
            if (nextAddress <= (long)address)
                break;

            address = (IntPtr)nextAddress;
        }

        Console.WriteLine($"[StellarisMemoryScanner] Scan complete: {totalBytesScanned / (1024 * 1024)}MB scanned, {candidates.Count} candidates found");

        if (candidates.Count == 0)
        {
            Console.WriteLine("[StellarisMemoryScanner] No date candidates found");
            return;
        }

        // Full validation pass on remaining candidates
        Console.WriteLine($"[StellarisMemoryScanner] Validating {candidates.Count} remaining candidates...");
        var validatedAddress = ValidateCandidates(candidates);

        if (validatedAddress != IntPtr.Zero)
        {
            string? date = ReadStringAt(validatedAddress, DateStringLength);

            lock (_lock)
            {
                _dateAddress = validatedAddress;
                _currentDate = date;
            }

            Console.WriteLine($"[StellarisMemoryScanner] Tracking date at 0x{validatedAddress:X}: {date}");
        }
        else
        {
            Console.WriteLine("[StellarisMemoryScanner] No valid date address found");
        }
    }

    /// <summary>
    /// Single-pass check of existing candidates for date progression.
    /// Returns the first candidate whose date has advanced, or IntPtr.Zero if none have.
    /// Unlike ValidateCandidates, this does NOT sleep/wait — it's meant to be called inline during scanning.
    /// </summary>
    private IntPtr TryValidateCandidates(Dictionary<IntPtr, string> candidates)
    {
        foreach (var kvp in candidates.ToList())
        {
            IntPtr addr = kvp.Key;
            string originalValue = kvp.Value;

            string? currentValue = ReadStringAt(addr, DateStringLength);

            if (currentValue == null || !IsValidDate(currentValue))
            {
                candidates.Remove(addr);
                continue;
            }

            if (CompareDates(currentValue, originalValue) > 0)
            {
                Console.WriteLine($"[StellarisMemoryScanner] Candidate 0x{addr:X} progressed: {originalValue} -> {currentValue}");
                return addr;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Scans a memory region for date pattern matches.
    /// </summary>
    private void ScanRegionForDates(IntPtr baseAddress, int regionSize, Dictionary<IntPtr, string> candidates, byte[] buffer)
    {
        int chunkSize = buffer.Length;

        for (int offset = 0; offset < regionSize; offset += chunkSize)
        {
            // Check for cancellation to allow clean shutdown during long scans
            if (_cts.Token.IsCancellationRequested)
                return;

            int bytesToRead = Math.Min(chunkSize, regionSize - offset);
            IntPtr readAddress = IntPtr.Add(baseAddress, offset);

            if (!ReadProcessMemory(_processHandle, readAddress, buffer, bytesToRead, out int bytesRead) || bytesRead == 0)
                continue;


            for (int i = 0; i <= bytesRead - DateStringLength; i++)
            {
                if (buffer[i + 4] == '.' && buffer[i + 7] == '.')
                {
                    string potential = Encoding.ASCII.GetString(buffer, i, DateStringLength);
                    
                    if (IsValidDate(potential))
                    {
                        candidates[IntPtr.Add(readAddress, i)] = potential;
                        
                        if (candidates.Count >= 1000)
                            return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates candidates by checking if the date has increased since it was originally scanned.
    /// Compares current value against the value captured during the initial memory scan.
    /// Returns IntPtr.Zero if no candidate shows a date increase (game paused or not in-game).
    /// </summary>
    private IntPtr ValidateCandidates(Dictionary<IntPtr, string> candidates)
    {
        if (candidates.Count == 0)
            return IntPtr.Zero;

        Console.WriteLine($"[StellarisMemoryScanner] Validating {candidates.Count} candidates...");

        // Check each candidate to see if the date has increased since scan time
        int checksRemaining = ValidationDurationMs / ScanIntervalMs;

        while (checksRemaining > 0 && !_cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(ScanIntervalMs);
            checksRemaining--;

            foreach (var kvp in candidates.ToList())
            {
                IntPtr addr = kvp.Key;
                string originalValue = kvp.Value;
                
                string? currentValue = ReadStringAt(addr, DateStringLength);
                
                if (currentValue == null || !IsValidDate(currentValue))
                {
                    // Address no longer valid, remove from consideration
                    candidates.Remove(addr);
                    continue;
                }

                if (CompareDates(currentValue, originalValue) > 0)
                {
                    Console.WriteLine($"[StellarisMemoryScanner] Candidate 0x{addr:X} progressed: {originalValue} -> {currentValue}");
                    return addr;
                }
            }
        }

        Console.WriteLine("[StellarisMemoryScanner] No candidate showed date progression, game may be paused or not in-game");
        return IntPtr.Zero;
    }

    /// <summary>
    /// Compares two Stellaris dates in YYYY.MM.DD format.
    /// Returns positive if date1 > date2, negative if date1 < date2, zero if equal.
    /// </summary>
    private static int CompareDates(string date1, string date2)
    {
        return string.Compare(date1, date2, StringComparison.Ordinal);
    }

    #endregion

    #region Memory Reading

    /// <summary>
    /// Reads a string from the specified address.
    /// </summary>
    private string? ReadStringAt(IntPtr address, int length)
    {
        byte[] buffer = new byte[length];
        
        if (!ReadProcessMemory(_processHandle, address, buffer, length, out int bytesRead) || bytesRead != length)
            return null;

        return Encoding.ASCII.GetString(buffer);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates that a string matches the expected Stellaris date format YYYY.MM.DD
    /// </summary>
    private static bool IsValidDate(string value)
    {
        if (!DatePattern.IsMatch(value))
            return false;

        // Check that year, month, and day are within expected ranges for Stellaris
        if (int.TryParse(value.Substring(0, 4), out int year) &&
            int.TryParse(value.Substring(5, 2), out int month) &&
            int.TryParse(value.Substring(8, 2), out int day))
        {
            return year >= 2200 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= 30;
        }

        return false;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        Console.WriteLine("[StellarisMemoryScanner] Disposing...");

        _cts.Cancel();
        
        if (_scannerThread.IsAlive)
        {
            _scannerThread.Join(3000);
        }

        ClearTrackedData();
        
        _cts.Dispose();
        GC.SuppressFinalize(this);
        
        Console.WriteLine("[StellarisMemoryScanner] Disposed");
    }

    #endregion
}
