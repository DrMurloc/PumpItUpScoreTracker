using System.Text;

namespace ScoreTracker.Web.Services;

/// <summary>
///     How the site's CSV downloads are encoded: UTF-8 with a byte-order mark. Without the mark,
///     Excel opens the file in the system codepage and garbles every non-ASCII title, and a
///     player who edits the file there and saves it re-uploads names that no longer match the
///     catalog. The upload readers skip the mark (StreamReader detects it by default).
/// </summary>
public static class CsvText
{
    /// <summary>
    ///     UTF-8 that writes its byte-order mark. A StreamWriter over this encoding emits the mark
    ///     on its first write to an empty stream; <see cref="Encoding.GetBytes(string)" /> never
    ///     emits it, which is what <see cref="ToBytes" /> is for.
    /// </summary>
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

    /// <summary>The content type a CSV file response is served with.</summary>
    public const string ContentType = "text/csv; charset=utf-8";

    /// <summary>The file's bytes: the UTF-8 byte-order mark, then the text.</summary>
    public static byte[] ToBytes(string csv)
    {
        var preamble = Utf8WithBom.GetPreamble();
        var bytes = new byte[preamble.Length + Utf8WithBom.GetByteCount(csv)];
        preamble.CopyTo(bytes, 0);
        Utf8WithBom.GetBytes(csv, 0, csv.Length, bytes, preamble.Length);
        return bytes;
    }
}
