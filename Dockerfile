FROM ://microsoft.com AS base
WORKDIR /app

FROM ://microsoft.com AS build
WORKDIR /src

COPY ["TelegramCarsBot.csproj", "./"]
RUN dotnet restore "TelegramCarsBot.csproj"

COPY . .
RUN dotnet publish "TelegramCarsBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramCarsBot.dll"]
