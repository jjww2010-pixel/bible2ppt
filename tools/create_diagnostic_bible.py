#!/usr/bin/env python3
"""Create format-only Bible fixtures with generated sentences, without scripture text."""
import argparse
import json
from pathlib import Path


def create_text(shape):
    merged = {(book, chapter, start): end for book, chapter, start, end in shape["combined"]}
    missing = {tuple(item) for item in shape["missing"]}
    wraps = {tuple(item) for item in shape["continuations"]}
    lines = []
    for book, chapters in shape["chapters"].items():
        abbreviation = shape["abbreviations"][book]
        for chapter, count in enumerate(chapters, 1):
            verse = 1
            while verse <= count:
                if (book, chapter, verse) in missing:
                    verse += 1
                    continue
                end = merged.get((book, chapter, verse), verse)
                label = f"{verse}" if end == verse else f"{verse}-{end}"
                lines.append(f"{abbreviation}{chapter}:{label} 진단용 {book} {chapter}:{label}")
                if (book, chapter, verse) in wraps:
                    lines.append(f"{abbreviation}{chapter}:이어진 진단용 문장")
                verse = end + 1
            lines.append("")
    return "\n".join(lines)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    shape = json.loads((Path(__file__).resolve().parents[1] / "scripts/fixtures/bible-format.json").read_text(encoding="utf-8"))
    args.output.write_text(create_text(shape), encoding="utf-8-sig")
