using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bible2PPT.Services;
using Bible2PPT.Sources;

namespace Bible2PPT
{
    // Run on a Windows machine with PowerPoint to verify the original rendering path.
    internal static class PublishedPptCheck
    {
        public static int Run(string[] args)
        {
            if (args.Length != 4) return 2;
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Bible2PPT-ppt-check", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                var source = new LocalBible(args[1]);
                var bible = source.GetBiblesOnlineAsync().GetAwaiter().GetResult()[0];
                var book = source.GetBooksOnlineAsync(bible).GetAwaiter().GetResult()[0];
                book.Bible = bible;
                book.NameInfo = book.Name;
                book.AbbreviationInfo = book.Abbreviation;
                var chapter = source.GetChaptersOnlineAsync(book).GetAwaiter().GetResult()[0];
                chapter.Book = book;
                var verses = source.GetVersesOnlineAsync(chapter).GetAwaiter().GetResult().Take(2).ToList();
                var templatePath = Path.Combine(temporaryDirectory, "template.pptx");
                using (var resource = typeof(PublishedPptCheck).Assembly.GetManifestResourceStream("Bible2PPT.Resources.Template.pptx"))
                using (var destination = File.Create(templatePath))
                {
                    if (resource == null) throw new InvalidDataException("기본 PPT 템플릿이 없습니다.");
                    resource.CopyTo(destination);
                }
                var output = Path.GetFullPath(args[2]);
                PowerPointDiagnostic.WriteSamplePpt(templatePath, output, book, chapter, verses);

                using var archive = ZipFile.OpenRead(output);
                var slides = archive.Entries.Where(entry => Regex.IsMatch(entry.FullName, @"^ppt/slides/slide[0-9]+\.xml$")).ToList();
                XNamespace drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
                var body = string.Concat(slides.SelectMany(entry =>
                {
                    using var stream = entry.Open();
                    return XDocument.Load(stream).Descendants(drawing + "t").Select(text => text.Value).ToList();
                }));
                if (slides.Count < verses.Count || verses.Any(verse => !body.Contains(verse.Text, StringComparison.Ordinal)))
                    throw new InvalidDataException("생성된 PPT의 슬라이드 또는 본문이 예상과 다릅니다.");
                File.WriteAllText(args[3], $"PowerPoint OK\nSlides={slides.Count}\nVerses={verses.Count}\nPPT={output}\n");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(args[3], $"PowerPoint 확인 실패\n{ex}");
                return 1;
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}
