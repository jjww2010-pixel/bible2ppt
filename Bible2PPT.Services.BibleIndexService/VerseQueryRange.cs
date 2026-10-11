using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Bible2PPT.Services.BibleIndexService;

public class VerseQueryRange //: ISpanParsable<VerseQueryRange>
{
    private static readonly Regex _regex = new(@"^(?<chapFrom>\d+)(?::(?<versFrom>\d+))?(?:-(?<chapTo>\d+)(?::(?<versTo>\d+))?)?$", RegexOptions.Compiled);

    public int StartChapterNumber { get; set; } = 1;
    public int StartVerseNumber { get; set; } = 1;
    public int? EndChapterNumber { get; set; }
    public int? EndVerseNumber { get; set; }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [NotNullWhen(true)] out VerseQueryRange? result)
    {
        result = null;
        var match = _regex.Match(s.ToString());
        if (!match.Success) return false;
        bool Positive(string group, out int value) =>
            int.TryParse(match.Groups[group].Value, System.Globalization.NumberStyles.None, provider, out value) && value > 0;
        if (!Positive("chapFrom", out var startChapter)) return false;
        var candidate = new VerseQueryRange { StartChapterNumber = startChapter, EndChapterNumber = startChapter };
        var hasStartVerse = match.Groups["versFrom"].Success;
        if (hasStartVerse)
        {
            if (!Positive("versFrom", out var startVerse)) return false;
            candidate.StartVerseNumber = startVerse;
            candidate.EndVerseNumber = startVerse;
        }
        if (match.Groups["chapTo"].Success)
        {
            if (!Positive("chapTo", out var end)) return false;
            if (hasStartVerse && !match.Groups["versTo"].Success)
                candidate.EndVerseNumber = end;
            else
            {
                candidate.EndChapterNumber = end;
                candidate.EndVerseNumber = null;
                if (match.Groups["versTo"].Success)
                {
                    if (!Positive("versTo", out var endVerse)) return false;
                    candidate.EndVerseNumber = endVerse;
                }
            }
        }
        if (candidate.EndChapterNumber < candidate.StartChapterNumber ||
            (candidate.EndChapterNumber == candidate.StartChapterNumber && candidate.EndVerseNumber < candidate.StartVerseNumber))
            return false;
        result = candidate;
        return true;
    }
}
