/*
    AppConstants.cs - Defined constants for the RPCParadox application.
    Copyright (C) 2026 Derek Li (DairyProducts)

    This program is licensed under the Microsoft Public License (MS-PL).
    See the LICENSE file in the project root for license information.
*/

using System.Reflection;

namespace RPCParadox;

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