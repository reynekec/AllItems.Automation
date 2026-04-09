$ErrorActionPreference = "Stop"

Write-Host "Building solution..."
dotnet build WpfAutomation.sln

Write-Host "Running tests..."
dotnet test WpfAutomation.sln --no-build
