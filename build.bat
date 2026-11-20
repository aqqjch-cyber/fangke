@echo off
chcp 65001 >nul
setlocal

echo ============================================
echo   万方商贸管家 - 打包脚本
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 dotnet 命令。
    echo 请先安装 .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/3] 正在编译...
dotnet publish "%~dp0src\FangKeDelivery\FangKeDelivery.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%~dp0dist" --nologo
if errorlevel 1 (
    echo.
    echo [错误] 编译失败。
    pause
    exit /b 1
)

echo.
echo [2/3] 清理中间文件...
if exist "%~dp0dist\data" rmdir /s /q "%~dp0dist\data"

echo.
echo [3/3] 完成！
echo.
echo 可执行文件位置:
echo   %~dp0dist\WanfangTrade.exe
echo.
echo 直接把 WanfangTrade.exe 复制到任意 Windows 电脑即可运行，
echo 首次运行会在同目录自动创建 data 文件夹保存数据。
echo.
pause
