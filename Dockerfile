# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies first for better layer caching
COPY Aidly.csproj ./
RUN dotnet restore "Aidly.csproj"

# Copy everything else and build
COPY . ./
RUN dotnet publish "Aidly.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "Aidly.dll"]
