param(
    [Parameter(Mandatory = $true)][string]$PublishDirectory,
    [string]$ResultDirectory = 'verification-exe'
)

$ErrorActionPreference = 'Stop'
$env:PYTHONUTF8 = '1'
$originalExe = Join-Path (Resolve-Path $PublishDirectory) 'Bible2PPT.exe'
New-Item -ItemType Directory -Force $ResultDirectory | Out-Null
$results = (Resolve-Path $ResultDirectory).Path
$exe = $originalExe
if (!(Test-Path $exe)) { throw 'Bible2PPT.exe was not published.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('Bible2PPT smoke ' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null
$isolated = Join-Path $scratch '한글 경로'
New-Item -ItemType Directory -Path $isolated | Out-Null
$exe = Join-Path $isolated 'Bible2PPT.exe'
Copy-Item $originalExe $exe

function Invoke-BibleCheck([string]$InputFile, [string]$Report, [int]$ExpectedExit, [string]$Mode = '--check-bible') {
    Set-Content (Join-Path $results 'last-check.txt') "$Mode / $InputFile / expected=$ExpectedExit / started=$([DateTime]::UtcNow.ToString('o'))"
    $arguments = if ($Mode -eq '--check-ui') { @($Mode, ('"{0}"' -f $Report)) } else { @($Mode, ('"{0}"' -f $InputFile), ('"{0}"' -f $Report)) }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $scratch -PassThru
    $null = $process.Handle
    if (!$process.WaitForExit(300000)) {
        Stop-Process -Id $process.Id -Force
        throw 'Published EXE did not complete the local Bible check.'
    }
    if ($process.ExitCode -ne $ExpectedExit) {
        $details = if (Test-Path $Report) { Get-Content $Report -Raw } else { 'No report was produced.' }
        throw "EXE exit code $($process.ExitCode), expected $ExpectedExit. $details"
    }
    if (!(Test-Path $Report)) { throw 'Published EXE did not write its report.' }
}

try {
    $valid = Join-Path $scratch 'valid input.json'
    $report = Join-Path $scratch 'valid report.txt'
    Copy-Item 'Bible2PPT/Bibles/krv.example.json' $valid
    Invoke-BibleCheck $valid $report 0
    $result = Get-Content $report -Raw
    if ($result -notmatch 'Books=1' -or $result -notmatch 'Verses=2') {
        throw "Unexpected published EXE result: $result"
    }

    $invalid = Join-Path $scratch 'invalid input.json'
    Set-Content -Path $invalid -Value '{}' -Encoding utf8
    Invoke-BibleCheck $invalid (Join-Path $scratch 'invalid report.txt') 1
    $text = Join-Path $scratch 'wrapped and combined input.txt'
    Copy-Item 'scripts/fixtures/wrapped-bible.txt' $text
    $textReport = Join-Path $scratch 'text report.txt'
    Invoke-BibleCheck $text $textReport 0
    $textResult = Get-Content $textReport -Raw
    if ($textResult -notmatch 'Books=1' -or $textResult -notmatch 'Verses=3') {
        throw "Unexpected TXT import result from published EXE: $textResult"
    }
    $fullText = Join-Path $scratch 'whole Bible.txt'
    python tools/create_diagnostic_bible.py $fullText
    if ($LASTEXITCODE -ne 0) { throw 'Failed to generate format-only fixture.' }
    $fullReport = Join-Path $scratch 'whole Bible report.txt'
    Invoke-BibleCheck $fullText $fullReport 0
    $fullResult = Get-Content $fullReport -Raw
    foreach ($expected in @('Books=66', 'Chapters=1189', 'Verses=31088', 'CoveredVerses=31101', 'QueriesChecked=31101')) {
        if ($fullResult -notmatch "(?m)^$expected$") { throw "Full Bible check missing $expected : $fullResult" }
    }

    $bibleDirectory = Join-Path $isolated 'Bibles'
    New-Item -ItemType Directory $bibleDirectory | Out-Null
    python tools/import_bible.py $fullText (Join-Path $bibleDirectory 'krv.json') --name '진단용 예시' > (Join-Path $results 'prepare-ui.txt') 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Failed to prepare UI fixture.' }
    foreach ($configMode in @('fresh', 'truncated', 'invalid-line-limit')) {
        $configPath = "$exe.cfg"
        if ($configMode -eq 'truncated') { [IO.File]::WriteAllBytes($configPath, [byte[]]@(255,255)) }
        if ($configMode -eq 'invalid-line-limit') {
            $bytes = New-Object byte[] 209
            [BitConverter]::GetBytes([int]::MaxValue).CopyTo($bytes, 205)
            [IO.File]::WriteAllBytes($configPath, $bytes)
        }
        $uiReport = Join-Path $scratch "UI $configMode report.txt"
        Invoke-BibleCheck '' $uiReport 0 '--check-ui'
        if ((Get-Content $uiReport -Raw) -notmatch 'UI OK') { throw 'UI check did not pass.' }
    }
    $cp949 = Join-Path $scratch 'CP949 input.txt'
    [Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance)
    $encoding = [Text.Encoding]::GetEncoding(949)
    [IO.File]::WriteAllText($cp949, [IO.File]::ReadAllText($text), $encoding)
    Invoke-BibleCheck $cp949 (Join-Path $scratch 'CP949 report.txt') 0
    Invoke-BibleCheck (Join-Path $scratch 'missing file.json') (Join-Path $scratch 'missing report.txt') 1
    $badText = Join-Path $scratch 'bad TXT input.txt'
    Set-Content $badText -Value 'not a verse' -Encoding utf8
    Invoke-BibleCheck $badText (Join-Path $scratch 'bad TXT report.txt') 1
    Write-Host 'Published EXE passed: 66 books / 1189 chapters / 31101 verse queries, SQLite, imports, Unicode paths, UI navigation and settings.'
    Write-Host 'PowerPoint COM rendering: NOT TESTED (Office is not installed on hosted runners).'
} catch {
    $_ | Out-String | Set-Content (Join-Path $results 'failure.txt') -Encoding utf8
    throw
} finally {
    Get-ChildItem $scratch -Filter '*report.txt' | Copy-Item -Destination $results
    Remove-Item $scratch -Recurse -Force
}
