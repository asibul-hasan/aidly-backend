# Base runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Configure default listening port (7860 for Hugging Face Spaces compatibility)
ENV ASPNETCORE_URLS=http://+:7860 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 7860

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching during restore
COPY ["src/Host/AidlyErp.Api/AidlyErp.Api.csproj", "src/Host/AidlyErp.Api/"]
COPY ["src/Shared/AidlyErp.Shared.Core/AidlyErp.Shared.Core.csproj", "src/Shared/AidlyErp.Shared.Core/"]
COPY ["src/Shared/AidlyErp.Shared.Contracts/AidlyErp.Shared.Contracts.csproj", "src/Shared/AidlyErp.Shared.Contracts/"]
COPY ["src/Shared/AidlyErp.Shared.Infrastructure/AidlyErp.Shared.Infrastructure.csproj", "src/Shared/AidlyErp.Shared.Infrastructure/"]

COPY ["src/Modules/Sys/AidlyErp.Sys.Domain/AidlyErp.Sys.Domain.csproj", "src/Modules/Sys/AidlyErp.Sys.Domain/"]
COPY ["src/Modules/Sys/AidlyErp.Sys.Contracts/AidlyErp.Sys.Contracts.csproj", "src/Modules/Sys/AidlyErp.Sys.Contracts/"]
COPY ["src/Modules/Sys/AidlyErp.Sys.Application/AidlyErp.Sys.Application.csproj", "src/Modules/Sys/AidlyErp.Sys.Application/"]
COPY ["src/Modules/Sys/AidlyErp.Sys.Infrastructure/AidlyErp.Sys.Infrastructure.csproj", "src/Modules/Sys/AidlyErp.Sys.Infrastructure/"]

COPY ["src/Modules/Hrm/AidlyErp.Hrm.Domain/AidlyErp.Hrm.Domain.csproj", "src/Modules/Hrm/AidlyErp.Hrm.Domain/"]
COPY ["src/Modules/Hrm/AidlyErp.Hrm.Contracts/AidlyErp.Hrm.Contracts.csproj", "src/Modules/Hrm/AidlyErp.Hrm.Contracts/"]
COPY ["src/Modules/Hrm/AidlyErp.Hrm.Application/AidlyErp.Hrm.Application.csproj", "src/Modules/Hrm/AidlyErp.Hrm.Application/"]
COPY ["src/Modules/Hrm/AidlyErp.Hrm.Infrastructure/AidlyErp.Hrm.Infrastructure.csproj", "src/Modules/Hrm/AidlyErp.Hrm.Infrastructure/"]

COPY ["src/Modules/Fin/AidlyErp.Fin.Domain/AidlyErp.Fin.Domain.csproj", "src/Modules/Fin/AidlyErp.Fin.Domain/"]
COPY ["src/Modules/Fin/AidlyErp.Fin.Contracts/AidlyErp.Fin.Contracts.csproj", "src/Modules/Fin/AidlyErp.Fin.Contracts/"]
COPY ["src/Modules/Fin/AidlyErp.Fin.Application/AidlyErp.Fin.Application.csproj", "src/Modules/Fin/AidlyErp.Fin.Application/"]
COPY ["src/Modules/Fin/AidlyErp.Fin.Infrastructure/AidlyErp.Fin.Infrastructure.csproj", "src/Modules/Fin/AidlyErp.Fin.Infrastructure/"]

COPY ["src/Modules/Inv/AidlyErp.Inv.Domain/AidlyErp.Inv.Domain.csproj", "src/Modules/Inv/AidlyErp.Inv.Domain/"]
COPY ["src/Modules/Inv/AidlyErp.Inv.Contracts/AidlyErp.Inv.Contracts.csproj", "src/Modules/Inv/AidlyErp.Inv.Contracts/"]
COPY ["src/Modules/Inv/AidlyErp.Inv.Application/AidlyErp.Inv.Application.csproj", "src/Modules/Inv/AidlyErp.Inv.Application/"]
COPY ["src/Modules/Inv/AidlyErp.Inv.Infrastructure/AidlyErp.Inv.Infrastructure.csproj", "src/Modules/Inv/AidlyErp.Inv.Infrastructure/"]

COPY ["src/Modules/Pur/AidlyErp.Pur.Domain/AidlyErp.Pur.Domain.csproj", "src/Modules/Pur/AidlyErp.Pur.Domain/"]
COPY ["src/Modules/Pur/AidlyErp.Pur.Contracts/AidlyErp.Pur.Contracts.csproj", "src/Modules/Pur/AidlyErp.Pur.Contracts/"]
COPY ["src/Modules/Pur/AidlyErp.Pur.Application/AidlyErp.Pur.Application.csproj", "src/Modules/Pur/AidlyErp.Pur.Application/"]
COPY ["src/Modules/Pur/AidlyErp.Pur.Infrastructure/AidlyErp.Pur.Infrastructure.csproj", "src/Modules/Pur/AidlyErp.Pur.Infrastructure/"]

COPY ["src/Modules/Sal/AidlyErp.Sal.Domain/AidlyErp.Sal.Domain.csproj", "src/Modules/Sal/AidlyErp.Sal.Domain/"]
COPY ["src/Modules/Sal/AidlyErp.Sal.Contracts/AidlyErp.Sal.Contracts.csproj", "src/Modules/Sal/AidlyErp.Sal.Contracts/"]
COPY ["src/Modules/Sal/AidlyErp.Sal.Application/AidlyErp.Sal.Application.csproj", "src/Modules/Sal/AidlyErp.Sal.Application/"]
COPY ["src/Modules/Sal/AidlyErp.Sal.Infrastructure/AidlyErp.Sal.Infrastructure.csproj", "src/Modules/Sal/AidlyErp.Sal.Infrastructure/"]

# Restore project dependencies
RUN dotnet restore "src/Host/AidlyErp.Api/AidlyErp.Api.csproj"

# Copy remaining source code and publish the application
COPY src/ src/
WORKDIR "/src/src/Host/AidlyErp.Api"
RUN dotnet publish "AidlyErp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production image stage
FROM base AS final
WORKDIR /app

# Copy published application binaries with non-root ownership
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Pre-create uploads directory and assign ownership to non-root user
RUN mkdir -p /app/uploads && chown -R $APP_UID /app

# Set non-root container user
USER $APP_UID

ENTRYPOINT ["dotnet", "AidlyErp.Api.dll"]
