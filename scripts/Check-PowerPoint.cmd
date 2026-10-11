@echo off
cd /d "%~dp0"
Bible2PPT.exe --check-ppt "Diagnostics\example.json" "PowerPoint-check.pptx" "PowerPoint-check.txt"
if errorlevel 1 (
    echo PowerPoint check FAILED. See PowerPoint-check.txt.
) else (
    echo PowerPoint check PASSED. Open PowerPoint-check.pptx.
)
type "PowerPoint-check.txt"
pause
