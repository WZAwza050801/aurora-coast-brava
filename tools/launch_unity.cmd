@echo off
rem Launch the Unity Editor with the normal (non-elevated) user token.
rem Unity refuses to run when the parent process is elevated.
start "" "D:\桌面\Unity Hub\6000.0.29f1\Editor\Unity.exe" -projectPath "D:\ai_for_games\赛车游戏复刻\Alsphalt8" -disable-assembly-updater
exit /b 0
