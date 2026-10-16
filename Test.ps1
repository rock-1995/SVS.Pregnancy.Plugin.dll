param([string]$SvsBodyFixture, [string]$SvsSkirtFixture, [string]$SVSGameDir = $env:SVS_GAME_DIR)
$ErrorActionPreference = 'Stop'
if ($SvsBodyFixture) {
    dotnet run --project "$PSScriptRoot\tests\GeometryRegression.csproj" -c Release -- $SvsBodyFixture
} else {
    dotnet run --project "$PSScriptRoot\tests\GeometryRegression.csproj" -c Release
}
if ($LASTEXITCODE -ne 0) { throw 'Geometry regressions failed.' }
dotnet run --project "$PSScriptRoot\runtime-tests\RuntimeRegression.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runtime adapter regressions failed.' }
dotnet run --project "$PSScriptRoot\settings-tests\SettingsRegression.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Settings regressions failed.' }
if ($SvsSkirtFixture) {
    dotnet run --project "$PSScriptRoot\skirt-tests\SkirtRegression.csproj" -c Release -- $SvsSkirtFixture
} else {
    dotnet run --project "$PSScriptRoot\skirt-tests\SkirtRegression.csproj" -c Release
}
if ($LASTEXITCODE -ne 0) { throw 'Skirt regressions failed.' }
foreach ($project in @('body-change-tests/BodyChangeRegression.csproj', 'performance-tests/PerformanceRegression.csproj', 'breast-physics-tests/BreastPhysicsRegression.csproj', 'unrestricted-tests/UnrestrictedRegression.csproj')) {
    dotnet run --project (Join-Path $PSScriptRoot $project) -c Release
    if ($LASTEXITCODE -ne 0) { throw "Regression failed: $project" }
}
if ($SVSGameDir) {
    dotnet run --project "$PSScriptRoot\persistence-tests\PersistenceRegression.csproj" -c Release -- "$PSScriptRoot\bin\Release\net6.0\SVS_Pregnancy.dll" $SVSGameDir
    if ($LASTEXITCODE -ne 0) { throw 'Compiled persistence regressions failed.' }
}

if ($SVSGameDir) {
    dotnet run --project "$PSScriptRoot\startup-tests\StartupRegression.csproj" -c Release -- "$PSScriptRoot\bin\Release\net6.0\SVS_Pregnancy.dll" $SVSGameDir
    if ($LASTEXITCODE -ne 0) { throw 'Native startup hook regressions failed.' }
}
