using System.Reflection;
using Microsoft.Win32;

namespace SchoolMessenger.Desktop;

public sealed class WindowsStartup(string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    const string EntryName = "YeoyangSchoolMessenger";
    public bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(registryPath); return key?.GetValue(EntryName) is string; }
    }
    public static string Command
    {
        get
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("자동 실행 경로를 확인할 수 없습니다.");
            var assembly = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? " \"" + Path.Combine(AppContext.BaseDirectory, Assembly.GetEntryAssembly()!.GetName().Name + ".dll") + "\"" : "";
            return "\"" + executable + "\"" + assembly + " --startup";
        }
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        if (enabled) key.SetValue(EntryName, Command, RegistryValueKind.String);
        else key.DeleteValue(EntryName, throwOnMissingValue: false);
    }
}
