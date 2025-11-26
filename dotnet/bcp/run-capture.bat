@echo off
REM Quick test script for bulk copy packet capture

echo Building project...
dotnet build

echo.
echo ===================================================
echo IMPORTANT: Start packet capture NOW before running
echo ===================================================
echo.
echo Press any key to run the test (after starting capture)...
pause > nul

echo Running bulk copy test...
dotnet run

echo.
echo Test completed! Stop your packet capture now.
echo.
pause
