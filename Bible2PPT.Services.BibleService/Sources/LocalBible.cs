using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleIndexService;

namespace Bible2PPT.Sources;

/// <summary>Reads a user-supplied UTF-8 Bible file without making network requests.</summary>
public sealed class LocalBible : BibleSource
{
    // Keep the old web source IDs (0-2) reserved so their cached data is never reused.
    public const int DefaultSourceId = 3;
    private const string BibleOnlineId = "local-krv";
    private Lazy<Task<Document>> _document;

    public string FilePath { get; }
    public override bool CacheContent => false;

    public LocalBible(string? filePath = null)
    {
        Id = DefaultSourceId;
        Name = "로컬 성경 (JSON)";
        FilePath = Path.GetFullPath(filePath ?? Path.Combine(AppContext.BaseDirectory, "Bibles", "krv.json"));
        // One validated snapshot per process. Restart after replacing the file.
        _document = new Lazy<Task<Document>>(LoadAsync);
    }

    public void Reload() => _document = new Lazy<Task<Document>>(LoadAsync);

    public override async Task<List<Bible>> GetBiblesOnlineAsync()
    {
        var document = await _document.Value.ConfigureAwait(false);
        return new List<Bible>
        {
            new Bible { OnlineId = BibleOnlineId, Name = document.Name!, LanguageCode = document.LanguageCode! },
        };
    }

    public override async Task<List<Book>> GetBooksOnlineAsync(Bible bible)
    {
        if (bible.OnlineId != BibleOnlineId)
        {
            throw Invalid("선택한 성경이 로컬 본문 파일과 일치하지 않습니다.");
        }

        var document = await _document.Value.ConfigureAwait(false);
        return document.Books!.OrderBy(book => book.Key).Select(book => new Book
        {
            Key = book.Key,
            OnlineId = book.Key.ToString(),
            Name = book.Name!,
            Abbreviation = book.Abbreviation!,
            ChapterCount = book.Chapters!.Max(chapter => chapter.Number),
        }).ToList();
    }

    public override async Task<List<Chapter>> GetChaptersOnlineAsync(Book book)
    {
        var data = await FindBookAsync(book.Key).ConfigureAwait(false);
        return data.Chapters!.OrderBy(chapter => chapter.Number).Select(chapter => new Chapter
        {
            OnlineId = chapter.Number.ToString(CultureInfo.InvariantCulture),
            Number = chapter.Number,
        }).ToList();
    }

    public override async Task<List<Verse>> GetVersesOnlineAsync(Chapter chapter)
    {
        var book = await FindBookAsync(chapter.Book.Key).ConfigureAwait(false);
        var data = book.Chapters!.FirstOrDefault(item => item.Number == chapter.Number)
            ?? throw Invalid($"{book.Name} {chapter.Number}장 본문이 없습니다.");
        return data.Verses!.OrderBy(verse => verse.Number).Select(verse => new Verse
        {
            Number = verse.Number,
            Text = verse.Text!,
        }).ToList();
    }

    private async Task<BookData> FindBookAsync(BookKey key)
    {
        var document = await _document.Value.ConfigureAwait(false);
        return document.Books!.FirstOrDefault(book => book.Key == key)
            ?? throw Invalid($"{key} 본문이 없습니다.");
    }

    private async Task<Document> LoadAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            using var stream = File.OpenRead(FilePath);
            var document = await JsonSerializer.DeserializeAsync<Document>(stream, options).ConfigureAwait(false);
            if (document is null)
            {
                throw Invalid("JSON 최상위 객체가 비어 있습니다.");
            }

            Validate(document);
            return document;
        }
        catch (FileNotFoundException ex)
        {
            throw Invalid("본문 파일을 찾을 수 없습니다. 개역개정 본문을 UTF-8 JSON으로 저장한 뒤 프로그램을 다시 시작하세요.", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw Invalid("Bibles 폴더와 krv.json 본문 파일을 준비한 뒤 프로그램을 다시 시작하세요.", ex);
        }
        catch (JsonException ex)
        {
            throw Invalid($"JSON 형식이 잘못되었습니다 (경로: {ex.Path}, 줄: {ex.LineNumber + 1}).", ex);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw Invalid("본문 파일을 읽을 수 없습니다. 파일 권한과 다른 프로그램의 사용 여부를 확인하세요.", ex);
        }
    }

    private void Validate(Document document)
    {
        if (string.IsNullOrWhiteSpace(document.Name) || string.IsNullOrWhiteSpace(document.LanguageCode))
        {
            throw Invalid("name과 languageCode가 필요합니다.");
        }
        if (document.Books is null || document.Books.Count == 0)
        {
            throw Invalid("books에 최소 한 권의 본문이 필요합니다.");
        }

        var keys = new HashSet<BookKey>();
        foreach (var book in document.Books)
        {
            if (book is null || book.Key == BookKey.Unknown || !Enum.IsDefined(typeof(BookKey), book.Key))
            {
                throw Invalid("각 책의 key에 Genesis, Matthew 등 유효한 BookKey 이름을 사용하세요.");
            }
            if (!keys.Add(book.Key))
            {
                throw Invalid($"{book.Key} 책이 중복되어 있습니다.");
            }
            if (string.IsNullOrWhiteSpace(book.Name) || string.IsNullOrWhiteSpace(book.Abbreviation))
            {
                throw Invalid($"{book.Key}의 name과 abbreviation이 필요합니다.");
            }
            if (book.Chapters is null || book.Chapters.Count == 0)
            {
                throw Invalid($"{book.Name}의 chapters가 비어 있습니다.");
            }

            var chapterNumbers = new HashSet<int>();
            foreach (var chapter in book.Chapters)
            {
                if (chapter is null || chapter.Number < 1 || !chapterNumbers.Add(chapter.Number))
                {
                    throw Invalid($"{book.Name}의 장 번호는 양수여야 하며 중복될 수 없습니다.");
                }
                if (chapter.Verses is null || chapter.Verses.Count == 0)
                {
                    throw Invalid($"{book.Name} {chapter.Number}장의 verses가 비어 있습니다.");
                }

                var verseNumbers = new HashSet<int>();
                foreach (var verse in chapter.Verses)
                {
                    if (verse is null || verse.Number < 1 || !verseNumbers.Add(verse.Number))
                    {
                        throw Invalid($"{book.Name} {chapter.Number}장의 절 번호는 양수여야 하며 중복될 수 없습니다.");
                    }
                    if (string.IsNullOrWhiteSpace(verse.Text))
                    {
                        throw Invalid($"{book.Name} {chapter.Number}:{verse.Number}의 text가 비어 있습니다.");
                    }
                }
            }
        }
    }

    private InvalidDataException Invalid(string message, Exception? inner = null) =>
        new($"로컬 성경 파일: {FilePath}\n\n{message}", inner);

    private sealed class Document
    {
        public Document() { }
        public string? Name { get; set; }
        public string? LanguageCode { get; set; }
        public List<BookData>? Books { get; set; }
    }

    private sealed class BookData
    {
        public BookData() { }
        public BookKey Key { get; set; }
        public string? Name { get; set; }
        public string? Abbreviation { get; set; }
        public List<ChapterData>? Chapters { get; set; }
    }

    private sealed class ChapterData
    {
        public ChapterData() { }
        public int Number { get; set; }
        public List<VerseData>? Verses { get; set; }
    }

    private sealed class VerseData
    {
        public VerseData() { }
        public int Number { get; set; }
        public string? Text { get; set; }
    }
}
