using VRChatSDKInspectorJPLocalization.Core;

namespace VRChatSDKInspectorJPLocalization.Services;

public static class TranslationInstaller
{
    /// <summary>現在のインストール状態と既存POの整合性の確認</summary>
    public static InstallationInfo Inspect(string poPath)
    {
        var document = Utf8Document.Read(File.ReadAllBytes(poPath));
        var info = PoDocument.Inspect(document.Text);
        if (info.State == InstallationState.Abnormal) return info;
        if (PoSyntax.ReadMessageIds(document.Text).Count == 0)
            throw new InvalidDataException("ja.poに有効な翻訳エントリがありません。");
        return info;
    }

    /// <summary>操作直前の安全確認と翻訳の更新</summary>
    public static string Execute(string unityPath, TranslationOperation operation)
    {
        var unity = UnityDetector.Inspect(unityPath);
        if (!unity.IsValid || !unity.HasLanguagePack) throw new InvalidOperationException(unity.Message);
        if (!AdministratorService.IsAdministrator()) throw new UnauthorizedAccessException("管理者権限で再起動してください。");
        var editors = System.Diagnostics.Process.GetProcessesByName("Unity");
        try
        {
            if (editors.Length != 0)
                throw new InvalidOperationException("Unity Editorをすべて終了してから操作してください。");
        }
        finally { foreach (var editor in editors) editor.Dispose(); }
        // 親ディレクトリのジャンクションも拒否し、意図しない場所への書き込みを防止
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(unity.PoPath)!); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("リンクまたはジャンクションを含むUnityパスは安全のため変更できません。");
        return AtomicPoFile.Update(unity.PoPath, bytes =>
        {
            var document = Utf8Document.Read(bytes);
            var data = operation == TranslationOperation.Uninstall ? null : TranslationCsvReader.ReadEmbeddedData();
            var result = PoDocument.Transform(document.Text, operation, data?.Entries ?? Array.Empty<Translation>(), data?.Version ?? "");
            return document.GetBytes(result);
        });
    }
}
