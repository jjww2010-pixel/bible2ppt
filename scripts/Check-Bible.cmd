@echo off
cd /d "%~dp0"
if not exist "Bibles\*.json" (
  echo Import your Bible file in Bible2PPT.exe first.
  pause
  exit /b 1
)
echo Checking all imported translations. This can take a few minutes.
for %%F in ("Bibles\*.json") do (
  Bible2PPT.exe --check-bible "%%F" "Bible-check-%%~nF.txt"
  if errorlevel 1 (
    echo %%~nxF check FAILED.
  ) else (
    echo %%~nxF check PASSED.
  )
  type "Bible-check-%%~nF.txt"
)
pause
