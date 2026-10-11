import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import os

SCRIPT = Path(__file__).with_name("import_bible.py")
spec = importlib.util.spec_from_file_location("import_bible", SCRIPT)
import_bible = importlib.util.module_from_spec(spec)
spec.loader.exec_module(import_bible)


class ImportBibleTest(unittest.TestCase):
    def test_flat_json_sorting_and_new_testament_mapping(self):
        document = import_bible.convert([
            (40, 1, 1, "신약 테스트"),
            ("창세기", 1, 2, "두 번째 테스트"),
            ("Genesis", 1, 1, "첫 번째 테스트"),
        ], "개역개정")
        self.assertEqual(["Genesis", "Matthew"], [book["key"] for book in document["books"]])
        self.assertEqual([1, 2], [verse["number"] for verse in document["books"][0]["chapters"][0]["verses"]])
        self.assertEqual("첫 번째 테스트", document["books"][0]["chapters"][0]["verses"][0]["text"])

    def test_all_66_book_keys_are_distinct_and_ordered(self):
        document = import_bible.convert([(i, 1, 1, "테스트") for i in range(1, 67)], "테스트 역본")
        self.assertEqual(66, len({book["key"] for book in document["books"]}))
        self.assertEqual("Revelation", document["books"][-1]["key"])

    def test_rejects_duplicate_references(self):
        with self.assertRaisesRegex(ValueError, "중복"):
            import_bible.convert([("창", 1, 1, "첫 본문"), (1, 1, 1, "둘째 본문")], "개역개정")

    def test_rejects_bad_numbers_books_and_empty_text(self):
        for row in [(0, 1, 1, "본문"), (67, 1, 1, "본문"), ("없는책", 1, 1, "본문"),
                    (1, 0, 1, "본문"), (1, 1, -1, "본문"), (1, True, 1, "본문"), (1, 1, 1, " ")]:
            with self.subTest(row=row), self.assertRaises(ValueError):
                import_bible.convert([row], "개역개정")

    def test_empty_input_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "구절이 없습니다"):
            import_bible.convert([], "개역개정")

    def run_cli(self, source, output, *options):
        return subprocess.run([sys.executable, str(SCRIPT), str(source), str(output), "--name", "개역개정", *options],
                              capture_output=True, text=True, encoding="utf-8")

    def test_bom_txt_and_tsv_are_converted_by_cli(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.txt"
            output = Path(directory) / "Bibles" / "krv.json"
            source.write_text("# 테스트\n창1:2 두 번째 테스트\n창세기 1:1 첫 번째 테스트\n40\t1\t1\t신약 테스트\n", encoding="utf-8-sig")
            result = self.run_cli(source, output)
            self.assertEqual(0, result.returncode, result.stderr)
            document = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("Matthew", document["books"][1]["key"])
            self.assertEqual([1, 2], [v["number"] for v in document["books"][0]["chapters"][0]["verses"]])

    def test_content_field_and_source_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.json", Path(directory) / "krv.json"
            source.write_text(json.dumps([{"book": "마", "chapter": 1, "verse": 1, "content": "테스트"}]), encoding="utf-8")
            result = self.run_cli(source, output, "--source", "사용자가 제공한 테스트")
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual("사용자가 제공한 테스트", json.loads(output.read_text(encoding="utf-8"))["source"])

    def test_wrapped_and_combined_verses_are_preserved_once(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
            source.write_text("창35:22 첫 문장\n창세기35:이어진 문장\n창35:23-25 통합 문장\n창35:추가 문장\n창35:26 마지막 문장", encoding="utf-8-sig")
            result = self.run_cli(source, output)
            self.assertEqual(0, result.returncode, result.stderr)
            verses = json.loads(output.read_text(encoding="utf-8"))["books"][0]["chapters"][0]["verses"]
            self.assertEqual([22, 23, 26], [verse["number"] for verse in verses])
            self.assertEqual("첫 문장 이어진 문장", verses[0]["text"])
            self.assertEqual("통합 문장 추가 문장", verses[1]["text"])
            self.assertEqual(25, verses[1]["endNumber"])

    def test_orphan_continuations_and_invalid_ranges_are_rejected(self):
        for text in ["창35:이어진 문장", "창35:22 본문\n출35:이어진 문장",
                     "창35:22 본문\n창36:이어진 문장", "창35:22-21 역방향",
                     "창35:22-24 본문\n창35:23 겹친 본문", "창35:22 본문\n창35:23- 잘못된 범위"]:
            with self.subTest(text=text), tempfile.TemporaryDirectory() as directory:
                source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
                source.write_text(text, encoding="utf-8")
                output.write_text("기존 파일", encoding="utf-8")
                self.assertNotEqual(0, self.run_cli(source, output, "--overwrite").returncode)
                self.assertEqual("기존 파일", output.read_text(encoding="utf-8"))

    def test_existing_output_requires_explicit_overwrite(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
            source.write_text("창1:1 테스트", encoding="utf-8")
            output.write_text("기존 파일", encoding="utf-8")
            self.assertNotEqual(0, self.run_cli(source, output).returncode)
            self.assertEqual("기존 파일", output.read_text(encoding="utf-8"))
            self.assertEqual(0, self.run_cli(source, output, "--overwrite").returncode)

    def test_invalid_input_does_not_replace_existing_output(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
            source.write_text("본문 형식 오류", encoding="utf-8")
            output.write_text("기존 파일", encoding="utf-8")
            result = self.run_cli(source, output, "--overwrite")
            self.assertNotEqual(0, result.returncode)
            self.assertIn("1번째 줄", result.stderr)
            self.assertEqual("기존 파일", output.read_text(encoding="utf-8"))

    def test_input_cannot_be_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.txt"
            source.write_text("창1:1 테스트", encoding="utf-8")
            self.assertNotEqual(0, self.run_cli(source, source, "--overwrite").returncode)
            self.assertEqual("창1:1 테스트", source.read_text(encoding="utf-8"))

    def test_cp949_is_supported_when_explicitly_selected(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
            source.write_text("창1:1 테스트 본문", encoding="cp949")
            result = self.run_cli(source, output, "--encoding", "cp949")
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn("테스트 본문", output.read_text(encoding="utf-8"))

    def test_legacy_redirected_console_does_not_report_failure_after_success(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output = Path(directory) / "input.txt", Path(directory) / "krv.json"
            source.write_text("창1:1 진단용 문장", encoding="utf-8")
            env = os.environ.copy()
            env["PYTHONIOENCODING"] = "ascii"
            result = subprocess.run([sys.executable, str(SCRIPT), str(source), str(output), "--name", "진단용"],
                                    env=env, capture_output=True, text=True, encoding="utf-8")
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual("진단용 문장", json.loads(output.read_text(encoding="utf-8"))["books"][0]["chapters"][0]["verses"][0]["text"])


if __name__ == "__main__":
    unittest.main()
