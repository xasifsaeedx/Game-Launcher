$ErrorActionPreference = "Stop"

Write-Host "Restoring packages..."
dotnet restore .\GameLauncher.sln

Write-Host "Building launcher..."
dotnet build .\GameLauncher.sln -c Release --no-restore

Write-Host "Running launcher tests..."
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release --no-build

Write-Host "Build and tests completed successfully."
