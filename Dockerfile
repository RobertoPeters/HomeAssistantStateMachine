FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
EXPOSE 3080
ENV ASPNETCORE_URLS="http://+:3080"
ENV DOTNET_RUNNING_IN_CONTAINER=true
COPY src/Hasm/DeployLinux/ .
RUN mkdir Settings
USER $APP_UID
ENTRYPOINT ["dotnet", "Hasm.dll"]