@echo off

rem =========================================================
rem =                                                       =
rem =             Publish script HASM                       =
rem =                                                       =
rem =========================================================
dotnet publish -c Release -r linux-musl-x64 --self-contained true -o DeployLinux