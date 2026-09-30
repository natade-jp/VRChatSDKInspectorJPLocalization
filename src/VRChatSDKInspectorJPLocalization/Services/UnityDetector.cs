using System.Diagnostics;
using VRChatSDKInspectorJPLocalization.Core;

namespace VRChatSDKInspectorJPLocalization.Services;

public sealed record UnityInstallation(string Path, bool IsValid, bool HasLanguagePack, string Message)
{
    public string PoPath => System.IO.Path.Combine(Path, AppConstants.LocalizationRelativePath);
}

public static class UnityDetector
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Unity", "Hub", "Editor", AppConstants.UnityVersion);

    /// <summary>Unity実行ファイルの製品バージョンと日本語Language Packの検証</summary>
    public static UnityInstallation Inspect(string path)
    {
        path = Path.GetFullPath(path);
        var executable = Path.Combine(path, "Editor", "Unity.exe");
        if (!File.Exists(executable) || !Directory.Exists(Path.Combine(path, "Editor", "Data")))
            return new(path, false, false, $"Unity {AppConstants.UnityVersion}が見つかりませんでした。");
        var productVersion = FileVersionInfo.GetVersionInfo(executable).ProductVersion;
        if (productVersion != AppConstants.UnityVersion &&
            productVersion?.StartsWith(AppConstants.UnityVersion + "_", StringComparison.Ordinal) != true)
            return new(path, false, false, $"Unity {AppConstants.UnityVersion}を確認できません。製品バージョン: {productVersion ?? "不明"}");
        var hasPack = File.Exists(Path.Combine(path, AppConstants.LocalizationRelativePath));
        return new(path, true, hasPack, hasPack ? "対応Unityを確認済み" : "Unityの日本語Language Packが見つかりません。");
    }
}
