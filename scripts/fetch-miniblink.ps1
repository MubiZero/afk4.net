[CmdletBinding()]
param(
    [string] $OutputDir = (Join-Path $PSScriptRoot '..\artifacts\miniblink')
)

# Miniblink — Chromium 132 одной DLL (github.com/weolar/miniblink49, Apache-2.0), движок экранов программ на ПК клуба
# (docs/roadmap/windows7.md). 54–66 МБ в git не держим: качаем закреплённый выпуск автора и сверяем суммы —
# архива и каждой DLL, чтобы подменённый файл не доехал до клубов. Распаковка — 7-Zip (есть на Windows-раннерах).

$ErrorActionPreference = 'Stop'

$release = 'https://github.com/weolar/miniblink49/releases/download/20251212/miniblink132_251212.7z'
$archiveSha256 = '237f5166701780918c27a1a340c3e1249776aee3db0673a3dffc1f9a009e6528'
$libraries = [ordered]@{
    'mb132_x32.dll' = 'a0dd15bf836cc0de1ea8a1244bc6527adb33e195493fa193764a25141edd5ddc'
    'mb132_x64.dll' = 'ae797512d56c3482119d279c4a4d3e016dc4974112c659c4df2652cd4e24cb40'
}

function Test-Sha256([string] $Path, [string] $Expected) {
    (Test-Path -LiteralPath $Path) -and ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Expected.ToUpperInvariant())
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$ready = $true
foreach ($library in $libraries.GetEnumerator()) {
    $ready = $ready -and (Test-Sha256 (Join-Path $OutputDir $library.Key) $library.Value)
}
if ($ready) {
    Write-Host "Miniblink is already in $OutputDir."
    return
}

$archive = Join-Path $OutputDir 'miniblink132.7z'
if (-not (Test-Sha256 $archive $archiveSha256)) {
    Invoke-WebRequest -Uri $release -OutFile $archive -UseBasicParsing
    if (-not (Test-Sha256 $archive $archiveSha256)) {
        throw "Miniblink archive checksum mismatch: $archive"
    }
}

$sevenZip = (Get-Command 7z, 7zz -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $sevenZip) {
    throw '7-Zip (7z or 7zz) is required to unpack Miniblink.'
}
& $sevenZip e $archive "-o$OutputDir" -y @($libraries.Keys) | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "7-Zip failed with exit code $LASTEXITCODE."
}

foreach ($library in $libraries.GetEnumerator()) {
    if (-not (Test-Sha256 (Join-Path $OutputDir $library.Key) $library.Value)) {
        throw "Miniblink library checksum mismatch: $($library.Key)"
    }
}
Write-Host "Miniblink unpacked to $OutputDir."
