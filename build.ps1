$ErrorActionPreference = "Stop"

Write-Host "Restoring packages..."
dotnet restore .\GameLauncher.sln

Write-Host "Building Phase 1..."
dotnet build .\GameLauncher.sln -c Release --no-restore

Write-Host "Running Phase 1 tests..."
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release --no-build

Write-Host "Phase 1 build and tests completed successfully."
