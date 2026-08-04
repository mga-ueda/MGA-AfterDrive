param(
    [string] $Configuration = 'Debug',
    [string[]] $AppArgs = @()
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\MgaAfterDrive\MgaAfterDrive.csproj'
$exe = Join-Path $repoRoot "src\MgaAfterDrive\bin\$Configuration\net8.0-windows\MGA AfterDrive.exe"
$log = Join-Path $PSScriptRoot 'last-run.log'

function Write-Log([string] $Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $log -Value $line -Encoding UTF8
    Write-Host $line
}

Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
Write-Log "Build: $project"
dotnet build $project -c $Configuration -v q
if ($LASTEXITCODE -ne 0) {
    Write-Log "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Log "EXE not found: $exe"
    exit 1
}

Write-Log "Start: $exe $($AppArgs -join ' ')"
try {
    if ($AppArgs.Count -gt 0) {
        Start-Process -FilePath $exe -ArgumentList $AppArgs | Out-Null
    }
    else {
        Start-Process -FilePath $exe | Out-Null
    }
    Write-Log 'Start-Process OK'
}
catch {
    Write-Log "Start-Process failed: $($_.Exception.Message)"
    exit 1
}
