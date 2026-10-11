@echo off
cd /d "%~dp0"
if not exist "Bibles\krv.json" (
  echo Import your Bible file in Bible2PPT.exe first.
  pause
  exit /b 1
)
echo Checking all verse queries. This can take a few minutes.
Bible2PPT.exe --check-bible "Bibles\krv.json" "Bible-check.txt"
if errorlevel 1 (
  echo Bible check FAILED.
) else (
  echo Bible check PASSED.
)
type "Bible-check.txt"
pause
