# Stage 1: Build React Frontend
FROM node:22-alpine AS frontend-builder

# Build arguments for environment variables (required at build time)
ARG ENTRA_SPA_CLIENT_ID
ARG ENTRA_TENANT_ID
ARG ENTRA_BACKEND_CLIENT_ID=""
# Browser Application Insights connection string, Base64 (UTF-8) encoded. Base64 is NOT a secrecy
# measure: the value ends up in the JavaScript bundle on purpose (the browser SDK needs it). It only
# avoids quoting/parsing problems with ';', '/', '=' and 'https://' across PowerShell/Git Bash,
# Azure CLI, ACR Tasks and the build shell. Decoded only inside the `npm run build` RUN below.
ARG APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64=""

WORKDIR /app/frontend

# Copy all frontend files (includes package.json, .npmrc if present, and source)
COPY frontend/ ./

# Install dependencies (respects .npmrc for custom registries if present)
RUN npm ci

# Remove ALL local environment files to prevent localhost config from being used
RUN rm -f .env.local .env.development .env

# Set environment variables for Vite build
ENV NODE_ENV=production
ENV VITE_ENTRA_SPA_CLIENT_ID=$ENTRA_SPA_CLIENT_ID
ENV VITE_ENTRA_TENANT_ID=$ENTRA_TENANT_ID
ENV VITE_ENTRA_BACKEND_CLIENT_ID=$ENTRA_BACKEND_CLIENT_ID
# Don't set VITE_API_URL - will default to "/api" (same origin)

# Build the frontend.
# The App Insights connection string is required: an empty, malformed or non-connection-string value
# fails the build (messages never include the value). It is decoded in this RUN only and handed
# exclusively to the npm/Vite process as VITE_APPLICATIONINSIGHTS_CONNECTION_STRING: no ENV, no file,
# nothing in the final image except the bundle. printf '%s' avoids adding a newline; `base64 -d` is
# the BusyBox applet included in node:*-alpine.
RUN set -eu; \
    b64="$APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64"; \
    if [ -z "$b64" ]; then \
      echo "ERROR: APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64 is required (Base64 of the frontend Application Insights connection string)." >&2; exit 1; \
    fi; \
    case "$b64" in *[!A-Za-z0-9+/=]*) \
      echo "ERROR: APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64 is not valid Base64 (unexpected characters)." >&2; exit 1;; \
    esac; \
    if [ $(( ${#b64} % 4 )) -ne 0 ]; then \
      echo "ERROR: APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64 is not valid Base64 (length)." >&2; exit 1; \
    fi; \
    decoded="$(printf '%s' "$b64" | base64 -d 2>/dev/null)" || { \
      echo "ERROR: APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64 could not be decoded." >&2; exit 1; }; \
    case "$decoded" in *InstrumentationKey=*) ;; *) \
      echo "ERROR: APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64 does not decode to an Application Insights connection string." >&2; exit 1;; \
    esac; \
    VITE_APPLICATIONINSIGHTS_CONNECTION_STRING="$decoded" npm run build

# Stage 2: Build .NET API Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-builder

WORKDIR /app

# Copy solution and project files
COPY backend/WebApp.sln ./
COPY backend/WebApp.Api/WebApp.Api.csproj ./backend/WebApp.Api/
COPY backend/WebApp.ServiceDefaults/WebApp.ServiceDefaults.csproj ./backend/WebApp.ServiceDefaults/

# Restore dependencies
RUN dotnet restore backend/WebApp.Api/WebApp.Api.csproj

# Copy source code
COPY backend/ ./backend/

# Build and publish
RUN dotnet publish backend/WebApp.Api/WebApp.Api.csproj -c Release -o /app/publish

# Stage 3: Runtime - .NET API serving both backend and frontend static files
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

WORKDIR /app

# Copy published .NET API
COPY --from=backend-builder /app/publish ./

# Copy built React frontend into wwwroot (ASP.NET static files directory)
COPY --from=frontend-builder /app/frontend/dist ./wwwroot

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run as non-root user (built-in 'app' user in ASP.NET alpine images)
USER app

# Start the .NET API (which will also serve frontend static files from wwwroot)
ENTRYPOINT ["dotnet", "WebApp.Api.dll"]
