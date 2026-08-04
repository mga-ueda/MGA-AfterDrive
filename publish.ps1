#Requires -Version 5.1
<#
.SYNOPSIS
  MGA AfterDrive を単一 EXE として publish/ へ出力する。
#>
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$out = Join-Path $root "publish"
$project = Join-Path $root "src\MgaAfterDrive\MgaAfterDrive.csproj"

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -o $out

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Published (single file):"
Get-ChildItem $out -File | ForEach-Object { "  $($_.Name)  ($([math]::Round($_.Length / 1KB, 1)) KB)" }
Write-Host ""
Write-Host "Distribute: $out\MGA AfterDrive.exe"
