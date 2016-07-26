@echo off
setlocal ENABLEDELAYEDEXPANSION

set CURDIR=%~dp0

set COMPILER=vc140
set CONFIG=Release

call :call_vcenv_bat
if ERRORLEVEL 1 goto errend
call :find_exe git
if ERRORLEVEL 1 goto errend
call :find_exe msbuild
if ERRORLEVEL 1 goto errend
call :find_exe nuget
if ERRORLEVEL 1 goto errend

nuget restore
pushd HttpStream
msbuild HttpStream.csproj /p:Configuration=%CONFIG%
popd

rmdir /S /Q %CURDIR%\dist
mkdir %CURDIR%\dist
pushd %CURDIR%\dist
%CURDIR%\nuget pack %CURDIR%\\HttpStream\HttpStream.csproj -Prop Configuration=%CONFIG% -symbols
popd

goto :EOF

rem -------------------------------------------------------------------------
rem Determine whether the executable is found or not.
rem -------------------------------------------------------------------------
:find_exe
for /f %%c in ('where %1 2^>NUL') do exit /b 0
echo Command not found: %1
exit /b 1

rem -------------------------------------------------------------------------
rem Call vcvarsall.bat to initializes PATH and other env variables.
rem -------------------------------------------------------------------------
:call_vcenv_bat
if /i "%PLATFORM%"=="x64" (
	set OSARCH=%PROCESSOR_ARCHITEW6432%
	if "%OSARCH%"=="" set OSARCH=%PROCESSOR_ARCHITECTURE%
	if "%OSARCH%"=="" set OSARCH=x86
	if /i "%OSARCH%"=="x86" ( set vcarch=x86_amd64 ) else ( set vcarch=amd64 )
) else (
	set vcarch=x86
)

set VCBAT="!VS%COMPILER:~2%COMNTOOLS!\..\..\vc\vcvarsall.bat"
if not exist %VCBAT% (
	echo Error: %COMPILER% is not installed.
	exit /b 1
)
call %VCBAT% %vcarch%
goto :EOF
rem -------------------------------------------------------------------------
:errend
popd
echo Build failed.
exit /b %ERRORLEVEL%

:success
popd
goto :EOF
