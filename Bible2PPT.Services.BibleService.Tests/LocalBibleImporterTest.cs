using System.Text;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Sources;
using Xunit;

namespace Bible2PPT.Services.BibleService.Tests;

public sealed class LocalBibleImporterTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-import-tests", Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(_directory, "Bibles", "krv.json");

    public LocalBibleImporterTest() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("창1:2 두 번째 테스트\n창세기 1:1 첫 번째 테스트\n40\t1\t1\t신약 테스트")]
    [InlineData("# 주석\nGenesis 1:2 두 번째 테스트\n창1:1 첫 번째 테스트\nMatthew 1:1 신약 테스트")]
    public async Task ImportsTextAndTsvIntoTheSameReaderUsedByTheExe(string text)
    {
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, text, new UTF8Encoding(true));
        await LocalBibleImporter.ImportAsync(input, Output);
        var source = new LocalBible(Output);
        var bible = Assert.Single(await source.GetBiblesOnlineAsync());
        var books = await source.GetBooksOnlineAsync(bible);
        Assert.Equal(2, books.Count);
        Assert.Equal(BookKey.Matthew, books[1].Key);
        var chapter = Assert.Single(await source.GetChaptersOnlineAsync(books[0]));
        chapter.Book = books[0];
        var verses = await source.GetVersesOnlineAsync(chapter);
        Assert.Equal(new[] { 1, 2 }, verses.Select(verse => verse.Number));
        Assert.Equal("첫 번째 테스트", verses[0].Text);
    }

    [Fact]
    public async Task ImportsFlatJsonWithContentField()
    {
        var input = Path.Combine(_directory, "input.json");
        await File.WriteAllTextAsync(input, "[{\"book\":40,\"chapter\":1,\"verse\":1,\"content\":\"테스트 본문\"}]");
        await LocalBibleImporter.ImportAsync(input, Output);
        var source = new LocalBible(Output);
        var book = Assert.Single(await source.GetBooksOnlineAsync(Assert.Single(await source.GetBiblesOnlineAsync())));
        Assert.Equal(BookKey.Matthew, book.Key);
    }

    [Fact]
    public async Task ImportsCp949Text()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, "창1:1 테스트 본문", Encoding.GetEncoding(949));
        await LocalBibleImporter.ImportAsync(input, Output);
        Assert.Single(await new LocalBible(Output).GetBiblesOnlineAsync());
    }

    [Fact]
    public async Task PreservesWorkingFileOnBadImportAndCanReloadAfterSuccess()
    {
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, "창1:1 기존 본문");
        await LocalBibleImporter.ImportAsync(input, Output);
        var source = new LocalBible(Output);
        await source.GetBiblesOnlineAsync();
        var before = await File.ReadAllTextAsync(Output);
        await File.WriteAllTextAsync(input, "형식 오류");
        await Assert.ThrowsAsync<InvalidDataException>(() => LocalBibleImporter.ImportAsync(input, Output));
        Assert.Equal(before, await File.ReadAllTextAsync(Output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Output)!, ".bible-import-*"));

        await File.WriteAllTextAsync(input, "창1:1 새 본문");
        await LocalBibleImporter.ImportAsync(input, Output);
        source.Reload();
        var book = Assert.Single(await source.GetBooksOnlineAsync(Assert.Single(await source.GetBiblesOnlineAsync())));
        var chapter = Assert.Single(await source.GetChaptersOnlineAsync(book));
        chapter.Book = book;
        Assert.Equal("새 본문", Assert.Single(await source.GetVersesOnlineAsync(chapter)).Text);
    }

    [Theory]
    [InlineData("창1:1 본문\n창1:1 중복")]
    [InlineData("창0:1 본문")]
    [InlineData("67\t1\t1\t본문")]
    [InlineData("")]
    public async Task RejectsInvalidOrDuplicateReferences(string text)
    {
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, text);
        await Assert.ThrowsAsync<InvalidDataException>(() => LocalBibleImporter.ImportAsync(input, Output));
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public async Task MissingSourceCanBeLoadedAfterImportWithoutRestart()
    {
        var source = new LocalBible(Output);
        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetBiblesOnlineAsync());
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, "창1:1 테스트 본문");
        await LocalBibleImporter.ImportAsync(input, Output);
        source.Reload();
        Assert.Single(await source.GetBiblesOnlineAsync());
    }

    [Fact]
    public async Task ImportsNativeJsonAndKeepsItsTranslationName()
    {
        var input = Path.Combine(_directory, "input.txt");
        await File.WriteAllTextAsync(input, "창1:1 테스트 본문");
        await LocalBibleImporter.ImportAsync(input, Output);
        var native = Path.Combine(_directory, "native.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(Output))!;
        document["name"] = "테스트 역본";
        await File.WriteAllTextAsync(native, document.ToJsonString());
        await LocalBibleImporter.ImportAsync(native, Output);
        Assert.Equal("테스트 역본", Assert.Single(await new LocalBible(Output).GetBiblesOnlineAsync()).Name);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
