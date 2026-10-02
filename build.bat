@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
dotnet restore VRChatSDKInspectorJPLocalization.sln
if errorlevel 1 goto :failed
dotnet build VRChatSDKInspectorJPLocalization.sln -c Release --no-restore
if errorlevel 1 goto :failed
echo Build succeeded.
echo Output: %~dp0src\VRChatSDKInspectorJPLocalization\bin\Release\net10.0-windows\
popd
exit /b 0
:failed
echo Build FAILED. Install the .NET 10 SDK or a compatible newer SDK and check the errors above.
popd
exit /b 1
