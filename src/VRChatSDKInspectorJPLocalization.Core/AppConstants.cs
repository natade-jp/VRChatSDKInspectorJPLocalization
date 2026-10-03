namespace VRChatSDKInspectorJPLocalization.Core;

public static class AppConstants
{
    public const string AppName = "VRChatSDK-Inspector翻訳機";
    public const string UnityVersion = "2022.3.22f1";
    public const string BeginMarker = "# ===== VRChatSDK-Inspector-JP BEGIN =====";
    public const string EndMarker = "# ===== VRChatSDK-Inspector-JP END =====";
    public const string LocalizationRelativePath = "Editor/Data/Localization/ja.po";
    public const string LanguagePackHelpUrl = "https://blog.natade.net/2026/10/03/unity-editor-japanese/";
}

public enum InstallationState { NotInstalled, Installed, Abnormal }
public enum TranslationOperation { Install, Reinstall, Uninstall }
public sealed record Translation(string English, string Japanese);
public sealed record TranslationData(IReadOnlyList<Translation> Entries, string Version);
public sealed record InstallationInfo(InstallationState State, string Message,
    int Start = 0, int Length = 0, string? Version = null);
