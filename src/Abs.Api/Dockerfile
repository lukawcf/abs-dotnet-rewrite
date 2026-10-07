# Build context is the repository root: docker build -f src/Abs.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Abs.Api/Abs.Api.csproj src/Abs.Api/
COPY src/Abs.Data/Abs.Data.csproj src/Abs.Data/
RUN dotnet restore src/Abs.Api/Abs.Api.csproj
COPY src/ src/
RUN dotnet publish src/Abs.Api/Abs.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Abs.Api.dll"]
