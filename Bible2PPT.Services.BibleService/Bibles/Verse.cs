using System.ComponentModel.DataAnnotations.Schema;

namespace Bible2PPT.Bibles;

public record Verse : BibleBase
{
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    public int Number { get; set; }
    // Local TXT exports may contain one combined passage such as 18-19.
    // Local content is not cached, so no database migration is needed.
    [NotMapped]
    public int? EndNumber { get; set; }

    [NotMapped]
    public string NumberLabel => EndNumber is { } end && end != Number ? $"{Number}-{end}" : $"{Number}";

    public bool Overlaps(int startNumber, int? endNumber) =>
        (EndNumber ?? Number) >= startNumber && (endNumber is null || Number <= endNumber);
    public string Text { get; set; } = null!;
}
