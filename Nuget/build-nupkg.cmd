@echo off
setlocal enableextensions disabledelayedexpansion

:: Parse options
:GETOPTS
 if /I "%~1" == "/?" goto USAGE
 if /I "%~1" == "/Help" goto USAGE
 shift
if not (%1)==() goto GETOPTS

echo.
echo Creating nupkg directory structure
md nupkg

copy ..\LICENSE nupkg /y || goto err

echo Creating NuGet Package
nuget help > NUL
IF ERRORLEVEL 1 (
    echo Please install nuget.exe from http://nuget.org
    goto err
)
nuget pack nupkg\Microsoft.IoT.Mubiquity.nuspec || goto err


:end

echo Success
exit /b 0

:err
  echo Script failed!
  exit /b 1