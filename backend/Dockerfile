FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала только csproj — слой с restore переиспользуется, пока не менялись зависимости.
COPY src/Timesheet.Api/Timesheet.Api.csproj src/Timesheet.Api/
RUN dotnet restore src/Timesheet.Api/Timesheet.Api.csproj

COPY . .
RUN dotnet publish src/Timesheet.Api/Timesheet.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Тот же образ используется и как API, и как сидер: аргумент --seed наполняет базу и завершает работу.
ENTRYPOINT ["dotnet", "Timesheet.Api.dll"]
