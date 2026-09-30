namespace VRChatSDKInspectorJPLocalization.Core;

public static class AppConstants
{
    public const string AppName = "VRChatSDK-Inspector翻訳機";
    public const string UnityVersion = "2022.3.22f1";
    public const string TranslationVersion = "sample-1";
    public const string BeginMarker = "# ===== VRChatSDK-Inspector-JP BEGIN =====";
    public const string EndMarker = "# ===== VRChatSDK-Inspector-JP END =====";
    public const string LocalizationRelativePath = "Editor/Data/Localization/ja.po";
    // TODO: 日本語化の説明ページが決まり次第差し替え
    public const string LanguagePackHelpUrl = "https://docs.unity3d.com/2022.3/Documentation/Manual/Preferences.html";
}

public enum InstallationState { NotInstalled, Installed, Abnormal }
public enum TranslationOperation { Install, Reinstall, Uninstall }
public sealed record Translation(string English, string Japanese);
public sealed record InstallationInfo(InstallationState State, string Message,
    int Start = 0, int Length = 0, string? Version = null);
