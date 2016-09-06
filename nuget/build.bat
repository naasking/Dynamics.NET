@echo on

set libs=Dynamics
set tools=
set files=%tools% %libs%

:: clear whole folder
for %%i in (%files%) do rd /S /Q "%%i"

:: generate packages
del *.nupkg
nuget pack Dynamics.NET.nuspec
