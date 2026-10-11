#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Bible2PPT.Bibles;
using Bible2PPT.Sources;
using Bible2PPT.Services.BibleIndexService;
using Bible2PPT.Services.BibleService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible2PPT
{
    // Exercises the published EXE and its runtime without requiring Office on a CI machine.
    internal static class PublishedAppCheck
    {
        public static int Run(string[] args)
        {
            if (args.Length != 3) return 2;
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Bible2PPT-check", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                var registrations = new ServiceCollection();
                registrations.AddBibleIndexService(options => options.UseSqlite($"Data Source={Path.Combine(temporaryDirectory, "index.db")};Pooling=False"));
                registrations.AddBibleService(options => options.UseSqlite($"Data Source={Path.Combine(temporaryDirectory, "bible.db")};Pooling=False"));
                using var services = registrations.BuildServiceProvider();
                services.UseBibleIndexService();
                services.UseBibleService();
                var bibleService = services.GetRequiredService<BibleService>();
                var input = args[1];
                if (!Path.GetExtension(input).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var converted = Path.Combine(temporaryDirectory, "imported.json");
                    LocalBibleImporter.ImportAsync(input, converted).GetAwaiter().GetResult();
                    input = converted;
                }
                var source = new LocalBible(input);
                var bibles = bibleService.GetBiblesAsync(source).GetAwaiter().GetResult();
                var books = bibleService.GetBooksAsync(bibles[0]).GetAwaiter().GetResult();
                var verseCount = 0;
                var chapterCount = 0;
                var coveredCount = 0;
                var queryCount = 0;
                var zipped = services.GetRequiredService<ZippedBibleService>();
                var parser = services.GetRequiredService<VerseQueryParser>();
                foreach (var book in books)
                {
                    foreach (var chapter in bibleService.GetChaptersAsync(book).GetAwaiter().GetResult())
                    {
                        chapterCount++;
                        var expected = bibleService.GetVersesAsync(chapter).GetAwaiter().GetResult();
                        var actual = ReadZipped(zipped, chapter).GetAwaiter().GetResult();
                        if (!actual.Select(Identity).SequenceEqual(expected.Select(Identity)))
                            throw new InvalidDataException($"Query pipeline changed passages in {book.Key} {chapter.Number}.");
                        verseCount += actual.Count;
                        foreach (var verse in actual)
                        {
                            var last = verse.EndNumber ?? verse.Number;
                            coveredCount += last - verse.Number + 1;
                            for (var number = verse.Number; ; number++)
                            {
                                var query = parser.ParseVerseQueries($"{book.AbbreviationInfo}{chapter.Number}:{number}").Single();
                                if (query.BookKey != book.Key || query.StartChapterNumber != chapter.Number || query.EndChapterNumber != chapter.Number)
                                    throw new InvalidDataException("Verse query resolved to the wrong book/chapter.");
                                var selected = actual.Where(item => item.Overlaps(query.StartVerseNumber, query.EndVerseNumber)).ToList();
                                if (selected.Count != 1 || Identity(selected[0]) != Identity(verse))
                                    throw new InvalidDataException($"Verse selection failed: {book.Key} {chapter.Number}:{number}.");
                                queryCount++;
                                if (number == last) break;
                            }
                        }
                    }
                }
                if (ReadZipped(zipped, null).GetAwaiter().GetResult().Count != 0)
                    throw new InvalidDataException("Empty chapter selection returned passages.");
                File.WriteAllText(args[2], $"Books={books.Count}\nChapters={chapterCount}\nVerses={verseCount}\nCoveredVerses={coveredCount}\nQueriesChecked={queryCount}\nEmptySources=OK\nPowerPoint=NOT_TESTED\n");
                return verseCount > 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                File.WriteAllText(args[2], ex.Message);
                return 1;
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
            }
        }

        private static (int Number, int? End, string Text) Identity(Verse verse) => (verse.Number, verse.EndNumber, verse.Text);

        private static async System.Threading.Tasks.Task<List<Verse>> ReadZipped(ZippedBibleService zipped, Chapter? chapter)
        {
            var result = new List<Verse>();
            await foreach (var row in zipped.GetVersesAsync(new[] { chapter }, System.Threading.CancellationToken.None))
                result.AddRange(row.OfType<Verse>());
            return result;
        }
    }
}
