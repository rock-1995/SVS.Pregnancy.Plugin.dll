param([string]$SvsBodyFixture)
$ErrorActionPreference = 'Stop'
if ($SvsBodyFixture) {
    dotnet run --project "$PSScriptRoot\tests\GeometryRegression.csproj" -c Release -- $SvsBodyFixture
} else {
    dotnet run --project "$PSScriptRoot\tests\GeometryRegression.csproj" -c Release
}
if ($LASTEXITCODE -ne 0) { throw 'Geometry regressions failed.' }
dotnet run --project "$PSScriptRoot\runtime-tests\RuntimeRegression.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runtime adapter regressions failed.' }
