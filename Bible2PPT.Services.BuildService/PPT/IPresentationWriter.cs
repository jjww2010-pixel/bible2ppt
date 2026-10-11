using Bible2PPT.Bibles;

namespace Bible2PPT.PPT;

public interface IPresentationWriter : IDisposable
{
    Task AppendChapter(IAsyncEnumerable<IEnumerable<Verse?>> verses, Book book, Chapter chapter,
        int startVerseNumber, int? endVerseNumber, CancellationToken token);
    void Save();
}
