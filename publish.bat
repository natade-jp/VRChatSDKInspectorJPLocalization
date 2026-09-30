@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
dotnet publish src\VRChatSDKInspectorJPLocalization\VRChatSDKInspectorJPLocalization.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts\publish\win-x64
if errorlevel 1 goto :failed
echo Publish succeeded.
echo Output: %~dp0artifacts\publish\win-x64\
popd
exit /b 0
:failed
echo Publish FAILED. Check the errors above.
popd
exit /b 1
