using System.Text;

namespace VRChatSDKInspectorJPLocalization.Core;

public sealed record Utf8Document(string Text, bool HasBom)
{
    public static UTF8Encoding Encoding { get; } = new(false, true);

    /// <summary>厳密なUTF-8とLFの検証およびBOMの保持</summary>
    public static Utf8Document Read(byte[] bytes)
    {
        var bom = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });
        var text = Encoding.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        if (text.Contains('\r') || text.Contains('\0') || text.Contains('\uFEFF'))
            throw new InvalidDataException("ja.poが想定するUTF-8・LF形式ではありません。自動変換せず中止します。");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("ja.poが空です。");
        return new Utf8Document(text, bom);
    }

    /// <summary>元のBOM有無を維持したUTF-8バイト列の生成</summary>
    public byte[] GetBytes(string text)
    {
        var body = Encoding.GetBytes(text);
        return HasBom ? new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray() : body;
    }
}
