/*
    AppConstants.cs - Defined constants for the RPCParadox application.
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

using System.Reflection;

namespace RPCParadox2;

internal static class AppConstants
{
    private static readonly Assembly _asm = Assembly.GetEntryAssembly()!;
    private static readonly AssemblyInformationalVersionAttribute? _info = _asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    private static readonly string _ver = _info?.InformationalVersion ?? "unknown";

    internal static readonly string Version = GetVersion();
    internal static readonly string ?CommitHash = GetCommitHash();
    internal static readonly bool DebugBuild = IsDebugBuild();

    private static string GetVersion()
    {
        if (_ver.Contains('+'))
        {
            return _ver.Substring(0, _ver.IndexOf('+'));
        }
        return _ver;
    }

    private static string? GetCommitHash()
    {
        if (_ver.Contains('+'))
        {
            return _ver.Substring(_ver.IndexOf('+') + 1);
        }
        else
        {
            return null;
        }
    }

    private static bool IsDebugBuild()
    {
        bool isDebug = false;
        #if DEBUG
        isDebug = true;
        #endif
        return isDebug;
    }
}