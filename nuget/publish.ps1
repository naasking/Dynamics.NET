./nuget push ..\Dynamics\bin\Release\*.nupkg -Source https://api.nuget.org/v3/index.json
#ls ../Dynamics/bin/Release/*.nupkg

Read-Host -Prompt "All packages pushed, press enter to continue"
