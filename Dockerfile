FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Yakku.API/Yakku.API.csproj", "src/Yakku.API/"]
COPY ["src/Yakku.Application/Yakku.Application.csproj", "src/Yakku.Application/"]
COPY ["src/Yakku.Domain/Yakku.Domain.csproj", "src/Yakku.Domain/"]
COPY ["src/Yakku.Infrastructure/Yakku.Infrastructure.csproj", "src/Yakku.Infrastructure/"]
RUN dotnet restore "src/Yakku.API/Yakku.API.csproj"

COPY . .
WORKDIR /src/src/Yakku.API
RUN dotnet publish "./Yakku.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "Yakku.API.dll"]
