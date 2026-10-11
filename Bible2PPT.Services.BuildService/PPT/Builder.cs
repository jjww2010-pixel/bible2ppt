using System.Threading.Channels;
using Bible2PPT.Bibles;
using Bible2PPT.Services;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;
using Bible2PPT.Services.BuildService;
using Microsoft.EntityFrameworkCore;

namespace Bible2PPT.PPT;

public class Builder : JobManager
{
    private readonly HiddenPowerPoint _powerPoint;
    private readonly ZippedBibleService _zippedBibleService;
    private readonly VerseQueryParser _verseQueryParser;
    private readonly IDbContextFactory<BuildContext> _dbFactory;

    public Builder(HiddenPowerPoint ppt, ZippedBibleService bibleService, VerseQueryParser verseQueryParser, IDbContextFactory<BuildContext> dbFactory)
    {
        _powerPoint = ppt;
        _zippedBibleService = bibleService;
        _verseQueryParser = verseQueryParser;
        _dbFactory = dbFactory;
    }

    protected virtual IPresentationWriter CreatePresentation(Job job, string output) =>
        new PPTManager(_powerPoint.Instance, job, output);

    protected override async Task ProcessAsync(Job job, CancellationToken token)
    {
        await base.ProcessAsync(job, token).ConfigureAwait(false);
        var queries = _verseQueryParser.ParseVerseQueries(job.QueryString).ToArray();
        if (queries.Length == 0) throw new InvalidDataException("PPT로 만들 성경 구절을 입력하세요.");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var workerToken = stop.Token;
        var channel = Channel.CreateBounded<(IAsyncEnumerable<IEnumerable<Verse?>> verses, Book book, Chapter chapter, int start, int? end)>(1);
        var queriesDone = 0;
        var chaptersDone = 0;
        var chapterCount = 0;

        async Task ProduceAsync()
        {
            try
            {
                foreach (var query in queries)
                {
                    var books = await _zippedBibleService.FindBookAsync(job.Bibles, query.BookKey, workerToken).ConfigureAwait(false);
                    var book = books.FirstOrDefault(book => book is not null)
                        ?? throw new InvalidDataException($"{query.BookKey} 본문이 선택한 성경에 없습니다.");
                    var foundChapter = false;
                    await foreach (var eachChapter in _zippedBibleService.GetChaptersAsync(books, workerToken).ConfigureAwait(false))
                    {
                        var chapter = eachChapter.FirstOrDefault(chapter => chapter is not null);
                        if (chapter is null || chapter.Number < query.StartChapterNumber ||
                            (query.EndChapterNumber is { } last && chapter.Number > last)) continue;
                        foundChapter = true;
                        var start = chapter.Number == query.StartChapterNumber ? query.StartVerseNumber : 1;
                        var end = chapter.Number == query.EndChapterNumber ? query.EndVerseNumber : null;
                        var rows = await _zippedBibleService.GetVersesAsync(eachChapter, workerToken)
                            .Where(row => row.Any(verse => verse is not null && verse.Overlaps(start, end)))
                            .ToListAsync(workerToken).ConfigureAwait(false);
                        if (rows.Count == 0)
                            throw new InvalidDataException($"{book.Name} {chapter.Number}:{start} 요청에 해당하는 본문이 없습니다.");
                        chapterCount++;
                        await channel.Writer.WriteAsync((rows.ToAsyncEnumerable(), book, chapter, start, end), workerToken).ConfigureAwait(false);
                    }
                    if (!foundChapter)
                        throw new InvalidDataException($"{book.Name} {query.StartChapterNumber} 요청에 해당하는 장이 없습니다.");
                    queriesDone++;
                }
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                throw;
            }
        }

        var producer = ProduceAsync();
        IPresentationWriter? combined = null;
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(workerToken).ConfigureAwait(false))
            {
                if (job.SplitChaptersIntoFiles)
                {
                    var output = Path.Combine(job.OutputDestination, item.book.Name, $"{item.chapter.Number:000}.pptx");
                    using var ppt = CreatePresentation(job, output);
                    await ppt.AppendChapter(item.verses, item.book, item.chapter, item.start, item.end, workerToken).ConfigureAwait(false);
                    workerToken.ThrowIfCancellationRequested();
                    ppt.Save();
                }
                else
                {
                    combined ??= CreatePresentation(job, job.OutputDestination);
                    await combined.AppendChapter(item.verses, item.book, item.chapter, item.start, item.end, workerToken).ConfigureAwait(false);
                }
                chaptersDone++;
                OnJobProgress(new JobProgressEventArgs(job, null, queriesDone, queries.Length, chaptersDone, chapterCount));
            }
            await producer.ConfigureAwait(false);
            workerToken.ThrowIfCancellationRequested();
            combined?.Save();
        }
        finally
        {
            // A rendering/file error must stop a producer blocked on the bounded
            // channel. Observe its completion without replacing the original error.
            stop.Cancel();
            try { await producer.ConfigureAwait(false); } catch { }
            combined?.Dispose();
        }
    }

    public new void Queue(Job job)
    {
        using (var db = _dbFactory.CreateDbContext())
        {
            db.Jobs.Add(job);
            db.SaveChanges();
        }

        base.Queue(job);
    }

    public async ValueTask RemoveJobAsync(Job job)
    {
        Cancel(job);

        using var db = _dbFactory.CreateDbContext();
        if (await db.Jobs.ContainsAsync(job).ConfigureAwait(false))
        {
            db.Jobs.Remove(job);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public IEnumerable<Job> GetJobs()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Jobs.ToList();
    }
}
