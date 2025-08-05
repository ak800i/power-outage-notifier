@echo off
rem the shenanigans before "docker" commands are here to append the current date to the output filename. 
setlocal

rem Get current date in YYYY.MM.DD format
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set "datetime=%%I"
set "year=%datetime:~0,4%"
set "month=%datetime:~4,2%"
set "day=%datetime:~6,2%"

rem Concatenate the date components to form the filename
set "filename=poweroutagenotifier-%year%.%month%.%day%.tar"

rem Run Docker commands
docker build -t belgradebc/poweroutagenotifier .
docker save -o "%filename%" belgradebc/poweroutagenotifier
docker push belgradebc/poweroutagenotifier

endlocal
