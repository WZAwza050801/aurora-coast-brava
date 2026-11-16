@echo off
title AURORA - Unity MCP launcher
rem Double-click this file. It must run with a normal user token: Unity refuses
rem to start when the parent process is elevated.
"%~dp0..\.workbuddy\venvs\unity-mcp\Scripts\python.exe" "%~dp0start_unity_mcp.py"
if errorlevel 1 pause
timeout /t 6 >nul
