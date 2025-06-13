FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS="http://+:80"
ENV DOTNET_RUNNING_IN_CONTAINER=true
COPY src/HomeAssistantStateMachine/DeployLinux/ .
RUN mkdir Settings
USER $APP_UID
ENTRYPOINT ["dotnet", "HomeAssistantStateMachine.dll"]