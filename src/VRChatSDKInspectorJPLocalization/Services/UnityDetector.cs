using System.Text.RegularExpressions;
using VRChatSDKInspectorJPLocalization.Core;

namespace VRChatSDKInspectorJPLocalization.Services;

public sealed record UnityInstallation(string Path, bool IsValid, bool HasLanguagePack, string Message, string? Version = null)
{
    public string PoPath => System.IO.Path.Combine(Path, AppConstants.LocalizationRelativePath);
}

public static class UnityDetector
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Unity", "Hub", "Editor", AppConstants.UnityVersion);

    /// <summary>フォルダー名によるUnityバージョンと必要な構造の確認</summary>
    public static UnityInstallation Inspect(string path)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var version = Path.GetFileName(path);
        if (!Regex.IsMatch(version, @"\A[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+\z", RegexOptions.CultureInvariant))
            return new(path, false, false, "フォルダー名からUnityバージョンを取得できません。2022.3.22f1などのバージョン名のフォルダーを選択してください。");
        var executable = Path.Combine(path, "Editor", "Unity.exe");
        if (!File.Exists(executable) || !Directory.Exists(Path.Combine(path, "Editor", "Data")))
            return new(path, false, false, "Unityの必要な構造（Editor\\Unity.exe、Editor\\Data）が見つかりません。", version);
        var hasPack = File.Exists(Path.Combine(path, AppConstants.LocalizationRelativePath));
        var description = version == AppConstants.UnityVersion ? "検証済みバージョン" : "動作未検証";
        return new(path, true, hasPack, hasPack ? $"Unity {version}（{description}）" : "Unityの日本語Language Packが見つかりません。", version);
    }
}
