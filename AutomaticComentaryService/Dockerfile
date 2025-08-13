# -------------------------------
# Stage 0: Base image with CUDA, .NET Runtime, and Python TTS support
# -------------------------------
FROM pytorch/pytorch:2.2.2-cuda12.1-cudnn8-runtime AS base

# 1. Install .NET 8 ASP.NET Core Runtime
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      wget apt-transport-https gnupg ca-certificates && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends aspnetcore-runtime-8.0 && \
    rm -rf /var/lib/apt/lists/* packages-microsoft-prod.deb

# 2. Install Python 3, venv, and dependencies for Coqui TTS
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      python3 python3-venv python3-pip && \
    rm -rf /var/lib/apt/lists/*

# 3. Create and activate a virtual environment, install TTS, torch, soundfile
RUN python3 -m venv /opt/venv && \
    /opt/venv/bin/pip install --upgrade pip && \
    /opt/venv/bin/pip install TTS torch soundfile

# Make venv Python the default
ENV PATH="/opt/venv/bin:${PATH}" \
    PYTHONUNBUFFERED=1

WORKDIR /app
EXPOSE 8080 8081

# -------------------------------
# Stage 1: Build .NET 8 application
# -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AutomaticComentaryService/AutomaticComentaryService.csproj", "AutomaticComentaryService/"]
RUN dotnet restore "AutomaticComentaryService/AutomaticComentaryService.csproj"
COPY . .
WORKDIR "/src/AutomaticComentaryService"
RUN dotnet publish "./AutomaticComentaryService.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# -------------------------------
# Stage 2: Final image
# -------------------------------
FROM base AS final
WORKDIR /app

# Copy .NET published app
COPY --from=build /app/publish ./

# Copy Python TTS script
COPY Scripts/generate_wav.py Scripts/generate_wav.py

COPY ./RagDatabase /app/RagDatabase

RUN dotnet tool install --global dotnet-trace
ENV PATH="$PATH:/root/.dotnet/tools"

# Entrypoint: run the .NET service
ENTRYPOINT ["dotnet", "AutomaticComentaryService.dll"]
