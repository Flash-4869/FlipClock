@echo off
rem Build FlipClock with the in-box .NET Framework compiler (no SDK needed)
setlocal
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set SRC=%~dp0src
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /win32icon:"%~dp0app.ico" /out:"%~dp0FlipClock.exe" /r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" /r:"%FW%\System.dll" /r:"%FW%\System.Core.dll" /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" "%SRC%\AssemblyInfo.cs" "%SRC%\Program.cs" "%SRC%\FlipDigit.cs" "%SRC%\SettingsWindow.cs" "%SRC%\Strings.cs" "%SRC%\TrayIcon.cs"
if errorlevel 1 (
  echo Build FAILED
  exit /b 1
)
echo Build OK: %~dp0FlipClock.exe
