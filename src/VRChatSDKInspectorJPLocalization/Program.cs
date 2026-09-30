using VRChatSDKInspectorJPLocalization.Forms;

namespace VRChatSDKInspectorJPLocalization;

internal static class Program
{
    /// <summary>アプリケーションの起動</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var unityPath = args.Length == 2 && args[0] == "--unity-path" ? args[1] : null;
        Application.Run(new MainForm(unityPath));
    }
}
