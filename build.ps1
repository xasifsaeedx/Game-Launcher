$ErrorActionPreference = "Stop"

Write-Host "Restoring packages..."
dotnet restore .\GameLauncher.sln

Write-Host "Building v1.0 / Phase 8..."
dotnet build .\GameLauncher.sln -c Release --no-restore

Write-Host "Running Phase 8 tests..."
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release --no-build

Write-Host "Phase 8 build and tests completed successfully."
