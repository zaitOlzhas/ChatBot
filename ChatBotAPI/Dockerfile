FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./ChatBotAPI.csproj", "ChatBotAPI/"]
RUN dotnet restore "ChatBotAPI/ChatBotAPI.csproj"
COPY . /src/ChatBotAPI
WORKDIR "/src/ChatBotAPI"
RUN dotnet build "ChatBotAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ChatBotAPI.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://*:5000;https://*:5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ChatBotAPI.dll"]