@echo off
rem SharpQuest helper (game track). Usage from the top of your repository: sq init, sq test, sq update, sq help
rem MANAGED FILE: `sq update` and CI replace it with the official copy.
call "%~dp0SharpQuest\sq.cmd" %* & exit /b
