using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;

namespace Bible2PPT.Services.BibleService;

/// <summary>Imports a supplied Bible file without requiring Python or a network connection.</summary>
public static class LocalBibleImporter
{
    public static async Task ImportAsync(string inputPath, string outputPath)
    {
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            await new LocalBible(inputPath).GetBiblesOnlineAsync().ConfigureAwait(false);
            return;
        }

        // Parse and validate before replacing a previously working Bible file.
        var content = await ReadTextAsync(inputPath).ConfigureAwait(false);
        string json;
        if (Path.GetExtension(inputPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                json = document.RootElement.ValueKind == JsonValueKind.Array
                    ? ConvertRows(ReadJsonRows(document.RootElement))
                    : content;
            }
            catch (Exception ex) when (ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException)
            {
                throw new InvalidDataException("JSON의 book, chapter, verse, text/content 필드를 확인하세요.", ex);
            }
        }
        else
        {
            json = ConvertRows(ReadTextRows(content));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".bible-import-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false)).ConfigureAwait(false);
            await new LocalBible(temporaryPath).GetBiblesOnlineAsync().ConfigureAwait(false);
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task<string> ReadTextAsync(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path, new UTF8Encoding(false, true)).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                return await File.ReadAllTextAsync(path, Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)).ConfigureAwait(false);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("본문 파일을 UTF-8 또는 CP949로 저장하세요.", ex);
            }
        }
    }

    private static IEnumerable<Row> ReadJsonRows(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            var book = item.GetProperty("book");
            var text = item.TryGetProperty("text", out var body) ? body : item.GetProperty("content");
            yield return new Row(
                GetBookKey(book.ValueKind == JsonValueKind.String ? book.GetString()! : book.GetRawText()),
                item.GetProperty("chapter").GetInt32(), item.GetProperty("verse").GetInt32(), text.GetString()!);
        }
    }

    private static IEnumerable<Row> ReadTextRows(string content)
    {
        using var reader = new StringReader(content);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
            var fields = line.Split('\t', 4);
            if (fields.Length != 4)
            {
                var match = Regex.Match(line, @"^\s*(.+?)\s*([0-9]+):([0-9]+)\s+(.+)$");
                if (!match.Success)
                {
                    throw new InvalidDataException($"TXT {lineNumber}번째 줄: '창1:1 본문' 또는 책/장/절/본문 TSV 형식이 필요합니다.");
                }
                fields = Enumerable.Range(1, 4).Select(index => match.Groups[index].Value).ToArray();
            }
            if (!int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var chapter) ||
                !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var verse))
            {
                throw new InvalidDataException($"TXT {lineNumber}번째 줄의 장·절 번호를 확인하세요.");
            }
            yield return new Row(GetBookKey(fields[0]), chapter, verse, fields[3]);
        }
    }

    private static string ConvertRows(IEnumerable<Row> input)
    {
        var rows = input.ToList();
        if (rows.Count == 0) throw new InvalidDataException("본문 파일에 구절이 없습니다.");
        foreach (var row in rows)
        {
            if (row.Chapter < 1 || row.Verse < 1 || string.IsNullOrWhiteSpace(row.Text))
                throw new InvalidDataException($"{row.Key} {row.Chapter}:{row.Verse}: 장·절 번호는 양수여야 하며 본문이 필요합니다.");
        }
        if (rows.GroupBy(row => (row.Key, row.Chapter, row.Verse)).Any(group => group.Count() > 1))
            throw new InvalidDataException("중복된 성경 구절이 있습니다.");

        var document = new
        {
            name = "개역개정", languageCode = "ko",
            books = rows.GroupBy(row => row.Key).OrderBy(group => group.Key).Select(book => new
            {
                key = book.Key.ToString(),
                name = BibleBookCatalog.Entries.First(item => item.Key == book.Key).Name,
                abbreviation = BibleBookCatalog.Entries.First(item => item.Key == book.Key).Abbreviation,
                chapters = book.GroupBy(row => row.Chapter).OrderBy(group => group.Key).Select(chapter => new
                {
                    number = chapter.Key,
                    verses = chapter.OrderBy(row => row.Verse).Select(row => new { number = row.Verse, text = row.Text }),
                }),
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    private static BookKey GetBookKey(string value)
    {
        value = Regex.Replace(value, @"\s+", "");
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= 66)
            return BibleBookCatalog.Entries[number - 1].Key;
        if (value is "나훔" or "나훔서") return BookKey.Nahum;
        if (value == "아가서") return BookKey.SongOfSolomon;
        foreach (var entry in BibleBookCatalog.Entries)
        {
            if (string.Equals(value, entry.Key.ToString(), StringComparison.OrdinalIgnoreCase) || value == entry.Name || value == entry.Abbreviation)
                return entry.Key;
        }
        throw new InvalidDataException($"알 수 없는 책 이름 또는 번호: {value}. 숫자 책 번호는 1~66을 사용하세요.");
    }

    private sealed record Row(BookKey Key, int Chapter, int Verse, string Text);
}
