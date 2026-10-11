using System.Text;
using Bible2PPT.Bibles;
using Bible2PPT.PPT;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Services.BuildService;
using Bible2PPT.Services.TemplateService;
using Bible2PPT.Sources;
using Bible2PPT.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using LocalBibleService = Bible2PPT.Services.BibleService.BibleService;

namespace Bible2PPT.Services.BuildService.Tests;

public sealed class BuilderTest : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-builder-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider _services = null!;
    private TestBuilder _builder = null!;
    private Bible _bible = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        var registrations = new ServiceCollection();
        registrations.AddBibleIndexService(options => options.UseSqlite($"Data Source={_directory}/index.db;Pooling=False"));
        registrations.AddBibleService(options => options.UseSqlite($"Data Source={_directory}/bible.db;Pooling=False"));
        registrations.AddDbContextFactory<BuildContext>(options => options.UseSqlite($"Data Source={_directory}/build.db;Pooling=False"));
        _services = registrations.BuildServiceProvider();
        _services.UseBibleIndexService(); _services.UseBibleService(); _services.UseBuildService();
        _builder = new TestBuilder(_services.GetRequiredService<ZippedBibleService>(),
            _services.GetRequiredService<VerseQueryParser>(), _services.GetRequiredService<IDbContextFactory<BuildContext>>());
        await SetText("창1:1 시작\n창1:2-3 통합\n창1:4 마지막\n창3:1 다른 장\n요일4:8 좋아하는 구절 테스트");
    }

    private async Task SetText(string text)
    {
        var input = Path.Combine(_directory, "input.txt");
        var output = Path.Combine(_directory, "krv.json");
        await File.WriteAllTextAsync(input, text, new UTF8Encoding(true));
        await LocalBibleImporter.ImportAsync(input, output);
        _bible = Assert.Single(await _services.GetRequiredService<LocalBibleService>().GetBiblesAsync(new LocalBible(output)));
    }

    private Job Job(string query, bool split = false) => new()
    {
        Bibles = new[] { _bible }, QueryString = query, SplitChaptersIntoFiles = split,
        OutputDestination = Path.Combine(_directory, "output"), Template = new Template(),
    };

    [Theory]
    [InlineData("요일 4:8", 1)]
    [InlineData("창1:3", 1)]
    [InlineData("창1:2-4", 2)]
    [InlineData("창1-3", 4)]
    [InlineData("창1:3-3:1", 3)]
    [InlineData("창1:1 요일4:8", 2)]
    public async Task ProducesRequestedPassagesAndSavesOnce(string query, int count)
    {
        await _builder.Run(Job(query));
        Assert.Equal(count, _builder.Writers.Sum(writer => writer.Passages.Count));
        Assert.Single(_builder.Writers);
        Assert.True(_builder.Writers[0].Saved);
        Assert.True(_builder.Writers[0].Disposed);
    }

    [Fact]
    public async Task SplitsChaptersIntoDistinctOutputFiles()
    {
        await _builder.Run(Job("창1-3", split: true));
        Assert.Equal(2, _builder.Writers.Count);
        Assert.EndsWith(Path.Combine("창세기", "001.pptx"), _builder.Writers[0].Output);
        Assert.EndsWith(Path.Combine("창세기", "003.pptx"), _builder.Writers[1].Output);
        Assert.All(_builder.Writers, writer => Assert.True(writer.Saved && writer.Disposed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("창99:1")]
    [InlineData("창1:99")]
    [InlineData("요1:1")]
    public async Task MissingInputFailsWithoutSavingAnEmptyPresentation(string query)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => _builder.Run(Job(query)));
        Assert.DoesNotContain(_builder.Writers, writer => writer.Saved);
    }

    [Theory]
    [InlineData("창0:1")]
    [InlineData("창1:0")]
    [InlineData("창3-1")]
    [InlineData("창1:9-1")]
    [InlineData("창99999999999999999999")]
    [InlineData("없는책1:1")]
    public async Task MalformedQueryFailsBeforeCreatingAnOutput(string query)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _builder.Run(Job(query)));
        Assert.Empty(_builder.Writers);
    }

    [Fact]
    public async Task RenderingErrorStopsProducerAndAllowsNextJob()
    {
        await SetText(string.Join("\n", Enumerable.Range(1, 30).Select(chapter => $"창{chapter}:1 진단용")));
        _builder.ThrowOnAppend = true;
        await Assert.ThrowsAsync<IOException>(() => _builder.Run(Job("창")));
        Assert.All(_builder.Writers, writer => Assert.True(writer.Disposed && !writer.Saved));
        _builder.ThrowOnAppend = false;
        await _builder.Run(Job("창1"));
        Assert.True(_builder.Writers.Last().Saved);
    }

    [Fact]
    public async Task SaveErrorIsReportedAndWriterIsDisposed()
    {
        _builder.ThrowOnSave = true;
        await Assert.ThrowsAsync<IOException>(() => _builder.Run(Job("창1")));
        Assert.True(Assert.Single(_builder.Writers).Disposed);
    }

    [Theory]
    [InlineData("창")]
    [InlineData("창1")]
    public async Task CancellationStopsProductionBeforeSaving(string query)
    {
        using var stop = new CancellationTokenSource();
        _builder.AfterAppend = stop.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _builder.Run(Job(query), stop.Token));
        Assert.DoesNotContain(_builder.Writers, writer => writer.Saved);
    }

    [Fact]
    public async Task WholeBibleFormatPassesThroughTheActualJobBuilderWithoutDroppingOrRepeatingPassages()
    {
        await SetText(DiagnosticBibleFixture.FullText());
        var books = await _services.GetRequiredService<LocalBibleService>().GetBooksAsync(_bible);
        await _builder.Run(Job(string.Join(" ", books.Select(book => book.AbbreviationInfo))), timeoutSeconds: 120);
        var passages = Assert.Single(_builder.Writers).Passages;
        Assert.Equal(31088, passages.Count);
        Assert.Equal(1189, passages.Select(item => (item.Key, item.Chapter)).Distinct().Count());
        Assert.Equal(66, passages.Select(item => item.Key).Distinct().Count());
        Assert.Equal(passages.Count, passages.Select(item => (item.Key, item.Chapter, item.Start)).Distinct().Count());
        Assert.Equal(31101, passages.Sum(item => item.End - item.Start + 1));
        Assert.Equal(45, passages.Count(item => item.Text.Contains("이어진 진단용 문장")));
        Assert.Equal(11, passages.Count(item => item.End > item.Start));
        Assert.Single(passages.Where(item => item.Key == BookKey.IJohn && item.Chapter == 4 && item.Start == 8));
    }

    public async Task DisposeAsync()
    {
        await _builder.DisposeAsync();
        await _services.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    private sealed class TestBuilder : Builder
    {
        public List<RecordingWriter> Writers { get; } = new();
        public bool ThrowOnAppend, ThrowOnSave;
        public Action? AfterAppend;
        public TestBuilder(ZippedBibleService zipped, VerseQueryParser parser, IDbContextFactory<BuildContext> factory)
            : base(null!, zipped, parser, factory) { }
        public async Task Run(Job job, CancellationToken token = default, int timeoutSeconds = 10)
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            stop.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await ProcessAsync(job, stop.Token).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds + 2));
        }
        protected override IPresentationWriter CreatePresentation(Job job, string output)
        {
            var writer = new RecordingWriter(output, this); Writers.Add(writer); return writer;
        }
    }

    private sealed class RecordingWriter : IPresentationWriter
    {
        public string Output { get; }
        public List<(BookKey Key, int Chapter, int Start, int End, string Text)> Passages { get; } = new();
        public bool Saved, Disposed;
        private readonly TestBuilder _owner;
        public RecordingWriter(string output, TestBuilder owner) { Output = output; _owner = owner; }
        public async Task AppendChapter(IAsyncEnumerable<IEnumerable<Verse?>> rows, Book book, Chapter chapter, int start, int? end, CancellationToken token)
        {
            if (_owner.ThrowOnAppend) throw new IOException("Simulated PowerPoint/file error");
            await foreach (var row in rows.WithCancellation(token))
            {
                token.ThrowIfCancellationRequested();
                var verse = row.First(item => item is not null)!;
                Passages.Add((book.Key, chapter.Number, verse.Number, verse.EndNumber ?? verse.Number, verse.Text));
            }
            _owner.AfterAppend?.Invoke();
        }
        public void Save()
        {
            if (_owner.ThrowOnSave) throw new IOException("Simulated save error");
            Saved = true;
        }
        public void Dispose() => Disposed = true;
    }
}
