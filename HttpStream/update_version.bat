@echo off

pushd %~dp0

call :find_exe git
if ERRORLEVEL 1 goto :errend
call :set_revision
if ERRORLEVEL 1 goto :errend
call :set_commitid
if ERRORLEVEL 1 goto :errend

call :set_build

set ASMVER=2.0.%REV%.%BUILD%
echo Version: %ASMVER% (Rev=%REV%, Build=%BUILD%, Commit=%COMMIT%)

call :generate_asminfo Properties

popd
goto :EOF

rem -------------------------------------------------------------------------
rem Generate AssemblyInfo.cs from AssemblyInfo.Template.cs.
rem -------------------------------------------------------------------------
:generate_asminfo
pushd "%1"
echo %cd%
copy /Y AssemblyInfo.Template.cs AssemblyInfo.tmp >NUL 2>NUL
echo [assembly: AssemblyVersion("%ASMVER%")] >> AssemblyInfo.tmp
echo [assembly: AssemblyFileVersion("%ASMVER%")] >> AssemblyInfo.tmp
echo [assembly: AssemblyInformationalVersion("%ASMVER% (%COMMIT%)")] >> AssemblyInfo.tmp

fc /B AssemblyInfo.tmp AssemblyInfo.cs >NUL 2>NUL
if ERRORLEVEL 1 (
	echo %1\AssemblyInfo.cs r%REV% updated.
	del /F AssemblyInfo.cs >NUL 2>NUL
	ren AssemblyInfo.tmp AssemblyInfo.cs
) else (
	echo No update for %1\AssemblyInfo.cs r%REV%.
	del /F AssemblyInfo.tmp >NUL 2>NUL
)
popd
goto :EOF

rem -------------------------------------------------------------------------
rem Set REV environment variable using git's commit count.
rem -------------------------------------------------------------------------
:set_revision
if NOT "%FORCE_REV%"=="" (
	set REV=%FORCE_REV%
	goto :EOF
)
for /f %%c in ('git log --oneline 2^>NUL ^| find /c /v ""') do set REV=%%c
if "%REV%"=="" (
	echo Could not determine source revision.
	exit /b 1
)
goto :EOF

rem -------------------------------------------------------------------------
rem Set COMMIT environment variable using git's commit count.
rem -------------------------------------------------------------------------
:set_commitid
set COMMIT=unknown
for /f %%c in ('git log --oneline -1 2^>NUL') do set COMMIT=%%c
goto :EOF

rem -------------------------------------------------------------------------
rem set BUILD variable 
rem -------------------------------------------------------------------------
:set_build
if exist ._build.bat (
	call ._build.bat
) else (
	set BUILD=0
) 
goto :EOF

rem -------------------------------------------------------------------------
rem Determine whether the executable is found or not.
rem -------------------------------------------------------------------------
:find_exe
for /f %%c in ('where %1 2^>NUL') do exit /b 0
echo Command not found: %1
exit /b 1

rem -------------------------------------------------------------------------
:errend
popd
echo Build failed.
exit /b %ERRORLEVEL%
