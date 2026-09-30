using System.ComponentModel;
using System.Diagnostics;
using VRChatSDKInspectorJPLocalization.Core;
using VRChatSDKInspectorJPLocalization.Services;

namespace VRChatSDKInspectorJPLocalization.Forms;

public sealed class MainForm : Form
{
    private readonly TextBox pathBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label unityStatus = NewLabel();
    private readonly Label packStatus = NewLabel();
    private readonly Label poStatus = NewLabel();
    private readonly Label adminStatus = NewLabel();
    private readonly Label translationStatus = NewLabel();
    private readonly Button browseButton = NewButton("インストール先を変更…");
    private readonly Button refreshButton = NewButton("状態を再確認");
    private readonly Button elevateButton = NewButton("管理者権限で再起動");
    private readonly Button installButton = NewButton("インストール");
    private readonly Button reinstallButton = NewButton("再インストール");
    private readonly Button uninstallButton = NewButton("アンインストール");
    private AppSettings settings = new();
    private bool busy;
    private bool validTarget;
    private InstallationState state = InstallationState.Abnormal;
    private readonly string? startupPath;

    /// <summary>標準的なWinForms画面の構築</summary>
    public MainForm(string? unityPath)
    {
        startupPath = unityPath;
        Text = AppConstants.AppName;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Yu Gothic UI", 10F);
        MinimumSize = new Size(780, 540);
        ClientSize = new Size(850, 540);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 11, AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var title = NewLabel();
        title.Text = AppConstants.AppName;
        title.Font = new Font(Font, FontStyle.Bold);
        AddWide(layout, title, 0);
        AddRow(layout, "対象Unity", NewLabel("Windows 64bit / " + AppConstants.UnityVersion), 1);
        AddRow(layout, "Unityインストール先", pathBox, 2);
        var pathActions = NewFlow();
        pathActions.Controls.AddRange(new Control[] { browseButton, refreshButton });
        AddWide(layout, pathActions, 3);
        AddRow(layout, "Unityの確認", unityStatus, 4);
        AddRow(layout, "日本語Language Pack", packStatus, 5);
        AddRow(layout, "ja.po", poStatus, 6);
        AddRow(layout, "管理者権限", adminStatus, 7);
        AddRow(layout, "翻訳の状態", translationStatus, 8);
        var actions = NewFlow();
        actions.Controls.AddRange(new Control[] { elevateButton, installButton, reinstallButton, uninstallButton });
        AddWide(layout, actions, 9);
        AddWide(layout, NewLabel("翻訳データ: " + AppConstants.TranslationVersion + "（動作確認用3件）\n"
            + "操作前にUnity Editorを終了してください。変更前のバックアップはja.poと同じ場所に保存します。\n"
            + "このツールはUnityの共有ファイルを変更するため、同じEditorを使う全プロジェクトに影響します。"), 10);
        Controls.Add(layout);
        browseButton.Click += async (_, _) => await BrowseAsync();
        refreshButton.Click += async (_, _) => await RefreshStatusAsync(true);
        elevateButton.Click += (_, _) => Elevate();
        installButton.Click += async (_, _) => await ExecuteAsync(TranslationOperation.Install);
        reinstallButton.Click += async (_, _) => await ExecuteAsync(TranslationOperation.Reinstall);
        uninstallButton.Click += async (_, _) => await ExecuteAsync(TranslationOperation.Uninstall);
        Shown += async (_, _) => await InitializeAsync();
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        UpdateButtons();
    }

    /// <summary>保存済み設定と標準パスによる起動時確認</summary>
    private async Task InitializeAsync()
    {
        try { settings = AppSettings.Load(); }
        catch (Exception ex) { ShowError("設定を読み込めませんでした。標準パスで確認します。", ex); }
        pathBox.Text = startupPath ?? settings.UnityPath ?? UnityDetector.DefaultPath;
        await RefreshStatusAsync(true);
        if (unityStatus.Tag is UnityInstallation { IsValid: false })
        {
            MessageBox.Show(this, $"Unity {AppConstants.UnityVersion}が見つかりませんでした。\nUnityのインストールフォルダーを選択してください。",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            await BrowseAsync();
        }
    }

    /// <summary>Unityインストールフォルダーの選択と保存</summary>
    private async Task BrowseAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = $"Unity {AppConstants.UnityVersion}のインストールフォルダーを選択", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        pathBox.Text = dialog.SelectedPath;
        await RefreshStatusAsync(true);
        if (unityStatus.Tag is not UnityInstallation { IsValid: true }) return;
        settings.UnityPath = pathBox.Text;
        try { settings.Save(); }
        catch (Exception ex) { ShowError("Unityのパスを保存できませんでした。今回の起動中は使用できます。", ex); }
    }

    /// <summary>画面表示と操作可否の再検証</summary>
    private async Task RefreshStatusAsync(bool notify)
    {
        busy = true;
        validTarget = false;
        state = InstallationState.Abnormal;
        unityStatus.Tag = null;
        unityStatus.Text = "確認中…";
        packStatus.Text = "未確認";
        poStatus.Text = "未確認";
        translationStatus.Text = "未確認";
        UpdateButtons();
        try
        {
            var path = pathBox.Text;
            var unity = await Task.Run(() => UnityDetector.Inspect(path));
            unityStatus.Tag = unity;
            unityStatus.Text = unity.Message;
            packStatus.Text = unity.HasLanguagePack ? "利用可能（ja.poあり）" : "利用不可";
            poStatus.Text = unity.HasLanguagePack ? "存在を確認済み" : "見つかりません";
            validTarget = unity.IsValid && unity.HasLanguagePack;
            if (!validTarget)
            {
                if (notify && unity.IsValid && !unity.HasLanguagePack) ShowLanguagePackHelp();
                return;
            }
            var info = await Task.Run(() => TranslationInstaller.Inspect(unity.PoPath));
            state = info.State;
            translationStatus.Text = info.Message + (info.Version is null ? "" : $"（データ: {info.Version}）");
            translationStatus.ForeColor = state == InstallationState.Abnormal ? Color.Firebrick : SystemColors.ControlText;
            if (notify && state == InstallationState.Abnormal)
                MessageBox.Show(this, info.Message + "\nファイルは変更しません。バックアップと内容を確認してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            translationStatus.Text = "インストール状態異常（読み込み・検証失敗）";
            if (notify) ShowError("状態を確認できないため、変更操作を無効にしました。", ex);
        }
        finally { busy = false; UpdateButtons(); }
    }

    /// <summary>状態と権限に応じたボタンの切り替え</summary>
    private void UpdateButtons()
    {
        var admin = AdministratorService.IsAdministrator();
        adminStatus.Text = admin ? "管理者として実行中" : "通常権限（変更には管理者権限が必要）";
        var writable = !busy && validTarget && admin;
        browseButton.Enabled = refreshButton.Enabled = !busy;
        elevateButton.Enabled = !busy && !admin;
        installButton.Enabled = writable && state == InstallationState.NotInstalled;
        reinstallButton.Enabled = uninstallButton.Enabled = writable && state == InstallationState.Installed;
        UseWaitCursor = busy;
    }

    /// <summary>翻訳操作の確認と非同期実行</summary>
    private async Task ExecuteAsync(TranslationOperation operation)
    {
        var action = operation switch { TranslationOperation.Install => "インストール", TranslationOperation.Reinstall => "再インストール", _ => "アンインストール" };
        if (MessageBox.Show(this, $"翻訳を{action}します。Unity Editorを終了してください。\n\n対象: {pathBox.Text}\n\n続行しますか？", Text,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        busy = true;
        UpdateButtons();
        try
        {
            var path = pathBox.Text;
            var backup = await Task.Run(() => TranslationInstaller.Execute(path, operation));
            MessageBox.Show(this, $"{action}が完了しました。\nUnityを起動し直して確認してください。\n\nバックアップ: {backup}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError($"{action}できませんでした。", ex); }
        finally { busy = false; await RefreshStatusAsync(true); }
    }

    /// <summary>管理者権限での再起動とUACキャンセルの通知</summary>
    private void Elevate()
    {
        try { AdministratorService.RestartElevated(pathBox.Text); Close(); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { MessageBox.Show(this, "管理者権限での再起動がキャンセルされました。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { ShowError("管理者権限で再起動できませんでした。", ex); }
    }

    /// <summary>日本語Language Packがない場合の案内</summary>
    private void ShowLanguagePackHelp()
    {
        var help = new TaskDialogButton("日本語化の方法を開く");
        var page = new TaskDialogPage
        {
            Caption = Text, Heading = "Unityの日本語Language Packが見つかりません",
            Text = "Unity Hubで対象Editorの日本語Language Packを追加してください。\n追加後は「状態を再確認」を押してください。",
            Icon = TaskDialogIcon.Warning, Buttons = { help, TaskDialogButton.Close }
        };
        if (TaskDialog.ShowDialog(this, page) != help) return;
        try { using var process = Process.Start(new ProcessStartInfo(AppConstants.LanguagePackHelpUrl) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("ブラウザーを開けませんでした。", ex); }
    }

    /// <summary>利用者向けのエラー通知と開発者向けの詳細ログ保存</summary>
    private void ShowError(string message, Exception exception)
    {
        var logPath = Path.Combine(AppSettings.DirectoryPath, "errors.log");
        string logMessage;
        try
        {
            Directory.CreateDirectory(AppSettings.DirectoryPath);
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O}\n{exception}\n\n");
            logMessage = "詳細ログ: " + logPath;
        }
        catch (Exception logException)
        {
            logMessage = "ログの保存にも失敗しました: " + logException.Message + "\n\n" + exception;
        }
        MessageBox.Show(this, message + "\n\n" + exception.Message + "\n\n" + logMessage, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>自動サイズ調整ラベルの生成</summary>
    private static Label NewLabel(string text = "") => new() { Text = text, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };

    /// <summary>標準ボタンの生成</summary>
    private static Button NewButton(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 5, 8, 5), Margin = new Padding(0, 4, 10, 4) };

    /// <summary>折り返し可能なボタン領域の生成</summary>
    private static FlowLayoutPanel NewFlow() => new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };

    /// <summary>ラベル付き行の追加</summary>
    private static void AddRow(TableLayoutPanel panel, string label, Control control, int row)
    {
        panel.Controls.Add(NewLabel(label), 0, row);
        panel.Controls.Add(control, 1, row);
    }

    /// <summary>全幅の行の追加</summary>
    private static void AddWide(TableLayoutPanel panel, Control control, int row)
    {
        panel.Controls.Add(control, 0, row);
        panel.SetColumnSpan(control, 2);
    }
}
