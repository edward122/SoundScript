@echo off
echo 🚀 Building SoundScript Release...

REM Clean previous builds
echo 📁 Cleaning previous builds...
if exist "SoundScript\bin\Release" rmdir /s /q "SoundScript\bin\Release"
if exist "SoundScript\obj\Release" rmdir /s /q "SoundScript\obj\Release"
if exist "Release" rmdir /s /q "Release"

REM Build the application
echo 🔨 Building application...
dotnet publish SoundScript\SoundScript.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release

if %ERRORLEVEL% NEQ 0 (
    echo ❌ Build failed!
    pause
    exit /b 1
)

echo ✅ Build completed successfully!
echo 📦 Release files are in the 'Release' folder

REM Show file size
for %%f in (Release\SoundScript.exe) do echo 📏 File size: %%~zf bytes

echo.
echo 🎉 Ready for distribution!
echo 📁 Location: %CD%\Release\SoundScript.exe
echo.
echo Users can now run SoundScript.exe directly - no installation required!
pause 