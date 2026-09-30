using Microsoft.VisualBasic.FileIO;

namespace VRChatSDKInspectorJPLocalization.Core;

public static class TranslationCsvReader
{
    /// <summary>埋め込み翻訳CSVの読み込み</summary>
    public static IReadOnlyList<Translation> ReadEmbedded()
    {
        using var stream = typeof(TranslationCsvReader).Assembly.GetManifestResourceStream("Translations.csv")
            ?? throw new InvalidDataException("埋め込み翻訳CSVが見つかりません。");
        using var reader = new StreamReader(stream, Utf8Document.Encoding, true);
        return Read(reader);
    }

    /// <summary>引用符と複数行に対応した翻訳CSVの解析</summary>
    public static IReadOnlyList<Translation> Read(TextReader reader)
    {
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        try
        {
            var header = parser.ReadFields();
            if (header is null || !header.SequenceEqual(new[] { "English", "Japanese" }))
                throw new InvalidDataException("CSVの見出しは English,Japanese にしてください。");
            var result = new List<Translation>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is null || fields.Length != 2 || fields.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException($"CSVの{parser.LineNumber}行付近に空欄または列数の誤りがあります。");
                var english = Normalize(fields[0]);
                var japanese = Normalize(fields[1]);
                if (!ids.Add(english))
                    throw new InvalidDataException($"CSVのEnglishが重複しています: {english}");
                result.Add(new Translation(english, japanese));
            }
            if (result.Count == 0)
                throw new InvalidDataException("CSVに翻訳がありません。");
            return result;
        }
        catch (MalformedLineException ex)
        {
            throw new InvalidDataException($"CSVの引用符などの形式が不正です（{ex.LineNumber}行）。", ex);
        }
    }

    /// <summary>CSVフィールド内の改行の統一</summary>
    private static string Normalize(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
}
