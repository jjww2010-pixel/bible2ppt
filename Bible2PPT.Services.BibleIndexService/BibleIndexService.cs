using Microsoft.EntityFrameworkCore;

namespace Bible2PPT.Services.BibleIndexService;

public class BibleIndexService
{
    private readonly IDbContextFactory<BibleIndexContext> _dbFactory;
    private volatile Lazy<Dictionary<string, BookInfo>> _abbreviations;

    public BibleIndexService(IDbContextFactory<BibleIndexContext> dbFactory)
    {
        _dbFactory = dbFactory;
        _abbreviations = new Lazy<Dictionary<string, BookInfo>>(ReadAbbreviations);
    }

    public BookInfo GetBookInfo(BookKey key, string languageCode)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BookInfos
            .Where(x => (x.Key == key) && (x.LanguageCode == languageCode))
            .OrderByDescending(x => x.IsPrimary)
            .First();
    }

    public string GetBookName(BookKey key, string languageCode)
    {
        using var db = _dbFactory.CreateDbContext();

        return GetBookInfoCandidates(db, key, languageCode)
            .Where(x => x.Kind == BookInfoKind.Name)
            .OrderByDescending(x => x.IsPrimary)
            .First()
            .Value;
    }

    public string GetBookAbbreviation(BookKey key, string languageCode)
    {
        using var db = _dbFactory.CreateDbContext();

        return GetBookInfoCandidates(db, key, languageCode)
            .Where(x => x.Kind == BookInfoKind.Abbreviation)
            .OrderByDescending(x => x.IsPrimary)
            .First()
            .Value;
    }

    public void AddBookInfos(IEnumerable<BookInfo> bookInfos)
    {
        using var db = _dbFactory.CreateDbContext();
        db.BookInfos.AddRange(bookInfos);
        db.SaveChanges();
        _abbreviations = new Lazy<Dictionary<string, BookInfo>>(ReadAbbreviations);
    }

    public BookInfo? FindBookInfoExactAbbreviation(string abbr)
    {
        return _abbreviations.Value.TryGetValue(abbr, out var info) ? info with { } : null;
    }

    private Dictionary<string, BookInfo> ReadAbbreviations()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.BookInfos.AsNoTracking().Where(info => info.Kind == BookInfoKind.Abbreviation).ToList()
            .GroupBy(info => info.Value, StringComparer.Ordinal)
            .Where(group => group.Select(info => info.Key).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private static IQueryable<BookInfo> GetBookInfoCandidates(BibleIndexContext db, BookKey key, string languageCode)
    {
        var bookInfo = db.BookInfos.Where(x => x.Key == key);

        var languageMatches = bookInfo.Where(x => x.LanguageCode == languageCode);
        if (!languageMatches.Any())
        {
            return bookInfo;
        }

        return languageMatches;
    }
}
