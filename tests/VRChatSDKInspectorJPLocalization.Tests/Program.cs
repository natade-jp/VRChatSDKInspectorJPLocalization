using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using VRChatSDKInspectorJPLocalization.Core;
using VRChatSDKInspectorJPLocalization.Services;

namespace VRChatSDKInspectorJPLocalization.Tests;

internal static class Program
{
    private const string Original = "# 元の日本語コメント\nmsgid \"\"\nmsgstr \"\"\n\"Content-Type: text/plain; charset=UTF-8\\n\"\n\nmsgid \"Existing\"\nmsgstr \"既存\"\n";
    private static readonly IReadOnlyList<Translation> Sample = new[] { new Translation("Pull", "引っ張り"), new Translation("Spring", "ばね"), new Translation("Gravity Falloff", "重力減衰") };
    private static int failures;
    private static int count;

    /// <summary>外部パッケージを使わない回帰テストの実行</summary>
    private static int Main(string[] args)
    {
        Test("Embedded CSV", () => Equal(3, TranslationCsvReader.ReadEmbedded().Count));
        Test("CSV quotes, comma and multiline", () =>
        {
            using var reader = new StringReader("English,Japanese\n\"a,\"\"b\"\"\",\"日本語\n二行目\"\n");
            var data = TranslationCsvReader.Read(reader);
            Equal("a,\"b\"", data[0].English);
            Equal("日本語\n二行目", data[0].Japanese);
        });
        Test("CSV preserves whitespace", () => Equal(" x ", ReadCsv("English,Japanese\n x ,翻訳\n")[0].English));
        Test("CSV CRLF fields normalized", () => Equal("a\nb", ReadCsv("English,Japanese\r\n\"a\r\nb\",日本語\r\n")[0].English));
        Test("CSV bad header", () => Throws<InvalidDataException>(() => ReadCsv("Japanese,English\nあ,a")));
        Test("CSV identical duplicates merged", () =>
        {
            var data = ReadCsv("English,Japanese\n# コンポーネントA\nx,一\n# コンポーネントB\nx,一\n");
            Equal(1, data.Count);
            Equal("一", data[0].Japanese);
        });
        Test("CSV different translations keep first", () =>
        {
            var data = ReadCsv("English,Japanese\nx,一\ny,三\nx,二");
            Equal(2, data.Count);
            Equal("一", data[0].Japanese);
            Equal("y", data[1].English);
        });
        Test("CSV duplicate normalized multiline IDs", () => Equal(1, ReadCsv("English,Japanese\n\"a\r\nb\",一\n\"a\nb\",二").Count));
        Test("CSV duplicates produce a single PO entry", () =>
        {
            var data = ReadCsv("English,Japanese\nPull,引っ張り\nPull,引く\nPull,引っ張り\n");
            var installed = PoDocument.Install(Original, data);
            Equal(3, PoSyntax.ReadMessageIds(installed).Count);
            True(installed.Contains("msgstr \"引っ張り\"", StringComparison.Ordinal));
            True(!installed.Contains("msgstr \"引く\"", StringComparison.Ordinal));
            Equal(Original, PoDocument.Uninstall(installed));
        });
        Test("CSV empty field", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\nx,")));
        Test("CSV malformed duplicate still rejected", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\nx,一\nx,")));
        Test("CSV extra field", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\nx,y,z")));
        Test("CSV broken quotes", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\n\"x,y")));
        Test("CSV no entries", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\n")));
        Test("CSV component comments and blank lines", () =>
        {
            var data = ReadCsv("# 辞書の説明\n\nEnglish,Japanese\n# コンポーネントA\nPull,引っ張り\n  \t# コンポーネントB,\"未閉鎖の引用符もコメント\nSpring,ばね\n# 最終行");
            Equal(2, data.Count);
            Equal("Spring", data[1].English);
        });
        Test("CSV hashes inside fields preserved", () =>
        {
            var data = ReadCsv("English,Japanese\n\"#Label\",\"#ラベル\"\nA#B,訳 # 注釈ではない\n");
            Equal("#Label", data[0].English);
            Equal("#ラベル", data[0].Japanese);
            Equal("訳 # 注釈ではない", data[1].Japanese);
        });
        Test("CSV quoted comment-like and blank lines preserved", () =>
        {
            var data = ReadCsv("English,Japanese\n\"first\n# not a comment\n\nlast\",\"一行目\n  # 本文\n\n最終行\"\n");
            Equal("first\n# not a comment\n\nlast", data[0].English);
            Equal("一行目\n  # 本文\n\n最終行", data[0].Japanese);
        });
        Test("CSV trailing text after quote rejected", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\n\"x\"bad,訳")));
        Test("CSV quote in unquoted field rejected", () => Throws<InvalidDataException>(() => ReadCsv("English,Japanese\nx\"bad,訳")));
        Test("CSV comments only rejected", () => Throws<InvalidDataException>(() => ReadCsv("# 説明のみ\n")));
        Test("CSV CRLF comments and whitespace", () => Equal("訳", ReadCsv("  # 説明\r\nEnglish,Japanese\r\n \t\r\nx,訳\r\n \t")[0].Japanese));
        Test("PO escaping round trip", () =>
        {
            const string value = "日本語\"\\\n\t\r\a\b\f\v";
            Equal(value, PoSyntax.Unquote(PoSyntax.Quote(value)));
            Equal("\"a\\\"b\\\\c\\n\"", PoSyntax.Quote("a\"b\\c\n"));
        });
        Test("PO octal escape", () => Equal("A", PoSyntax.Unquote("\"\\101\"")));
        Test("Unity existing C1 characters preserved", () => Equal("a\u0080\u0099b", PoSyntax.Unquote("\"a\u0080\u0099b\"")));
        Test("Unity nonstandard msgstr preserved", () =>
        {
            const string original = "msgid \"Existing\"\nmsgstr \"既存\\ n文字列\"\n";
            Equal(original, PoDocument.Uninstall(PoDocument.Install(original, Sample)));
        });
        Test("Unknown msgid escape still rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("msgid \"Unknown\\ q\"\nmsgstr \"翻訳\"\n")));
        Test("PO malformed escape rejected", () => Throws<InvalidDataException>(() => PoSyntax.Unquote("\"\\q\"")));
        Test("PO raw quote rejected", () => Throws<InvalidDataException>(() => PoSyntax.Unquote("\"a\"b\"")));
        Test("PO unsupported control rejected", () => Throws<InvalidDataException>(() => PoSyntax.Quote("\0")));
        Test("PO multiline msgid", () => True(PoSyntax.ReadMessageIds("msgid \"Gravity \"\n\"Falloff\"\nmsgstr \"重力\"\n").Contains("Gravity Falloff")));
        Test("PO context and plural", () =>
        {
            var ids = PoSyntax.ReadMessageIds("msgctxt \"menu\"\nmsgid \"Pull\"\nmsgstr \"引く\"\n\nmsgid \"item\"\nmsgid_plural \"items\"\nmsgstr[0] \"物\"\nmsgstr[1] \"物\"\n");
            True(ids.Contains("Pull") && ids.Contains("item"));
        });
        Test("PO obsolete entries ignored", () => True(!PoSyntax.ReadMessageIds("#~ msgid \"Pull\"\n#~ msgstr \"引く\"\n").Contains("Pull")));
        Test("PO no blank separator", () => Equal(2, PoSyntax.ReadMessageIds("msgid \"a\"\nmsgstr \"あ\"\nmsgid \"b\"\nmsgstr \"び\"\n").Count));
        Test("PO broken input rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("msgid \"a\"\nmsgstr \"broken\n")));
        Test("PO missing msgstr rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("msgid \"a\"\n")));
        Test("PO orphan continuation rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("\"a\"\n")));
        Test("PO duplicate key rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("msgid \"a\"\nmsgstr \"あ\"\n\nmsgid \"a\"\nmsgstr \"い\"\n")));
        Test("PO same ID in distinct contexts", () => Equal(1, PoSyntax.ReadMessageIds("msgctxt \"a\"\nmsgid \"x\"\nmsgstr \"一\"\n\nmsgctxt \"b\"\nmsgid \"x\"\nmsgstr \"二\"\n").Count));
        Test("PO noncontiguous plural rejected", () => Throws<InvalidDataException>(() => PoSyntax.ReadMessageIds("msgid \"a\"\nmsgid_plural \"as\"\nmsgstr[0] \"一\"\nmsgstr[2] \"二\"\n")));
        Test("Not installed", () => Equal(InstallationState.NotInstalled, PoDocument.Inspect(Original).State));
        Test("Install and version", () =>
        {
            var result = PoDocument.Install(Original, Sample);
            var info = PoDocument.Inspect(result);
            Equal(InstallationState.Installed, info.State);
            Equal(AppConstants.TranslationVersion, info.Version);
            True(result.StartsWith(Original, StringComparison.Ordinal));
        });
        Test("Double install rejected", () => Throws<InvalidDataException>(() => PoDocument.Install(PoDocument.Install(Original, Sample), Sample)));
        Test("BEGIN only", () => Abnormal(Original + AppConstants.BeginMarker + "\n"));
        Test("END only", () => Abnormal(Original + AppConstants.EndMarker + "\n"));
        Test("Reversed markers", () => Abnormal(Original + AppConstants.EndMarker + "\n" + AppConstants.BeginMarker + "\n"));
        Test("Multiple blocks", () => Abnormal(PoDocument.Install(Original, Sample) + PoDocument.Install(Original, Sample)));
        Test("Altered block rejected", () => Abnormal(PoDocument.Install(Original, Sample).Replace("引っ張り", "変更")));
        Test("Truncated block rejected", () => Abnormal(PoDocument.Install(Original, Sample).TrimEnd('\n')));
        Test("Unrecognized block format rejected", () => Abnormal(Original + AppConstants.BeginMarker + "\nmsgid \"x\"\nmsgstr \"y\"\n" + AppConstants.EndMarker + "\n"));
        Test("Changed padding rejected", () => Abnormal(PoDocument.Install(Original, Sample).Replace("# Added-LF: 1", "# Added-LF: 2")));
        Test("Existing ID takes precedence", () =>
        {
            var original = Original + "\nmsgid \"Pull\"\nmsgstr \"標準優先\"\n";
            var result = PoDocument.Install(original, Sample);
            True(result.Contains("標準優先") && !result.Contains("引っ張り"));
            Equal(5, PoSyntax.ReadMessageIds(result).Count);
        });
        Test("Existing multiline ID excluded", () =>
        {
            var result = PoDocument.Install(Original + "\nmsgid \"Gravity \"\n\"Falloff\"\nmsgstr \"標準\"\n", Sample);
            True(!result.Contains("重力減衰"));
        });
        Test("Context ID conservatively excluded", () => True(!PoDocument.Install(Original + "\nmsgctxt \"menu\"\nmsgid \"Pull\"\nmsgstr \"標準\"\n", Sample).Contains("引っ張り")));
        Test("All translations already present", () =>
        {
            var result = PoDocument.Install("msgid \"Pull\"\nmsgstr \"既存\"\n", new[] { Sample[0] });
            Equal(InstallationState.Installed, PoDocument.Inspect(result).State);
        });
        foreach (var suffix in new[] { "", "\n", "\n\n", "\n\n\n" })
        {
            var original = Original.TrimEnd('\n') + suffix;
            Test($"Exact round trip trailing LF={suffix.Length}", () => Equal(original, PoDocument.Uninstall(PoDocument.Install(original, Sample))));
        }
        Test("Content after block retained", () =>
        {
            const string extra = "\n# 他ツールの追加\nmsgid \"Other\"\nmsgstr \"別\"\n";
            Equal(Original + extra, PoDocument.Uninstall(PoDocument.Install(Original, Sample) + extra));
        });
        Test("Reinstall replaces content", () =>
        {
            var result = PoDocument.Transform(PoDocument.Install(Original, Sample), TranslationOperation.Reinstall, new[] { new Translation("Pull", "最新版") });
            True(result.Contains("最新版") && !result.Contains("引っ張り") && !result.Contains("msgid \"Spring\""));
            Equal(Original, PoDocument.Uninstall(result));
        });
        Test("Reinstall when absent rejected", () => Throws<InvalidDataException>(() => PoDocument.Transform(Original, TranslationOperation.Reinstall, Sample)));
        Test("Uninstall when absent rejected", () => Throws<InvalidDataException>(() => PoDocument.Uninstall(Original)));
        Test("LF only", () => True(!PoDocument.Install(Original, Sample).Contains('\r')));
        Test("Invalid UTF8 rejected", () => Throws<DecoderFallbackException>(() => Utf8Document.Read(new byte[] { 0xFF, 0xFF })));
        Test("CRLF rejected without conversion", () => Throws<InvalidDataException>(() => Utf8Document.Read(Encoding.UTF8.GetBytes(Original.Replace("\n", "\r\n")))));
        Test("Empty document rejected", () => Throws<InvalidDataException>(() => Utf8Document.Read(Array.Empty<byte>())));
        Test("BOM and Japanese preserved", () =>
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Original)).ToArray();
            var document = Utf8Document.Read(bytes);
            True(document.HasBom);
            BytesEqual(bytes, document.GetBytes(PoDocument.Uninstall(PoDocument.Install(document.Text, Sample))));
        });
        Test("UTF8 no BOM preserved", () =>
        {
            var bytes = Encoding.UTF8.GetBytes(Original);
            var document = Utf8Document.Read(bytes);
            True(!document.HasBom);
            BytesEqual(bytes, document.GetBytes(document.Text));
        });
        Test("Atomic install, reinstall, uninstall and backups", FileRoundTrip);
        Test("Invalid transform leaves original untouched", () => InTemporaryDirectory(directory =>
        {
            var path = CreatePo(directory);
            Throws<InvalidDataException>(() => AtomicPoFile.Update(path, _ => throw new InvalidDataException("test")));
            Equal(Original, File.ReadAllText(path));
            Equal(1, Directory.GetFiles(directory).Length);
        }));
        Test("Read only file rejected", () => InTemporaryDirectory(directory =>
        {
            var path = CreatePo(directory);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try { Throws<IOException>(() => AtomicPoFile.Update(path, data => data)); }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
            Equal(Original, File.ReadAllText(path));
        }));
        Test("Writer lock prevents update", () => InTemporaryDirectory(directory =>
        {
            var path = CreatePo(directory);
            using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            Throws<IOException>(() => AtomicPoFile.Update(path, data => data));
        }));
        Test("Backup failure leaves original untouched", () => InTemporaryDirectory(directory =>
        {
            var path = CreatePo(directory);
            var folder = new DirectoryInfo(directory);
            var originalAcl = folder.GetAccessControl();
            var restrictedAcl = folder.GetAccessControl();
            using var identity = WindowsIdentity.GetCurrent();
            restrictedAcl.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateFiles,
                InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
            folder.SetAccessControl(restrictedAcl);
            try { Throws<UnauthorizedAccessException>(() => AtomicPoFile.Update(path, bytes => Transform(bytes, TranslationOperation.Install))); }
            finally { folder.SetAccessControl(originalAcl); }
            Equal(Original, File.ReadAllText(path));
            Equal(1, Directory.GetFiles(directory).Length);
        }));
        Test("Replacement failure preserves original and backup", () => InTemporaryDirectory(directory =>
        {
            var path = CreatePo(directory);
            FileStream? replacementLock = null;
            try
            {
                Throws<IOException>(() => AtomicPoFile.Update(path, bytes =>
                {
                    replacementLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Transform(bytes, TranslationOperation.Install);
                }));
            }
            finally { replacementLock?.Dispose(); }
            Equal(Original, File.ReadAllText(path));
            var backup = Directory.GetFiles(directory, "*.bak").Single();
            Equal(Original, File.ReadAllText(backup));
        }));
        Test("Fake Unity directory rejected", () => InTemporaryDirectory(directory =>
        {
            Directory.CreateDirectory(Path.Combine(directory, "Editor", "Data", "Localization"));
            File.WriteAllText(Path.Combine(directory, AppConstants.LocalizationRelativePath), Original);
            True(!UnityDetector.Inspect(directory).IsValid);
            Throws<InvalidOperationException>(() => TranslationInstaller.Execute(directory, TranslationOperation.Install));
            Equal(Original, File.ReadAllText(Path.Combine(directory, AppConstants.LocalizationRelativePath)));
        }));
        Test("Different executable version rejected", () => InTemporaryDirectory(directory =>
        {
            Directory.CreateDirectory(Path.Combine(directory, "Editor", "Data", "Localization"));
            File.Copy(Environment.ProcessPath!, Path.Combine(directory, "Editor", "Unity.exe"));
            File.WriteAllText(Path.Combine(directory, AppConstants.LocalizationRelativePath), Original);
            True(!UnityDetector.Inspect(directory).IsValid);
        }));
        if (args.Length == 2 && args[0] == "--inspect-unity")
            Test("Real Unity read-only detection and in-memory round trip", () =>
            {
                var unity = UnityDetector.Inspect(args[1]);
                True(unity.IsValid && unity.HasLanguagePack);
                var originalBytes = File.ReadAllBytes(unity.PoPath);
                var document = Utf8Document.Read(originalBytes);
                var installed = PoDocument.Install(document.Text, Sample);
                Equal(InstallationState.Installed, PoDocument.Inspect(installed).State);
                BytesEqual(originalBytes, document.GetBytes(PoDocument.Uninstall(installed)));
                Console.WriteLine($"  Read-only: {PoSyntax.ReadMessageIds(document.Text).Count} existing msgids, {originalBytes.Length} bytes");
            });
        Console.WriteLine($"{count - failures}/{count} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>一時ファイルでの更新とバックアップの統合検証</summary>
    private static void FileRoundTrip() => InTemporaryDirectory(directory =>
    {
        var path = CreatePo(directory);
        var original = File.ReadAllBytes(path);
        var installBackup = AtomicPoFile.Update(path, bytes => Transform(bytes, TranslationOperation.Install));
        BytesEqual(original, File.ReadAllBytes(installBackup));
        var installed = File.ReadAllBytes(path);
        Equal(InstallationState.Installed, PoDocument.Inspect(Utf8Document.Read(installed).Text).State);
        var reinstallBackup = AtomicPoFile.Update(path, bytes => Transform(bytes, TranslationOperation.Reinstall));
        BytesEqual(installed, File.ReadAllBytes(reinstallBackup));
        var uninstallBackup = AtomicPoFile.Update(path, bytes => Transform(bytes, TranslationOperation.Uninstall));
        BytesEqual(original, File.ReadAllBytes(path));
        True(File.Exists(uninstallBackup));
        Equal(3, Directory.GetFiles(directory, "*.bak").Length);
        Equal(0, Directory.GetFiles(directory, "*.tmp").Length);
    });

    /// <summary>UTF-8を保持したテスト用変換</summary>
    private static byte[] Transform(byte[] bytes, TranslationOperation operation)
    {
        var document = Utf8Document.Read(bytes);
        return document.GetBytes(PoDocument.Transform(document.Text, operation, Sample));
    }

    /// <summary>テスト用POファイルの作成</summary>
    private static string CreatePo(string directory)
    {
        var path = Path.Combine(directory, "ja.po");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(Original));
        return path;
    }

    /// <summary>テスト専用一時ディレクトリ内での処理</summary>
    private static void InTemporaryDirectory(Action<string> action)
    {
        var directory = Directory.CreateTempSubdirectory("vrcjp-tests-");
        try { action(directory.FullName); }
        finally { directory.Delete(true); }
    }

    /// <summary>文字列からのテスト用CSV読み込み</summary>
    private static IReadOnlyList<Translation> ReadCsv(string text)
    {
        using var reader = new StringReader(text);
        return TranslationCsvReader.Read(reader);
    }

    /// <summary>状態異常と削除拒否の検証</summary>
    private static void Abnormal(string text)
    {
        Equal(InstallationState.Abnormal, PoDocument.Inspect(text).State);
        Throws<InvalidDataException>(() => PoDocument.Uninstall(text));
    }

    /// <summary>単一テストの実行と結果集計</summary>
    private static void Test(string name, Action action)
    {
        count++;
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + "\n" + ex); }
    }

    /// <summary>等価性の検証</summary>
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}, actual {actual}");
    }

    /// <summary>条件の検証</summary>
    private static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed");
    }

    /// <summary>バイト列の完全一致の検証</summary>
    private static void BytesEqual(byte[] expected, byte[] actual) => True(expected.AsSpan().SequenceEqual(actual));

    /// <summary>想定例外の検証</summary>
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected exception: " + typeof(T).Name);
    }
}
