using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible2PPT.Services.BibleIndexService.Tests;

public sealed class BibleIndexServiceTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Bible2PPT-index-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    public BibleIndexServiceTest()
    {
        Directory.CreateDirectory(_directory);
        var registrations = new ServiceCollection();
        registrations.AddBibleIndexService(options => options.UseSqlite($"Data Source={_directory}/index.db;Pooling=False"));
        _services = registrations.BuildServiceProvider();
        _services.UseBibleIndexService();
    }

    [Fact]
    public void RepeatedLookupsDoNotReopenTheDatabaseAndDoNotShareMutableObjects()
    {
        var service = _services.GetRequiredService<BibleIndexService>();
        var first = service.FindBookInfoExactAbbreviation("요일")!;
        first.Key.ShouldBe(BookKey.IJohn);
        first.Value = "changed by caller";
        File.Move(Path.Combine(_directory, "index.db"), Path.Combine(_directory, "removed.db"));
        var again = service.FindBookInfoExactAbbreviation("요일")!;
        again.Key.ShouldBe(BookKey.IJohn);
        again.Value.ShouldBe("요일");
        service.FindBookInfoExactAbbreviation("unknown").ShouldBeNull();
    }

    [Fact]
    public void AddedAbbreviationsAreSavedAndAmbiguousOnesAreRejectedAfterCacheRefresh()
    {
        var service = _services.GetRequiredService<BibleIndexService>();
        service.FindBookInfoExactAbbreviation("초록").ShouldBeNull();
        service.AddBookInfos(new[] { Alias(BookKey.Genesis) });
        service.FindBookInfoExactAbbreviation("초록")!.Key.ShouldBe(BookKey.Genesis);
        using (var database = _services.GetRequiredService<IDbContextFactory<BibleIndexContext>>().CreateDbContext())
            database.BookInfos.Count(info => info.Value == "초록").ShouldBe(1);
        service.AddBookInfos(new[] { Alias(BookKey.Exodus) });
        service.FindBookInfoExactAbbreviation("초록").ShouldBeNull();
    }

    private static BookInfo Alias(BookKey key) => new()
    {
        Key = key, Kind = BookInfoKind.Abbreviation, LanguageCode = "ko", Version = "diagnostic", Value = "초록",
    };

    public void Dispose() { _services.Dispose(); Directory.Delete(_directory, recursive: true); }
}
