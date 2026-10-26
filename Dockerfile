FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Mesa.WhatsApp.sln ./
COPY src/Mesa.WhatsApp.Domain/Mesa.WhatsApp.Domain.csproj src/Mesa.WhatsApp.Domain/
COPY src/Mesa.WhatsApp.Application/Mesa.WhatsApp.Application.csproj src/Mesa.WhatsApp.Application/
COPY src/Mesa.WhatsApp.Infrastructure/Mesa.WhatsApp.Infrastructure.csproj src/Mesa.WhatsApp.Infrastructure/
COPY src/Mesa.WhatsApp.Api/Mesa.WhatsApp.Api.csproj src/Mesa.WhatsApp.Api/
RUN dotnet restore src/Mesa.WhatsApp.Api/Mesa.WhatsApp.Api.csproj
COPY src/ src/
RUN dotnet publish src/Mesa.WhatsApp.Api/Mesa.WhatsApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Mesa.WhatsApp.Api.dll"]
