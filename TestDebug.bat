@echo off
IF [%1] == [] GOTO Usage
cd src\ServerTest\bin\debug\netcoreapp3.1
start Server.exe
TIMEOUT 4 > NUL
cd ..\..\..\..\..

cd src\ClientTest\bin\debug\netcoreapp3.1
FOR /L %%i IN (1,1,%1) DO (
ECHO Starting client %%i
start Client.exe
TIMEOUT 2 > NUL
)
cd ..\..\..\..\..
@echo on
EXIT /b

:Usage
ECHO Specify the number of client nodes to start.
@echo on
EXIT /b