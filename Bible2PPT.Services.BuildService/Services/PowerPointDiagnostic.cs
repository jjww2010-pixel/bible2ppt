using Bible2PPT.Bibles;
using Bible2PPT.PPT;
using Bible2PPT.Services.TemplateService;

namespace Bible2PPT.Services;

public static class PowerPointDiagnostic
{
    public static void WriteSamplePpt(string templatePath, string output, Book book, Chapter chapter, IReadOnlyList<Verse> verses)
    {
        var job = new Job
        {
            Template = new Template { FileName = templatePath, NumberOfVerseLinesPerSlide = 6 },
            OutputDestination = output,
        };
        using var powerpoint = new HiddenPowerPoint();
        using var ppt = new PPTManager(powerpoint.Instance, job, output);
        foreach (var verse in verses)
        {
            verse.Chapter = chapter;
            ppt.AppendVerse(new[] { verse }, verse, book, chapter, verses[0].Number, verses.Last().Number, CancellationToken.None);
        }
        ppt.Save();
    }
}
