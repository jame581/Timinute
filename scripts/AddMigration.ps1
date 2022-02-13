
param ($name)

$location = Get-Location;

dotnet tool install --global dotnet-ef

Set-Location "../Timinute/Server";

$startupProject = Get-Childitem -Include Timinute.Server.csproj -Recurse

Set-Location $startupProject.Directory;
Write-Host -NoNewline "Creating migrations ";
Write-Host -ForegroundColor Yellow $name;

dotnet ef migrations add $name --output-dir "Data/Migrations" --project $startupProject --startup-project $startupProject;

Write-Host;

Set-Location $location;