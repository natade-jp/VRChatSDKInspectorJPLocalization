@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
dotnet publish src\VRChatSDKInspectorJPLocalization\VRChatSDKInspectorJPLocalization.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts\publish\win-x64
if errorlevel 1 goto :failed
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\package.ps1"
if errorlevel 1 goto :failed
echo Publish succeeded.
echo Distribution folder: %~dp0artifacts\distribution\VRChatSDKInspectorJPLocalization-win-x64\
echo Zip this folder to distribute the application and user guide.
popd
exit /b 0
:failed
echo Publish FAILED. Check the errors above.
popd
exit /b 1
