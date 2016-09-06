@echo off

nuget push *.nupkg

echo.
set /P _ret=All packages pushed...
goto :eof