# Base runtime image with Python setup
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Install Python3, pip, and the venv module
USER root
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    && rm -rf /var/lib/apt/lists/*

# Create a Python virtual environment and install required packages there
RUN python3 -m venv /opt/venv \
    && /opt/venv/bin/pip install --upgrade pip \
    && /opt/venv/bin/pip install TTS torch soundfile

# Update PATH so that the virtual environment's python is used by default
ENV PATH="/opt/venv/bin:${PATH}"
ENV PYTHONUNBUFFERED=1

# Set working directory and expose ports
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# --------------------------
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AutomaticComentaryService/AutomaticComentaryService.csproj", "AutomaticComentaryService/"]
RUN dotnet restore "./AutomaticComentaryService/AutomaticComentaryService.csproj"
COPY . .
WORKDIR "/src/AutomaticComentaryService"
RUN dotnet build "./AutomaticComentaryService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# --------------------------
# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AutomaticComentaryService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# --------------------------
# Final runtime image (inherits Python+venv setup from 'base')
FROM base AS final
WORKDIR /app

# Copy published .NET app
COPY --from=publish /app/publish .

# Copy the Python script required by your application
COPY Scripts/generate_wav.py Scripts/generate_wav.py

# Start the .NET service
ENTRYPOINT ["dotnet", "AutomaticComentaryService.dll"]