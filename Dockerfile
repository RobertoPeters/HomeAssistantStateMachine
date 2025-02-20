FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS="http://+:80"
ENV DOTNET_RUNNING_IN_CONTAINER=true
COPY src/HomeAssistantStateMachine/DeployLinux/ .
RUN mkdir Settings
USER $APP_UID
ENTRYPOINT ["./HomeAssistantStateMachine"]