param([string]$SVSGameDir = $env:SVS_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (-not $SVSGameDir) { throw 'Pass -SVSGameDir with the SamabakeScramble game directory.' }
dotnet build "$PSScriptRoot\src\SVS_Pregnancy.csproj" -c Release "-p:SVSGameDir=$SVSGameDir" -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
