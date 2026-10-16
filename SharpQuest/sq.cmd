@echo off
rem SharpQuest helper for Windows (game track). Usage: sq init, sq test, sq update, sq report, sq help
rem Works with a .NET 9 or .NET 10 SDK. The first run builds sq itself (10-20 s).
rem MANAGED FILE: `sq update` and CI replace it with the official copy.

if exist "%~dp0tools\sq\sq.csproj" goto run
echo sq: SharpQuest\tools\sq\sq.csproj is missing. Run: git checkout -- SharpQuest/tools
exit /b 1

:run
rem The exit on the same line stops cmd from reading this file again after sq update replaces it.
dotnet run --project "%~dp0tools\sq" -- %* & exit /b
