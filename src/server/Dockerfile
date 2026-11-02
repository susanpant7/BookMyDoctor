# Built from the repository ROOT (docker build -f src/server/Dockerfile .), which is also Render's default build context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/server/MyHospitalAI.csproj server/
RUN dotnet restore server/MyHospitalAI.csproj

COPY src/server/ server/
RUN dotnet publish server/MyHospitalAI.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The SQLite database lives in /app/data: mount a volume there to keep it.
RUN mkdir -p /app/data && chown -R $APP_UID /app/data
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "MyHospitalAI.dll"]
