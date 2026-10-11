using System.Text;
using System.Text.Json;

namespace Bible2PPT.TestSupport;

internal static class DiagnosticBibleFixture
{
    public static string FullText()
    {
        using var shape = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "bible-format.json")));
        var merged = shape.RootElement.GetProperty("combined").EnumerateArray().ToDictionary(
            item => (item[0].GetString()!, item[1].GetInt32(), item[2].GetInt32()), item => item[3].GetInt32());
        var missing = shape.RootElement.GetProperty("missing").EnumerateArray().Select(
            item => (item[0].GetString()!, item[1].GetInt32(), item[2].GetInt32())).ToHashSet();
        var wraps = shape.RootElement.GetProperty("continuations").EnumerateArray().Select(
            item => (item[0].GetString()!, item[1].GetInt32(), item[2].GetInt32())).ToHashSet();
        var text = new StringBuilder();
        foreach (var book in shape.RootElement.GetProperty("chapters").EnumerateObject())
        {
            var abbreviation = shape.RootElement.GetProperty("abbreviations").GetProperty(book.Name).GetString();
            var chapter = 0;
            foreach (var count in book.Value.EnumerateArray())
            {
                chapter++;
                for (var verse = 1; verse <= count.GetInt32(); verse++)
                {
                    if (missing.Contains((book.Name, chapter, verse))) continue;
                    var end = merged.TryGetValue((book.Name, chapter, verse), out var last) ? last : verse;
                    var label = end == verse ? $"{verse}" : $"{verse}-{end}";
                    text.AppendLine($"{abbreviation}{chapter}:{label} 진단용 {book.Name} {chapter}:{label}");
                    if (wraps.Contains((book.Name, chapter, verse)))
                        text.AppendLine($"{abbreviation}{chapter}:이어진 진단용 문장");
                    verse = end;
                }
                text.AppendLine();
            }
        }
        return text.ToString();
    }
}
