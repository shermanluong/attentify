how to publish project

dotnet --list-sdks
dotnet ef database update
dotnet ef database add ...
dotnet tool install --global dotnet-ef
dotnet publish -c Release -o ./publish
