@ECHO OFF

SET Hasm=src\HomeAssistantStateMachine
RMDIR /S /Q "%Hasm%\DeployLinux" >NUL

ECHO.
ECHO ** Publish HASM for Linux
ECHO.
pushd %Hasm%
CALL publishLinux.bat >NUL
popd

ECHO.
ECHO ** Checking HASM for Linux
ECHO.
IF NOT EXIST "%Hasm%\DeployLinux\HomeAssistantStateMachine.dll" (
  ECHO Camas Release build not found "%Hasm%\DeployLinux\HomeAssistantStateMachine.dll"
  GOTO ERROR
)

ECHO.
ECHO ** Creating docker image
ECHO.
docker compose down
docker build -f Dockerfile -t robertpeters/homeassistantstatemachine:dev .
docker compose up -d

GOTO SUCCESS


:ERROR
ECHO.
ECHO !! ERROR - Error occured
GOTO END

:SUCCESS
GOTO END

:END
ECHO.
PAUSE