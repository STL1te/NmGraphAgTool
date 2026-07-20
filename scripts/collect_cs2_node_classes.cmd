@echo off
setlocal

set "SCRIPT_DIR=%~dp0"

where python >nul 2>nul
if %errorlevel% equ 0 (
    python "%SCRIPT_DIR%collect_cs2_node_classes.py" %*
) else (
    py "%SCRIPT_DIR%collect_cs2_node_classes.py" %*
)

endlocal
