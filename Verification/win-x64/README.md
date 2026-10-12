# Windows verification result

Tested commit: be4c37fafcc456b54b2856ef8f640ea3ff64c90a

Runtime: win-x64

Operating system: Windows

Regression tests: success

EXE publish: success

Published EXE checks: success

Run: https://github.com/jjww2010-pixel/bible2ppt/actions/runs/38277535169

These checks run on a GitHub-hosted Windows machine. Full-format fixtures contain generated test sentences, not scripture text.
Published EXE checks cover 66 books, 1189 chapters, 31101 individual verse queries, imports, SQLite, Unicode/space paths, and the real MainForm's navigation, book selection, Bible selection, three separate translation files and selectors, and settings/configuration handling.

PowerPoint COM rendering: **NOT TESTED**. Microsoft Office is not installed on GitHub-hosted runners.
GUI diagnostics disable the rendering dependency; they do not bypass the MainForm or its event handlers.
Read the TRX files and individual EXE reports for the detailed results. A skipped/failed check is not a pass.
