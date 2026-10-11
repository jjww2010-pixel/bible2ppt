using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bible2PPT.Services.BibleService.Tests;

public sealed class ZippedBibleServiceTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-alignment", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;

    public ZippedBibleServiceTest()
    {
        Directory.CreateDirectory(_directory);
        var registrations = new ServiceCollection();
        registrations.AddBibleIndexService(options => options.UseSqlite($"Data Source={_directory}/index.db;Pooling=False"));
        registrations.AddBibleService(options => options.UseSqlite($"Data Source={_directory}/bible.db;Pooling=False"));
        _services = registrations.BuildServiceProvider();
        _services.UseBibleIndexService(); _services.UseBibleService();
    }

    private async Task<Book> Book(int id, Dictionary<int, int[]> rows)
    {
        var service = _services.GetRequiredService<BibleService>();
        var bible = Assert.Single(await service.GetBiblesAsync(new SampleSource(id, rows)));
        return Assert.Single(await service.GetBooksAsync(bible));
    }

    [Fact]
    public async Task AlignsSparseChaptersWithoutRepeatingExhaustedSources()
    {
        var first = await Book(100, new() { [1] = new[] { 1 }, [3] = new[] { 1 } });
        var second = await Book(101, new() { [2] = new[] { 1 } });
        var actual = new List<Chapter?[]>();
        await foreach (var row in _services.GetRequiredService<ZippedBibleService>().GetChaptersAsync(new[] { first, second }, CancellationToken.None))
            actual.Add(row.ToArray());
        Assert.Equal(3, actual.Count);
        Assert.Equal(1, actual[0][0]!.Number); Assert.Null(actual[0][1]);
        Assert.Null(actual[1][0]); Assert.Equal(2, actual[1][1]!.Number);
        Assert.Equal(3, actual[2][0]!.Number); Assert.Null(actual[2][1]);
    }

    [Fact]
    public async Task AlignsUnequalAndSparseVersesIncludingAMissingSource()
    {
        var first = await Book(100, new() { [1] = new[] { 1, 3, 9 } });
        var second = await Book(101, new() { [1] = new[] { 2 } });
        var service = _services.GetRequiredService<BibleService>();
        var chapters = new Chapter?[] { Assert.Single(await service.GetChaptersAsync(first)), Assert.Single(await service.GetChaptersAsync(second)), null };
        var actual = new List<Verse?[]>();
        await foreach (var row in _services.GetRequiredService<ZippedBibleService>().GetVersesAsync(chapters, CancellationToken.None))
            actual.Add(row.ToArray());
        Assert.Equal(4, actual.Count);
        Assert.Equal(new[] { 1, 2, 3, 9 }, actual.Select(row => row.First(verse => verse is not null)!.Number));
        Assert.Null(actual[0][1]); Assert.Null(actual[1][0]);
        Assert.Null(actual[2][1]); Assert.Null(actual[3][1]);
        Assert.All(actual, row => Assert.Null(row[2]));
    }

    [Fact]
    public async Task EmptyInputsProduceNoRows()
    {
        var zipped = _services.GetRequiredService<ZippedBibleService>();
        var book = await Book(100, new());
        await foreach (var row in zipped.GetChaptersAsync(new[] { book }, CancellationToken.None)) Assert.True(false, "Empty book returned chapters.");
        await foreach (var row in zipped.GetChaptersAsync(Array.Empty<Book>(), CancellationToken.None)) Assert.True(false, "Empty input returned chapters.");
        await foreach (var row in zipped.GetVersesAsync(new Chapter?[] { null }, CancellationToken.None)) Assert.True(false, "Missing input returned verses.");
    }

    [Fact]
    public async Task CancellationIsObservedWhileEnumeratingPassages()
    {
        var book = await Book(100, new() { [1] = new[] { 1, 2 } });
        var chapter = Assert.Single(await _services.GetRequiredService<BibleService>().GetChaptersAsync(book));
        using var stop = new CancellationTokenSource();
        await using var enumerator = _services.GetRequiredService<ZippedBibleService>().GetVersesAsync(new[] { chapter }, stop.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
    }

    public void Dispose() { _services.Dispose(); Directory.Delete(_directory, recursive: true); }

    private sealed class SampleSource : BibleSource
    {
        private readonly Dictionary<int, int[]> _rows;
        public SampleSource(int id, Dictionary<int, int[]> rows) { Id = id; Name = "Diagnostic source"; _rows = rows; }
        public override bool CacheContent => false;
        public override Task<List<Bible>> GetBiblesOnlineAsync() => Task.FromResult(new List<Bible> { new() { OnlineId = $"test-{Id}", Name = Name, LanguageCode = "ko" } });
        public override Task<List<Book>> GetBooksOnlineAsync(Bible bible) => Task.FromResult(new List<Book> { new() { Key = BookKey.Genesis, OnlineId = "Genesis", Name = "창세기", Abbreviation = "창", ChapterCount = 3 } });
        public override Task<List<Chapter>> GetChaptersOnlineAsync(Book book) => Task.FromResult(_rows.Keys.Select(number => new Chapter { Number = number, OnlineId = $"{number}" }).ToList());
        public override Task<List<Verse>> GetVersesOnlineAsync(Chapter chapter) => Task.FromResult(_rows[chapter.Number].Select(number => new Verse { Number = number, Text = $"진단용 {number}" }).ToList());
    }
}
