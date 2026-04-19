# -------- Build stage --------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project file (dependency layer)
COPY PartInventoryService.DotNet/PartInventoryService.DotNet.csproj PartInventoryService.DotNet/

# Restore dependencies with BuildKit cache
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore PartInventoryService.DotNet/PartInventoryService.DotNet.csproj

# Copy source code
COPY PartInventoryService.DotNet/. PartInventoryService.DotNet/

# Publish application (framework-dependent, optimized)
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish PartInventoryService.DotNet/PartInventoryService.DotNet.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained \
    /p:UseAppHost=false


# -------- Runtime stage --------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Environment configuration
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080

# Copy only published output
COPY --from=build /app/publish ./

# Switch to non-root user
USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "PartInventoryService.DotNet.dll"]