using System.Text;
using System.Text.Json;
using Bible2PPT.Bibles;
using Bible2PPT.Data;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bible2PPT.Services.BibleService.Tests;

public sealed class LocalBibleTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "krv.json");

    public LocalBibleTest() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ReadsLocalTextThroughThePptQueryPipelineWithoutCachingContent()
    {
        WriteBible();
        using var services = CreateServices();
        var service = services.GetRequiredService<BibleService>();
        var source = new LocalBible(FilePath);
        var bible = Assert.Single(await service.GetBiblesAsync(source));
        Assert.True(bible.Id > 0);
        Assert.Same(source, bible.Source);

        var query = Assert.Single(services.GetRequiredService<VerseQueryParser>().ParseVerseQueries("창1:1-2"));
        var zipped = services.GetRequiredService<ZippedBibleService>();
        var book = Assert.Single(await zipped.FindBookAsync(new[] { bible }, query.BookKey, CancellationToken.None));
        Assert.NotNull(book);
        Assert.Equal("창세기", book!.NameInfo);
        Assert.Equal("창", book.AbbreviationInfo);

        var chapters = new List<Chapter>();
        await foreach (var each in zipped.GetChaptersAsync(new[] { book }, CancellationToken.None))
        {
            chapters.Add(Assert.Single(each)!);
        }
        Assert.Equal(new[] { 1, 2 }, chapters.Select(chapter => chapter.Number));
        Assert.All(chapters, chapter => Assert.Same(book, chapter.Book));

        var verses = new List<Verse>();
        await foreach (var each in zipped.GetVersesAsync(new[] { chapters[0] }, CancellationToken.None))
        {
            verses.Add(Assert.Single(each)!);
        }
        Assert.Equal(new[] { 1, 2 }, verses.Select(verse => verse.Number));
        Assert.Equal(new[] { "테스트 본문 1", "테스트 본문 2" }, verses.Select(verse => verse.Text));
        Assert.All(verses, verse => Assert.Same(chapters[0], verse.Chapter));

        var restored = service.FindBible(bible.Id);
        Assert.NotNull(restored);
        Assert.IsType<LocalBible>(restored!.Source);
        using var db = services.GetRequiredService<IDbContextFactory<BibleContext>>().CreateDbContext();
        Assert.Single(db.Bibles);
        Assert.Empty(db.Books);
        Assert.Empty(db.Chapters);
        Assert.Empty(db.Verses);
    }

    [Fact]
    public async Task ReplacingFileAndReopeningSourceKeepsIdentityAndReadsNewText()
    {
        WriteBible();
        using var services = CreateServices();
        var service = services.GetRequiredService<BibleService>();
        var firstSource = new LocalBible(FilePath);
        var first = Assert.Single(await service.GetBiblesAsync(firstSource));
        var firstBook = Assert.Single(await service.GetBooksAsync(first));
        var firstChapter = (await service.GetChaptersAsync(firstBook))[0];
        Assert.Equal("테스트 본문 1", (await service.GetVersesAsync(firstChapter))[0].Text);

        WriteBible("교체한 본문", "개역개정 로컬");
        var reopened = Assert.Single(await service.GetBiblesAsync(new LocalBible(FilePath)));
        Assert.Equal(first.Id, reopened.Id);
        Assert.Equal("개역개정 로컬", service.FindBible(first.Id)!.Name);
        var book = Assert.Single(await service.GetBooksAsync(reopened));
        var chapter = (await service.GetChaptersAsync(book))[0];
        Assert.Equal("교체한 본문", (await service.GetVersesAsync(chapter))[0].Text);
    }

    [Fact]
    public async Task CachedBibleDoesNotHideMissingFile()
    {
        WriteBible();
        using var services = CreateServices();
        var service = services.GetRequiredService<BibleService>();
        await service.GetBiblesAsync(new LocalBible(FilePath));
        File.Delete(FilePath);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.GetBiblesAsync(new LocalBible(FilePath)));
        Assert.Contains(FilePath, exception.Message);
        Assert.Contains("본문 파일을 찾을 수 없습니다", exception.Message);
    }

    [Fact]
    public async Task ExistingWebCacheCannotSupplyLocalVerses()
    {
        WriteBible();
        using var services = CreateServices();
        using (var db = services.GetRequiredService<IDbContextFactory<BibleContext>>().CreateDbContext())
        {
            var webBible = new Bible { SourceId = 0, OnlineId = "rvsn", Name = "웹 캐시", LanguageCode = "ko" };
            var book = new Book { SourceId = 0, Bible = webBible, Key = BookKey.Genesis, OnlineId = "창", Name = "창세기", Abbreviation = "창", ChapterCount = 1 };
            var chapter = new Chapter { SourceId = 0, Book = book, OnlineId = "1", Number = 1 };
            db.Verses.Add(new Verse { SourceId = 0, Chapter = chapter, Number = 1, Text = "오래된 웹 본문" });
            db.SaveChanges();
        }
        var service = services.GetRequiredService<BibleService>();
        var bible = Assert.Single(await service.GetBiblesAsync(new LocalBible(FilePath)));
        Assert.Equal(LocalBible.DefaultSourceId, bible.SourceId);
        var localBook = Assert.Single(await service.GetBooksAsync(bible));
        var localChapter = (await service.GetChaptersAsync(localBook))[0];
        Assert.Equal("테스트 본문 1", (await service.GetVersesAsync(localChapter))[0].Text);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"name\":\"개역개정\",\"languageCode\":\"ko\",\"books\":[]}")]
    [InlineData("{\"name\":\"개역개정\",\"languageCode\":\"ko\",\"books\":[null]}")]
    [InlineData("{\"name\":\"개역개정\",\"languageCode\":\"ko\",\"books\":[{\"key\":\"BadBook\"}]}")]
    [InlineData("{\"name\":\"개역개정\",\"languageCode\":\"ko\",\"books\":[{\"key\":1}]}")]
    [InlineData("{\"name\":\"개역개정\",\"languageCode\":\"ko\",\"books\":[{\"key\":\"Unknown\"}]}")]
    public async Task RejectsMalformedOrIncompleteDocumentsWithFilePath(string json)
    {
        File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => new LocalBible(FilePath).GetBiblesOnlineAsync());
        Assert.Contains(FilePath, exception.Message);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("chapter")]
    [InlineData("verse")]
    [InlineData("empty_text")]
    [InlineData("empty_chapters")]
    [InlineData("empty_verses")]
    [InlineData("zero_chapter")]
    [InlineData("negative_verse")]
    [InlineData("null_chapter")]
    [InlineData("null_verse")]
    public async Task RejectsInvalidNumbersDuplicateEntriesAndEmptyContent(string problem)
    {
        WriteBible();
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(FilePath))!;
        var books = document["books"]!.AsArray();
        var chapters = books[0]!["chapters"]!.AsArray();
        var verses = chapters[0]!["verses"]!.AsArray();
        switch (problem)
        {
            case "book": books.Add(System.Text.Json.Nodes.JsonNode.Parse(books[0]!.ToJsonString())); break;
            case "chapter": chapters.Add(System.Text.Json.Nodes.JsonNode.Parse(chapters[0]!.ToJsonString())); break;
            case "verse": verses.Add(System.Text.Json.Nodes.JsonNode.Parse(verses[0]!.ToJsonString())); break;
            case "empty_text": verses[0]!["text"] = " "; break;
            case "empty_chapters": chapters.Clear(); break;
            case "empty_verses": verses.Clear(); break;
            case "zero_chapter": chapters[0]!["number"] = 0; break;
            case "negative_verse": verses[0]!["number"] = -1; break;
            case "null_chapter": chapters.Add((System.Text.Json.Nodes.JsonNode?)null); break;
            case "null_verse": verses.Add((System.Text.Json.Nodes.JsonNode?)null); break;
        }
        File.WriteAllText(FilePath, document.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => new LocalBible(FilePath).GetBiblesOnlineAsync());
    }

    [Fact]
    public async Task NewTestamentUsesBookKeysRatherThanNumericOffsets()
    {
        WriteBible(key: "Matthew");
        using var services = CreateServices();
        var service = services.GetRequiredService<BibleService>();
        var bible = Assert.Single(await service.GetBiblesAsync(new LocalBible(FilePath)));
        var book = Assert.Single(await service.GetBooksAsync(bible));
        var query = Assert.Single(services.GetRequiredService<VerseQueryParser>().ParseVerseQueries("마1"));
        Assert.Equal(query.BookKey, book.Key);
        Assert.Equal(BookKey.Matthew, book.Key);
    }

    [Fact]
    public void OnlyLocalSourceIsAvailable() => Assert.IsType<LocalBible>(Assert.Single(BibleSource.AvailableSources));

    private void WriteBible(string text = "테스트 본문 1", string name = "개역개정", string key = "Genesis")
    {
        var document = new
        {
            name,
            languageCode = "ko",
            books = new[]
            {
                new
                {
                    key, name = "창세기", abbreviation = "창",
                    chapters = new[]
                    {
                        new { number = 2, verses = new[] { new { number = 1, text = "다음 장 테스트" } } },
                        new { number = 1, verses = new[] { new { number = 2, text = "테스트 본문 2" }, new { number = 1, text } } },
                    },
                },
            },
        };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(document), new UTF8Encoding(false));
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddBibleIndexService(options => options.UseSqlite($"Data Source={Path.Combine(_directory, "index.db")};Pooling=False"));
        services.AddBibleService(options => options.UseSqlite($"Data Source={Path.Combine(_directory, "bible.db")};Pooling=False"));
        var provider = services.BuildServiceProvider();
        provider.UseBibleIndexService();
        provider.UseBibleService();
        return provider;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
