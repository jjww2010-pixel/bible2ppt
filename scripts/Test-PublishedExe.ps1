param([Parameter(Mandatory = $true)][string]$PublishDirectory)

$ErrorActionPreference = 'Stop'
$exe = Join-Path (Resolve-Path $PublishDirectory) 'Bible2PPT.exe'
if (!(Test-Path $exe)) { throw 'Bible2PPT.exe was not published.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('Bible2PPT smoke ' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null

function Invoke-BibleCheck([string]$InputFile, [string]$Report, [int]$ExpectedExit) {
    $process = Start-Process -FilePath $exe -ArgumentList @('--check-bible', ('"{0}"' -f $InputFile), ('"{0}"' -f $Report)) -PassThru
    $null = $process.Handle
    if (!$process.WaitForExit(60000)) {
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
    Write-Host 'Published EXE passed: runtime startup, SQLite initialization, local Bible reading, invalid-file handling.'
} finally {
    Remove-Item $scratch -Recurse -Force
}
