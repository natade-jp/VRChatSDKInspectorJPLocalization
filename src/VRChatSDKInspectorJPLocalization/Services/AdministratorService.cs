using System.Diagnostics;
using System.Security.Principal;

namespace VRChatSDKInspectorJPLocalization.Services;

public static class AdministratorService
{
    /// <summary>現在のプロセスの管理者権限の確認</summary>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>選択したUnityパスを引き継ぐUAC付きの再起動</summary>
    public static void RestartElevated(string unityPath)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルのパスを取得できません。");
        var start = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        // dotnet app.dllによる開発実行にも対応（通常の配布はapphost exe）
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "VRChatSDKInspectorJPLocalization.dll"));
        start.ArgumentList.Add("--unity-path");
        start.ArgumentList.Add(unityPath);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("管理者プロセスを起動できませんでした。");
    }
}
