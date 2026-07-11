# syntax=docker/dockerfile:1
# Single-image deploy: the .NET API serves the built React SPA from wwwroot,
# so the whole app runs as one Render web service (same origin, no CORS).

# 1) Build the React SPA. Empty API base URL => same-origin /api calls.
FROM node:22-alpine AS frontend
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
ENV VITE_API_URL=""
RUN npm run build

# 2) Restore & publish the .NET 8 API.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY global.json ./
COPY backend/Directory.Build.props ./backend/
COPY backend/src/ ./backend/src/
WORKDIR /src/backend
RUN dotnet restore src/Quorid.Api/Quorid.Api.csproj
RUN dotnet publish src/Quorid.Api/Quorid.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) Runtime: API + bundled SPA.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /app/frontend/dist ./wwwroot
# Render overrides this via PORT; kept for plain `docker run`.
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "Quorid.Api.dll"]
