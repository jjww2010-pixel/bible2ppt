# Windows verification result

Tested commit: 4366775bb2f1d8ace8b7b371eb7b915fdf39dba3

Runtime: win-x64

Operating system: Windows

Regression tests: success

EXE publish: success

Published EXE checks: failure

Run: https://github.com/jjww2010-pixel/bible2ppt/actions/runs/38154862402

These checks run on a GitHub-hosted Windows machine. Full-format fixtures contain generated test sentences, not scripture text.
Published EXE checks cover 66 books, 1189 chapters, 31101 individual verse queries, imports, SQLite, Unicode/space paths, and the real MainForm's navigation, book selection, Bible selection and settings/configuration handling.

PowerPoint COM rendering: **NOT TESTED**. Microsoft Office is not installed on GitHub-hosted runners.
GUI diagnostics disable the rendering dependency; they do not bypass the MainForm or its event handlers.
Read the TRX files and individual EXE reports for the detailed results. A skipped/failed check is not a pass.
