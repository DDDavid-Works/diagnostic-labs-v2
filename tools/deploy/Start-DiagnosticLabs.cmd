@echo off
rem Shortcut target for stations. Bypasses the script execution policy for this one script only.
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Start-DiagnosticLabs.ps1" %*
