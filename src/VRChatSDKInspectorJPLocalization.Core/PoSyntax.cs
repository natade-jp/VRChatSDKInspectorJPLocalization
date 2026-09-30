using System.Globalization;
using System.Text;

namespace VRChatSDKInspectorJPLocalization.Core;

public static class PoSyntax
{
    /// <summary>PO文字列のエスケープ</summary>
    public static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var c in value)
            result.Append(c switch
            {
                '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                '\a' => "\\a", '\b' => "\\b", '\f' => "\\f", '\v' => "\\v",
                _ when char.IsControl(c) => throw new InvalidDataException("翻訳に対応していない制御文字があります。"),
                _ => c.ToString()
            });
        return result.Append('"').ToString();
    }

    /// <summary>PO引用文字列の復号</summary>
    public static string Unquote(string value, bool preserveUnknownEscapes = false)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            throw new InvalidDataException("POの引用符が不正です。");
        var result = new StringBuilder();
        for (var i = 1; i < value.Length - 1; i++)
        {
            var c = value[i];
            // Unity標準POにはC1文字を含む文字列が存在するため、引用符とC0/DELのみを拒否
            if (c == '"' || (c < ' ' && c != '\t') || c == '\u007F')
                throw new InvalidDataException("PO文字列内のエスケープが不正です。");
            if (c != '\\') { result.Append(c); continue; }
            if (++i >= value.Length - 1)
                throw new InvalidDataException("POのエスケープが途中で終了しています。");
            c = value[i];
            if (c is >= '0' and <= '7')
            {
                var number = c - '0';
                for (var n = 1; n < 3 && i + 1 < value.Length - 1 && value[i + 1] is >= '0' and <= '7'; n++)
                    number = number * 8 + value[++i] - '0';
                result.Append((char)number);
            }
            else if (preserveUnknownEscapes && c is not ('\\' or '"' or 'n' or 'r' or 't' or 'a' or 'b' or 'f' or 'v'))
                result.Append('\\').Append(c); // Unity既存msgstrの非標準表記は解釈せず保持
            else
                result.Append(c switch
                {
                    '\\' => '\\', '"' => '"', 'n' => '\n', 'r' => '\r', 't' => '\t',
                    'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v',
                    _ => throw new InvalidDataException($"未対応のPOエスケープです: \\{c}")
                });
        }
        return result.ToString();
    }

    /// <summary>複数行・文脈・複数形を考慮した既存msgidの検出と構文検証</summary>
    public static HashSet<string> ReadMessageIds(string text)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<(string?, string)>();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        string? active = null;
        var lineNumber = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0)
            {
                FinishEntry(fields, ids, keys);
                active = null;
                continue;
            }
            if (line.StartsWith('#')) continue;
            if (line.StartsWith('"'))
            {
                if (active is null) throw new InvalidDataException($"POの{lineNumber}行に孤立した継続行があります。");
                fields[active] += Unquote(line, active.StartsWith("msgstr", StringComparison.Ordinal));
                continue;
            }
            var separator = line.IndexOfAny(new[] { ' ', '\t' });
            if (separator < 0) throw new InvalidDataException($"POの{lineNumber}行を解析できません。");
            var keyword = line[..separator];
            if ((keyword is "msgid" or "msgctxt") && fields.Keys.Any(k => k.StartsWith("msgstr", StringComparison.Ordinal)))
                FinishEntry(fields, ids, keys);
            if (keyword is not ("msgctxt" or "msgid" or "msgid_plural" or "msgstr") && !IsPluralTranslation(keyword))
                throw new InvalidDataException($"未対応のPO構文です（{lineNumber}行）。");
            if (fields.ContainsKey(keyword) ||
                (keyword == "msgctxt" && fields.Count != 0) ||
                (keyword == "msgid" && fields.Keys.Any(k => k != "msgctxt")) ||
                (keyword == "msgid_plural" && (!fields.ContainsKey("msgid") || fields.Keys.Any(k => k.StartsWith("msgstr", StringComparison.Ordinal)))) ||
                (keyword.StartsWith("msgstr", StringComparison.Ordinal) && !fields.ContainsKey("msgid")))
                throw new InvalidDataException($"POの項目順または重複が不正です（{lineNumber}行）。");
            try { fields.Add(keyword, Unquote(line[(separator + 1)..].Trim(), keyword.StartsWith("msgstr", StringComparison.Ordinal))); }
            catch (InvalidDataException ex) { throw new InvalidDataException($"POの{lineNumber}行: {ex.Message}", ex); }
            active = keyword;
        }
        FinishEntry(fields, ids, keys);
        return ids;
    }

    /// <summary>複数形翻訳の添字形式の判定</summary>
    private static bool IsPluralTranslation(string keyword) => keyword.StartsWith("msgstr[", StringComparison.Ordinal)
        && keyword.EndsWith(']') && int.TryParse(keyword.AsSpan(7, keyword.Length - 8), NumberStyles.None,
            CultureInfo.InvariantCulture, out var index) && index >= 0;

    /// <summary>POエントリの整合性確認とmsgidの収集</summary>
    private static void FinishEntry(Dictionary<string, string> fields, HashSet<string> ids, HashSet<(string?, string)> keys)
    {
        if (fields.Count == 0) return;
        if (!fields.TryGetValue("msgid", out var id)) throw new InvalidDataException("msgidのないPOエントリがあります。");
        if (fields.ContainsKey("msgid_plural"))
        {
            if (fields.ContainsKey("msgstr") || !fields.ContainsKey("msgstr[0]"))
                throw new InvalidDataException("複数形POエントリの翻訳が不正です。");
            var count = fields.Keys.Count(IsPluralTranslation);
            for (var i = 0; i < count; i++)
                if (!fields.ContainsKey($"msgstr[{i}]")) throw new InvalidDataException("複数形の添字が連続していません。");
        }
        else if (!fields.ContainsKey("msgstr") || fields.Keys.Any(IsPluralTranslation))
            throw new InvalidDataException("POエントリのmsgstrが不正です。");
        fields.TryGetValue("msgctxt", out var context);
        if (!keys.Add((context, id))) throw new InvalidDataException($"既存POに同じ文脈・msgidが重複しています: {id}");
        ids.Add(id); // 文脈付き・複数形でも標準のmsgidを保守的に優先
        fields.Clear();
    }
}
