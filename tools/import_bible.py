#!/usr/bin/env python3
"""Convert a supplied verse JSON/TXT file into Bible2PPT's local JSON format."""

import argparse
import json
import os
from pathlib import Path
import re
import tempfile

# Flat-file numeric IDs use the Protestant 1-66 order, not internal enum ordinals.
BOOKS = [
    ('Genesis', '창세기', '창'),
    ('Exodus', '출애굽기', '출'),
    ('Leviticus', '레위기', '레'),
    ('Numbers', '민수기', '민'),
    ('Deuteronomy', '신명기', '신'),
    ('Joshua', '여호수아', '수'),
    ('Judges', '사사기', '삿'),
    ('Ruth', '룻기', '룻'),
    ('ISamuel', '사무엘상', '삼상'),
    ('IISamuel', '사무엘하', '삼하'),
    ('IKings', '열왕기상', '왕상'),
    ('IIKings', '열왕기하', '왕하'),
    ('IChronicles', '역대상', '대상'),
    ('IIChronicles', '역대하', '대하'),
    ('Ezra', '에스라', '스'),
    ('Nehemiah', '느헤미야', '느'),
    ('Esther', '에스더', '에'),
    ('Job', '욥기', '욥'),
    ('Psalms', '시편', '시'),
    ('Proverbs', '잠언', '잠'),
    ('Ecclesiastes', '전도서', '전'),
    ('SongOfSolomon', '아가', '아'),
    ('Isaiah', '이사야', '사'),
    ('Jeremiah', '예레미야', '렘'),
    ('Lamentations', '예레미야애가', '애'),
    ('Ezekiel', '에스겔', '겔'),
    ('Daniel', '다니엘', '단'),
    ('Hosea', '호세아', '호'),
    ('Joel', '요엘', '욜'),
    ('Amos', '아모스', '암'),
    ('Obadiah', '오바댜', '옵'),
    ('Jonah', '요나', '욘'),
    ('Micah', '미가', '미'),
    ('Nahum', '나홈', '나'),
    ('Habakkuk', '하박국', '합'),
    ('Zephaniah', '스바냐', '습'),
    ('Haggai', '학개', '학'),
    ('Zechariah', '스가랴', '슥'),
    ('Malachi', '말라기', '말'),
    ('Matthew', '마태복음', '마'),
    ('Mark', '마가복음', '막'),
    ('Luke', '누가복음', '눅'),
    ('John', '요한복음', '요'),
    ('Acts', '사도행전', '행'),
    ('Romans', '로마서', '롬'),
    ('ICorinthians', '고린도전서', '고전'),
    ('IICorinthians', '고린도후서', '고후'),
    ('Galatians', '갈라디아서', '갈'),
    ('Ephesians', '에베소서', '엡'),
    ('Philippians', '빌립보서', '빌'),
    ('Colossians', '골로새서', '골'),
    ('IThessalonians', '데살로니가전서', '살전'),
    ('IIThessalonians', '데살로니가후서', '살후'),
    ('ITimothy', '디모데전서', '딤전'),
    ('IITimothy', '디모데후서', '딤후'),
    ('Titus', '디도서', '딛'),
    ('Philemon', '빌레몬서', '몬'),
    ('Hebrews', '히브리서', '히'),
    ('James', '야고보서', '약'),
    ('IPeter', '베드로전서', '벧전'),
    ('IIPeter', '베드로후서', '벧후'),
    ('IJohn', '요한일서', '요일'),
    ('IIJohn', '요한이서', '요이'),
    ('IIIJohn', '요한삼서', '요삼'),
    ('Jude', '유다서', '유'),
    ('Revelation', '요한계시록', '계'),
]


def book_index(value):
    normalized = re.sub(r"\s+", "", str(value)).casefold()
    if normalized.isdecimal():
        index = int(normalized) - 1
        if 0 <= index < len(BOOKS):
            return index
    else:
        for index, aliases in enumerate(BOOKS):
            if normalized in [alias.casefold() for alias in aliases]:
                return index
        alternate = {"나훔": "Nahum", "나훔서": "Nahum", "아가서": "SongOfSolomon", "계시록": "Revelation"}
        if normalized in alternate:
            return book_index(alternate[normalized])
    raise ValueError(f"알 수 없는 책 이름 또는 번호: {value!r} (숫자는 1~66)")


def positive_number(value, label):
    if type(value) is int and value > 0:
        return value
    if isinstance(value, str) and re.fullmatch(r"[0-9]+", value) and int(value) > 0:
        return int(value)
    raise ValueError(f"{label} 번호는 1 이상의 정수여야 합니다: {value!r}")


def read_records(path, encoding):
    with path.open(encoding=encoding) as stream:
        if path.suffix.lower() == ".json":
            records = json.load(stream)
            if not isinstance(records, list):
                raise ValueError("입력 JSON은 book, chapter, verse, text/content를 가진 객체들의 배열이어야 합니다.")
            for number, record in enumerate(records, 1):
                if not isinstance(record, dict):
                    raise ValueError(f"JSON 항목 {number}: 객체가 필요합니다.")
                try:
                    yield (record["book"], record["chapter"], record["verse"], record.get("text", record.get("content")))
                except KeyError as ex:
                    raise ValueError(f"JSON 항목 {number}: {ex.args[0]} 필드가 없습니다.") from ex
        else:
            for number, line in enumerate(stream, 1):
                line = line.rstrip("\r\n")
                if not line.strip() or line.lstrip().startswith("#"):
                    continue
                fields = line.split("\t", 3)
                if len(fields) == 4:
                    yield tuple(fields)
                    continue
                match = re.fullmatch(r"\s*(.+?)\s*([0-9]+):([0-9]+)\s+(.+)", line)
                if not match:
                    raise ValueError(f"TXT {number}번째 줄: '창1:1 본문' 또는 책/장/절/본문 TSV 형식이 필요합니다.")
                yield match.groups()


def convert(records, name, source=None):
    if not isinstance(name, str) or not name.strip():
        raise ValueError("성경 이름이 필요합니다.")
    books = {}
    for row, (book, chapter, verse, text) in enumerate(records, 1):
        try:
            index = book_index(book)
            chapter = positive_number(chapter, "장")
            verse = positive_number(verse, "절")
            if not isinstance(text, str) or not text.strip():
                raise ValueError("본문이 비어 있거나 문자열이 아닙니다.")
            verses = books.setdefault(index, {}).setdefault(chapter, {})
            if verse in verses:
                raise ValueError(f"{BOOKS[index][1]} {chapter}:{verse} 구절이 중복되어 있습니다.")
            verses[verse] = text
        except ValueError as ex:
            raise ValueError(f"본문 항목 {row}: {ex}") from ex
    if not books:
        raise ValueError("입력 파일에 구절이 없습니다.")
    document = {"name": name.strip(), "languageCode": "ko", "books": []}
    if source:
        document["source"] = source
    for index, chapters in sorted(books.items()):
        key, title, abbreviation = BOOKS[index]
        document["books"].append({
            "key": key, "name": title, "abbreviation": abbreviation,
            "chapters": [
                {"number": chapter, "verses": [
                    {"number": verse, "text": text} for verse, text in sorted(verses.items())
                ]} for chapter, verses in sorted(chapters.items())
            ],
        })
    return document


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="사용 가능한 본문 JSON 또는 TXT/TSV 파일")
    parser.add_argument("output", type=Path, help="출력 krv.json 경로")
    parser.add_argument("--name", required=True, help="실제 입력 본문의 번역 이름 (예: 개역개정)")
    parser.add_argument("--encoding", default="utf-8-sig", help="입력 인코딩 (기본 UTF-8, 필요시 cp949)")
    parser.add_argument("--source", help="본문 출처를 기록할 선택적 설명 또는 URL")
    parser.add_argument("--overwrite", action="store_true", help="기존 출력 파일 교체 허용")
    args = parser.parse_args(argv)
    temporary = None
    try:
        if args.input.resolve() == args.output.resolve():
            raise ValueError("입력과 출력 파일은 서로 다른 경로여야 합니다.")
        if args.output.exists() and not args.overwrite:
            raise ValueError("출력 파일이 이미 있습니다. 교체하려면 --overwrite를 사용하세요.")
        document = convert(read_records(args.input, args.encoding), args.name, args.source)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=args.output.parent, delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(document, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        # Failed parsing or validation never damages an existing output file.
        if args.overwrite:
            os.replace(temporary, args.output)
        else:
            # Exclusive creation also protects files created during conversion.
            with args.output.open("x", encoding="utf-8") as stream:
                stream.write(temporary.read_text(encoding="utf-8"))
        chapters = sum(len(book["chapters"]) for book in document["books"])
        verses = sum(len(chapter["verses"]) for book in document["books"] for chapter in book["chapters"])
        print(f"변환 완료: {len(document['books'])}권, {chapters}장, {verses}절 → {args.output}")
        if len(document["books"]) != 66:
            print("참고: 66권 전체가 들어 있는 파일은 아닙니다.")
        return 0
    except (OSError, UnicodeError, LookupError, ValueError) as ex:
        parser.exit(1, f"변환 실패: {ex}\n")
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())
