# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["backend/SunGrid.Api/SunGrid.Api.csproj", "backend/SunGrid.Api/"]
RUN dotnet restore "backend/SunGrid.Api/SunGrid.Api.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/backend/SunGrid.Api"
RUN dotnet publish "SunGrid.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Tell ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SunGrid.Api.dll"]
