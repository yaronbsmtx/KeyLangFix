using System;
using Microsoft.Win32;

namespace KeyLangFix.Services;

/// <summary>"Start with Windows" via the per-user Run key (no admin needed).</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyLangFix";
    private const string OldValueName = "AutoLang";   // earlier working name

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k == null) return;
            if (enabled && Environment.ProcessPath is { } path) k.SetValue(ValueName, "\"" + path + "\"");
            else k.DeleteValue(ValueName, throwOnMissingValue: false);
            k.DeleteValue(OldValueName, throwOnMissingValue: false);
        }
        catch { /* policy-restricted: ignore */ }
    }
}
