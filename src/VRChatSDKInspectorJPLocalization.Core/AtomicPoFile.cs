using System.Security.Cryptography;

namespace VRChatSDKInspectorJPLocalization.Core;

public static class AtomicPoFile
{
    /// <summary>排他制御・バックアップ・一時ファイルによるPOファイルの置換</summary>
    public static string Update(string path, Func<byte[], byte[]> transform)
    {
        path = Path.GetFullPath(path);
        var mutexId = Convert.ToHexString(SHA256.HashData(Utf8Document.Encoding.GetBytes(path.ToUpperInvariant())));
        using var mutex = new Mutex(false, "Local\\VRChatSDKInspectorJP_" + mutexId);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("別の翻訳処理が実行中です。");
            if ((File.GetAttributes(path) & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("読み取り専用またはリンクされたja.poは変更できません。");

            // 書き込み共有を許可せず、File.Replaceに必要な削除共有だけを許可
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var snapshot = new MemoryStream();
            source.CopyTo(snapshot);
            var original = snapshot.ToArray();
            var output = transform(original);
            Utf8Document.Read(output);
            var backup = path + ".vrcjp-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".bak";
            // CreateNewにより既存バックアップの上書きを禁止
            WriteNew(backup, original);
            if (!File.ReadAllBytes(backup).AsSpan().SequenceEqual(original))
                throw new IOException("バックアップの検証に失敗しました。ja.poは変更していません。");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteNew(temporary, output);
                if (!File.ReadAllBytes(temporary).AsSpan().SequenceEqual(output))
                    throw new IOException("一時ファイルの検証に失敗しました。");
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
                    throw new IOException("処理中にja.poが他のプログラムから変更されました。");
                // WindowsのReplaceFileは対象への書き込みアクセスを必要とするため、直前に読み取りロックを解放
                source.Dispose();
                File.Replace(temporary, path, null);
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(output))
                    throw new IOException("置換後の検証に失敗しました。バックアップ: " + backup);
                return backup;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException("ja.poの更新に失敗しました。確認用バックアップ: " + backup, ex);
            }
            // 失敗時の一時ファイルは調査用に保持、成功時はFile.Replaceによって消費
        }
        finally
        {
            if (acquired) mutex.ReleaseMutex();
        }
    }

    /// <summary>新規ファイルの作成とディスクへのフラッシュ</summary>
    private static void WriteNew(string path, byte[] data)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(data);
        stream.Flush(true);
    }
}
