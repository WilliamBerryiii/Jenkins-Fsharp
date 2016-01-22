@rem builds the solution and runs the tests using FAKE (F# Make tool)
@rem NOTE! If you get asked if Command can make a change, it's because Nancy has to register the URL, which needs administrator privileges

@echo off
cls

.paket\paket.bootstrapper.exe
if errorlevel 1 (
    exit /b %errorlevel%
)

.paket\paket.exe restore
if errorlevel 1 (
    exit /b %errorlevel%
)

packages\FAKE\tools\FAKE.exe build.fsx %*