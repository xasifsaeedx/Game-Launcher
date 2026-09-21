$ErrorActionPreference = "Stop"

Write-Host "Restoring packages..."
dotnet restore .\GameLauncher.sln

Write-Host "Building Phase 0..."
dotnet build .\GameLauncher.sln -c Release --no-restore

Write-Host "Running Phase 0 smoke tests..."
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release --no-build

Write-Host "Phase 0 build and tests completed successfully."
