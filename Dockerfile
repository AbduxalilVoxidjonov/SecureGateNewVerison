# syntax=docker/dockerfile:1
#
# SecureGate — bitta konteynerda React SPA + ASP.NET Core API + Swagger.
#
# Stage 1 (spa)      : node:22-alpine  — Vite bilan React SPA build
# Stage 2 (build)    : dotnet/sdk:8.0  — restore + publish (SPA natijasi wwwroot'ga)
# Stage 3 (final)    : dotnet/aspnet:8.0-noble — runtime
#
# Nega `-noble` (Ubuntu 24.04) va oddiy `aspnet:8.0` (Debian 12) emas:
# OpenCvSharp native kutubxonasi (libOpenCvSharpExtern.so) GLIBC_2.38 va
# GLIBCXX_3.4.32 talab qiladi. Debian 12 da glibc 2.36 — yuklanmaydi.

# ============================================================
# Stage 1 — SPA build
# ============================================================
FROM node:22-alpine AS spa
WORKDIR /spa

# Layer caching: avval faqat manifest fayllar
COPY securegate.client/package.json securegate.client/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY securegate.client/ ./
RUN npm run build


# ============================================================
# Stage 2 — .NET restore + publish
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ARG BUILD_CONFIGURATION=Release

# Layer caching: avval faqat csproj fayllar (esproj EMAS — Docker'da SPA alohida quriladi)
COPY SecureGate.Domain/SecureGate.Domain.csproj        SecureGate.Domain/
COPY SecureGate.Data/SecureGate.Data.csproj            SecureGate.Data/
COPY SecureGate.Infrastructure/SecureGate.Infrastructure.csproj SecureGate.Infrastructure/
COPY SecureGate.Server/SecureGate.Server.csproj        SecureGate.Server/

# BuildingInDocker=true → securegate.client.esproj ProjectReference va
# Microsoft.AspNetCore.SpaProxy paketi chetlab o'tiladi (JavaScript SDK konteynerda yo'q).
RUN dotnet restore SecureGate.Server/SecureGate.Server.csproj -p:BuildingInDocker=true

# Manba kodi
COPY SecureGate.Domain/         SecureGate.Domain/
COPY SecureGate.Data/           SecureGate.Data/
COPY SecureGate.Infrastructure/ SecureGate.Infrastructure/
COPY SecureGate.Server/         SecureGate.Server/

# SPA build natijasi → wwwroot (publish uni o'zi paketga qo'shadi)
COPY --from=spa /spa/dist/ SecureGate.Server/wwwroot/

RUN dotnet publish SecureGate.Server/SecureGate.Server.csproj \
        -c $BUILD_CONFIGURATION \
        -o /app/publish \
        --no-restore \
        -p:BuildingInDocker=true \
        -p:UseAppHost=false


# ============================================================
# Stage 3 — runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final

# OpenCvSharpExtern uchun zarur system kutubxonalari
# (FFmpeg — RTSP VideoCapture; GTK/cairo — highgui linkage; tesseract/webp/tiff — imgcodecs)
#
# `ffmpeg` — CLI binar, NVR playback (RTSP) oqimini mp4 ga remux qilish uchun
# (HikvisionNvrArchiveService.OpenPlaybackAsync). Yuqoridagi libav* paketlari
# faqat OpenCV linklaydigan kutubxonalar — ular CLI binarni bermaydi.
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        libgomp1 \
        libgtk-3-0t64 \
        libcairo2 \
        libgdk-pixbuf-2.0-0 \
        libglib2.0-0t64 \
        libavcodec60 \
        libavformat60 \
        libavutil58 \
        libswscale7 \
        libtesseract5 \
        libjpeg-turbo8 \
        libpng16-16t64 \
        libtiff6 \
        libwebp7 \
        libwebpdemux2 \
        libwebpmux3 \
        curl \
        ffmpeg \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

# Yuklangan rasmlar uchun katalog (docker-compose'da named volume mount qilinadi)
RUN mkdir -p /app/wwwroot/uploads /app/keys

ENV ASPNETCORE_URLS=http://+:3333 \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 3333

ENTRYPOINT ["dotnet", "SecureGate.Server.dll"]
