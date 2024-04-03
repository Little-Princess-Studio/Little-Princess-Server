cd ..
dotnet build LPS.Server.Demo --configuration Release
cd ./LPS.Server.Demo
dotnet ./bin/Release/net8.0/LPS.Server.Demo.dll bydefault
