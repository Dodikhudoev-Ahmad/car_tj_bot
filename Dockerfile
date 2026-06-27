
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app
 
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TelegramCarsBot.csproj", "./"]
RUN dotnet restore "TelegramCarsBot.csproj"
COPY . .
RUN dotnet publish "TelegramCarsBot.csproj" -c Release -o /app/publish
 
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramCarsBot.dll"]
 