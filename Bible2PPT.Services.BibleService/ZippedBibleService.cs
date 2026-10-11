using System.Runtime.CompilerServices;
using Bible2PPT.Bibles;
using Bible2PPT.Services.BibleIndexService;

namespace Bible2PPT.Services.BibleService;

public class ZippedBibleService
{
    private readonly BibleService _bibleService;

    public ZippedBibleService(BibleService bibleService)
    {
        _bibleService = bibleService;
    }

    public async Task<IEnumerable<Book?>> FindBookAsync(IEnumerable<Bible> bibles, BookKey key, CancellationToken cancellationToken)
    {
        var eachBooks = await GetEachBooksAsync(bibles, cancellationToken).ConfigureAwait(false);

        return eachBooks.Select(x => x.FirstOrDefault(book => book.Key == key)).ToList();
    }

    private async Task<IEnumerable<IEnumerable<Book>>> GetEachBooksAsync(IEnumerable<Bible> bibles, CancellationToken token)
    {
    RETRY:
        token.ThrowIfCancellationRequested();
        try
        {
            return await Task.WhenAll(bibles
                .Select(_bibleService.GetBooksAsync)
                .ToList()).ConfigureAwait(false);
        }
        // 취소할 때까지 계속 재시도
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            await Task.Delay(3000, token).ConfigureAwait(false);
            goto RETRY;
        }
    }

    private async Task<IEnumerable<IEnumerable<Chapter>>> GetEachChaptersAsync(IEnumerable<Book?> books, CancellationToken token)
    {
    RETRY:
        token.ThrowIfCancellationRequested();
        try
        {
            // 해당 책이 없는 성경도 있으므로 주의해서 장 정보 가져오기
            return await Task.WhenAll(books
                .Select(_bibleService.GetChaptersAsync)
                .ToList()).ConfigureAwait(false);
        }
        // 취소할 때까지 계속 재시도
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            await Task.Delay(3000, token).ConfigureAwait(false);
            goto RETRY;
        }
    }

    public async IAsyncEnumerable<IEnumerable<Chapter?>> GetChaptersAsync(IEnumerable<Book?> targetEachBook, [EnumeratorCancellation] CancellationToken token)
    {
        var eachTargetChapters = await GetEachChaptersAsync(targetEachBook, token).ConfigureAwait(false);

        foreach (var row in AlignByNumber(eachTargetChapters, chapter => chapter.Number))
        {
            token.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private async Task<IEnumerable<IEnumerable<Verse>>> GetEachVersesAsync(IEnumerable<Chapter?> chapters, CancellationToken token)
    {
    RETRY:
        token.ThrowIfCancellationRequested();
        try
        {
            return await Task.WhenAll(chapters
                .Select(_bibleService.GetVersesAsync)
                .ToList()).ConfigureAwait(false);
        }
        // 취소할 때까지 계속 재시도
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            await Task.Delay(3000, token).ConfigureAwait(false);
            goto RETRY;
        }
    }

    public async IAsyncEnumerable<IEnumerable<Verse?>> GetVersesAsync(IEnumerable<Chapter?> targetEachChapter, [EnumeratorCancellation] CancellationToken token)
    {
        var eachTargetVerses = await GetEachVersesAsync(targetEachChapter, token).ConfigureAwait(false);

        foreach (var row in AlignByNumber(eachTargetVerses, verse => verse.Number))
        {
            token.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private static IEnumerable<IEnumerable<T?>> AlignByNumber<T>(IEnumerable<IEnumerable<T>> sources, Func<T, int> number) where T : class
    {
        var maps = sources.Select(source => source.ToDictionary(number)).ToArray();
        // Only visit existing numbers. Empty sources and gaps never reuse an
        // exhausted enumerator or manufacture an all-null passage.
        foreach (var key in maps.SelectMany(map => map.Keys).Distinct().OrderBy(key => key))
            yield return maps.Select(map => map.TryGetValue(key, out var item) ? item : null).ToArray();
    }
}
