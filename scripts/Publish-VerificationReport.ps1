$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$directory = Join-Path $env:RUNNER_TEMP ('verification-' + $env:CHECK_RUNTIME)
$branch = 'verification-' + $env:CHECK_RUNTIME + '-' + $env:CHECKED_COMMIT + '-' + $env:GITHUB_RUN_ID + '-' + $env:GITHUB_RUN_ATTEMPT
git worktree add --detach $directory
if ($LASTEXITCODE -ne 0) { throw 'Cannot create report worktree.' }
git -C $directory switch --orphan $branch
if ($LASTEXITCODE -ne 0) { throw 'Cannot create report branch.' }
foreach ($name in @('verification-tests', 'verification-exe')) {
    $source = Join-Path $root $name
    if (Test-Path $source) { Copy-Item $source $directory -Recurse }
}
if ($env:EXE_RESULT -eq 'success' -and $env:REGRESSION_RESULT -eq 'success') {
    $zipName = 'Bible2PPT-' + $env:CHECK_RUNTIME + '.zip'
    $published = Join-Path $root ('artifacts/' + $env:CHECK_RUNTIME)
    Compress-Archive -Path (Join-Path $published '*') -DestinationPath (Join-Path $directory $zipName)
    $zipHash = (Get-FileHash (Join-Path $directory $zipName) -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content (Join-Path $directory ($zipName + '.sha256')) "$zipHash  $zipName" -Encoding ascii
}
$result = @"
# Windows verification result

Tested commit: $env:CHECKED_COMMIT

Runtime: $env:CHECK_RUNTIME

Operating system: $env:RUNNER_OS

Regression tests: $env:REGRESSION_RESULT

EXE publish: $env:PUBLISH_RESULT

Published EXE checks: $env:EXE_RESULT

Run: https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID

These checks run on a GitHub-hosted Windows machine. Full-format fixtures contain generated test sentences, not scripture text.
Published EXE checks cover 66 books, 1189 chapters, 31101 individual verse queries, imports, SQLite, Unicode/space paths, and the real MainForm's navigation, book selection, Bible selection, three separate translation files and selectors, and settings/configuration handling.

PowerPoint COM rendering: **NOT TESTED**. Microsoft Office is not installed on GitHub-hosted runners.
GUI diagnostics disable the rendering dependency; they do not bypass the MainForm or its event handlers.
Read the TRX files and individual EXE reports for the detailed results. A skipped/failed check is not a pass.
"@
Set-Content (Join-Path $directory 'README.md') $result -Encoding utf8
git -C $directory config user.name 'github-actions[bot]'
git -C $directory config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git -C $directory add .
git -C $directory commit -m 'Record Windows verification result'
if ($LASTEXITCODE -ne 0) { throw 'Cannot commit report.' }
git -C $directory push origin "HEAD:refs/heads/$branch"
if ($LASTEXITCODE -ne 0) { throw 'Cannot publish report.' }
Write-Host "Verification report: https://github.com/$env:GITHUB_REPOSITORY/tree/$branch"
