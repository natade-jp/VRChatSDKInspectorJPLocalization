using System.Text.Json;

namespace VRChatSDKInspectorJPLocalization.Services;

public sealed class AppSettings
{
    public string? UnityPath { get; set; }
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRChatSDKInspectorJPLocalization");
    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    /// <summary>ユーザー単位の設定の読み込み</summary>
    public static AppSettings Load() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException("設定ファイルが空です。")
        : new AppSettings();

    /// <summary>ユーザー単位の設定の保存</summary>
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
