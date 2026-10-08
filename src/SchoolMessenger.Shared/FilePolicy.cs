using System.Diagnostics;

namespace SchoolMessenger.Shared;

public sealed class FilePolicyException(string message, int status = 400) : Exception(message)
{ public int Status { get; } = status; }

public static class FilePolicy
{
    public const long MaxFile = 104_857_600, MaxTotal = 209_715_200, MaxStaged = 524_288_000;
    public static string Name(string raw)
    {
        var name = Path.GetFileName(raw.Replace('\\', '/'));
        if (name.Length is < 1 or > 200 || name.Any(char.IsControl) ||
            !new[] { ".hwp", ".hwpx", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg", ".zip" }.Contains(Path.GetExtension(name).ToLowerInvariant()))
            throw new FilePolicyException("지원하지 않는 파일 형식입니다.");
        return name;
    }
    public static bool Signature(string path, string extension)
    {
        Span<byte> bytes = stackalloc byte[8]; using var input = File.OpenRead(path);
        var length = input.Read(bytes); bytes = bytes[..length];
        if (extension is ".docx" or ".xlsx" or ".pptx" or ".hwpx" or ".zip") return length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 });
        if (extension is ".doc" or ".xls" or ".ppt" or ".hwp") return length == 8 && bytes.SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 });
        if (extension == ".pdf") return length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8);
        if (extension == ".png") return length == 8 && bytes.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (extension is ".jpg" or ".jpeg") return length >= 3 && bytes[..3].SequenceEqual(new byte[] { 255, 216, 255 });
        return extension is ".txt" or ".csv";
    }
    public static async Task Scan(string path, string name, bool development, string? scannerPath, CancellationToken cancellation)
    {
        if (!Signature(path, Path.GetExtension(name).ToLowerInvariant())) throw new FilePolicyException("파일 내용과 확장자가 일치하지 않습니다.");
        if (development) return;
        var scanner = scannerPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
        if (!File.Exists(scanner)) throw new FilePolicyException("첨부파일 검사기를 사용할 수 없습니다. 관리자에게 문의하세요.", 503);
        var info = new ProcessStartInfo(scanner) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-Scan", "-ScanType", "3", "-File", path, "-DisableRemediation" }) info.ArgumentList.Add(argument);
        using var scan = Process.Start(info) ?? throw new FilePolicyException("파일 검사기를 실행하지 못했습니다.", 503);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await scan.WaitForExitAsync(timeout.Token); }
        catch { if (!scan.HasExited) scan.Kill(true); throw; }
        if (scan.ExitCode != 0 || !File.Exists(path)) throw new FilePolicyException("파일 검사 실패 또는 위험 파일입니다.", 422);
    }
}
