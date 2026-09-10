@echo off
REM Double-click this. It runs install.ps1 with a scoped execution-policy
REM bypass so Windows' default script-blocking doesn't get in the way --
REM this does not change any system-wide setting.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
