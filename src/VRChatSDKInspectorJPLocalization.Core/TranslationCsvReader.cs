using System.Text;
using System.Security.Cryptography;

namespace VRChatSDKInspectorJPLocalization.Core;

public static class TranslationCsvReader
{
    /// <summary>埋め込み翻訳CSVの読み込み</summary>
    public static IReadOnlyList<Translation> ReadEmbedded() => ReadEmbeddedData().Entries;

    /// <summary>埋め込みCSVと内容に基づく識別子の読み込み</summary>
    public static TranslationData ReadEmbeddedData()
    {
        using var stream = typeof(TranslationCsvReader).Assembly.GetManifestResourceStream("Translations.csv")
            ?? throw new InvalidDataException("埋め込み翻訳CSVが見つかりません。");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ReadData(buffer.ToArray());
    }

    /// <summary>同一のCSVバイト列からの翻訳とSHA256識別子の生成</summary>
    public static TranslationData ReadData(byte[] csvBytes)
    {
        using var stream = new MemoryStream(csvBytes, false);
        using var reader = new StreamReader(stream, Utf8Document.Encoding, true);
        return new TranslationData(Read(reader), "sha256:" + Convert.ToHexString(SHA256.HashData(csvBytes)));
    }

    /// <summary>コメント・引用符・複数行に対応した翻訳CSVの解析</summary>
    public static IReadOnlyList<Translation> Read(TextReader reader)
    {
        using var records = ReadRecords(Normalize(reader.ReadToEnd())).GetEnumerator();
        if (!records.MoveNext() || !records.Current.Fields.SequenceEqual(new[] { "English", "Japanese" }))
            throw new InvalidDataException("CSVの見出しは English,Japanese にしてください。");
        var result = new List<Translation>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (records.MoveNext())
        {
            var (line, fields) = records.Current;
            if (fields.Length != 2 || fields.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"CSVの{line}行に空欄または列数の誤りがあります。");
            if (!ids.Add(fields[0]))
                continue; // コンポーネント間の重複は先に記載された訳へ統合
            result.Add(new Translation(fields[0], fields[1]));
        }
        if (result.Count == 0)
            throw new InvalidDataException("CSVに翻訳がありません。");
        return result;
    }

    /// <summary>引用符の外側にあるコメント行のみを除外したCSVレコードの読み込み</summary>
    private static IEnumerable<(int Line, string[] Fields)> ReadRecords(string text)
    {
        var position = 0;
        var line = 1;
        while (position < text.Length)
        {
            var first = position;
            while (first < text.Length && text[first] is ' ' or '\t') first++;
            if (first == text.Length) yield break;
            if (text[first] is '#' or '\n')
            {
                var end = text.IndexOf('\n', first);
                if (end < 0) yield break;
                position = end + 1;
                line++;
                continue;
            }

            var startLine = line;
            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var closedQuote = false;
            while (position < text.Length)
            {
                var c = text[position++];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (position < text.Length && text[position] == '"')
                        {
                            field.Append('"');
                            position++;
                        }
                        else { quoted = false; closedQuote = true; }
                    }
                    else
                    {
                        field.Append(c);
                        if (c == '\n') line++;
                    }
                    continue;
                }
                if (c is ',' or '\n')
                {
                    if (c == '\n') { line++; break; }
                    fields.Add(field.ToString());
                    field.Clear();
                    closedQuote = false;
                }
                else if (closedQuote)
                {
                    if (c is not (' ' or '\t'))
                        throw new InvalidDataException($"CSVの{line}行で閉じ引用符の後に不正な文字があります。");
                }
                else if (c == '"')
                {
                    if (field.ToString().Any(character => character is not (' ' or '\t')))
                        throw new InvalidDataException($"CSVの{line}行で引用符の位置が不正です。");
                    field.Clear();
                    quoted = true;
                }
                else field.Append(c);
            }
            if (quoted)
                throw new InvalidDataException($"CSVの{startLine}行から始まる引用符が閉じられていません。");
            fields.Add(field.ToString());
            yield return (startLine, fields.ToArray());
        }
    }

    /// <summary>CSVフィールド内の改行の統一</summary>
    private static string Normalize(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
}
