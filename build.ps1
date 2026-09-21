$ErrorActionPreference = "Stop"

Write-Host "Restoring packages..."
dotnet restore .\GameLauncher.sln

Write-Host "Building Phase 3..."
dotnet build .\GameLauncher.sln -c Release --no-restore

Write-Host "Running Phase 3 tests..."
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release --no-build

Write-Host "Phase 3 build and tests completed successfully."
