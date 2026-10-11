using System;
using System.IO;
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
                var source = new LocalBible(args[1]);
                var bibles = bibleService.GetBiblesAsync(source).GetAwaiter().GetResult();
                var books = bibleService.GetBooksAsync(bibles[0]).GetAwaiter().GetResult();
                var verseCount = 0;
                foreach (var book in books)
                {
                    foreach (var chapter in bibleService.GetChaptersAsync(book).GetAwaiter().GetResult())
                    {
                        verseCount += bibleService.GetVersesAsync(chapter).GetAwaiter().GetResult().Count;
                    }
                }
                File.WriteAllText(args[2], $"Books={books.Count}\nVerses={verseCount}\n");
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
    }
}
