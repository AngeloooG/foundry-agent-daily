#!/usr/bin/env pwsh
# Pre-deploy: Build container (local Docker if available, ACR cloud build as fallback)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/modules/HookLogging.ps1"
Start-HookLog -HookName "predeploy" -EnvironmentName $env:AZURE_ENV_NAME

Write-Host "Pre-Deploy: Building Container Image" -ForegroundColor Cyan

# Get required values — azd injects env vars into hooks, but `azd env get-value` may fail
# if the subprocess can't locate azure.yaml. Fall back to $env: vars.
function Get-AzdValue($name) {
    $val = (azd env get-value $name 2>&1) | Where-Object { $_ -notmatch 'ERROR|WARNING' } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($val)) { $val = [Environment]::GetEnvironmentVariable($name) }
    return $val
}

$clientId = Get-AzdValue 'ENTRA_SPA_CLIENT_ID'
$tenantId = Get-AzdValue 'ENTRA_TENANT_ID'
$backendClientId = Get-AzdValue 'ENTRA_BACKEND_CLIENT_ID'
$appInsightsConnStr = Get-AzdValue 'APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING'
$acrName = Get-AzdValue 'AZURE_CONTAINER_REGISTRY_NAME'
$resourceGroup = Get-AzdValue 'AZURE_RESOURCE_GROUP_NAME'
$containerApp = Get-AzdValue 'AZURE_CONTAINER_APP_NAME'

if (-not $clientId -or -not $tenantId) {
    Write-Host "[ERROR] ENTRA_SPA_CLIENT_ID or ENTRA_TENANT_ID not set" -ForegroundColor Red
    exit 1
}
if (-not $acrName) {
    Write-Host "[ERROR] AZURE_CONTAINER_REGISTRY_NAME not set" -ForegroundColor Red
    exit 1
}
# Browser telemetry is required in deployed images (the Dockerfile fails without it).
if ([string]::IsNullOrWhiteSpace($appInsightsConnStr)) {
    Write-Host "[ERROR] APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING not set" -ForegroundColor Red
    exit 1
}

# Transport the frontend App Insights connection string as Base64 (UTF-8). This is NOT a secrecy
# measure (the value ends up in the JavaScript bundle by design); it only avoids quoting problems
# with ';', '/', '=' and 'https://' across PowerShell, Azure CLI, ACR Tasks and the build shell.
# The Dockerfile decodes it inside the `npm run build` step. Neither value is ever printed.
$appInsightsB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($appInsightsConnStr.Trim()))
Remove-Variable appInsightsConnStr
$appInsightsArgName = 'APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64'

# Remove the Base64 value from any text before printing it (defense in depth for error output).
function Hide-BuildValue([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return $text }
    return $text.Replace($appInsightsB64, '<redacted>')
}

$imageTag = "deploy-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$imageName = "$acrName.azurecr.io/web:$imageTag"

# Check Docker availability
$dockerAvailable = $false
if (Get-Command docker -EA SilentlyContinue) {
    $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -eq 0 -and $dockerVersion) {
        $dockerAvailable = $true
        Write-Host "[OK] Docker v$dockerVersion" -ForegroundColor Green
    }
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $projectRoot

try {
    if ($dockerAvailable) {
        Write-Host "Building with local Docker..." -ForegroundColor Cyan
        $buildArgs = @(
            "--platform", "linux/amd64",
            "--build-arg", "ENTRA_SPA_CLIENT_ID=$clientId",
            "--build-arg", "ENTRA_TENANT_ID=$tenantId"
        )
        if ($backendClientId) { $buildArgs += @("--build-arg", "ENTRA_BACKEND_CLIENT_ID=$backendClientId") }
        # Name only: Docker reads the value from the environment, so it is not on the docker command line.
        $buildArgs += @("--build-arg", $appInsightsArgName)
        $buildArgs += @("-f", "deployment/docker/frontend.Dockerfile", "-t", $imageName, ".")
        [Environment]::SetEnvironmentVariable($appInsightsArgName, $appInsightsB64, 'Process')
        try {
            docker build @buildArgs 2>&1 | ForEach-Object { Hide-BuildValue "$_" } | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Docker build failed" }
        } finally {
            [Environment]::SetEnvironmentVariable($appInsightsArgName, $null, 'Process')
        }
        
        Write-Host "Pushing to ACR..." -ForegroundColor Cyan
        az acr login --name $acrName | Out-Null
        docker push $imageName 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Docker push failed" }
    } else {
        Write-Host "Using ACR cloud build (3-5 min)..." -ForegroundColor Yellow
        $acrBuildArgs = @("--build-arg", "ENTRA_SPA_CLIENT_ID=$clientId", "--build-arg", "ENTRA_TENANT_ID=$tenantId")
        if ($backendClientId) { $acrBuildArgs += @("--build-arg", "ENTRA_BACKEND_CLIENT_ID=$backendClientId") }
        # Secret build arg as operational hygiene ("not surfaced to the ACR team"); not a confidentiality guarantee.
        $acrBuildArgs += @("--secret-build-arg", "$appInsightsArgName=$appInsightsB64")
        $buildOutput = az acr build --registry $acrName --image "web:$imageTag" `
            @acrBuildArgs `
            --file deployment/docker/frontend.Dockerfile . `
            --no-logs --only-show-errors 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build output: $(Hide-BuildValue ($buildOutput | Out-String))" -ForegroundColor Red
            throw "ACR build failed"
        }
        Write-Host "[OK] ACR build completed" -ForegroundColor Green
    }
    Write-Host "[OK] Image built: $imageName" -ForegroundColor Green
    
    # Update Container App (skip if doesn't exist yet - first azd up uses placeholder)
    if ($containerApp -and $resourceGroup) {
        $exists = az containerapp show --name $containerApp --resource-group $resourceGroup --query name -o tsv 2>$null
        if ($exists) {
            Write-Host "Updating Container App..." -ForegroundColor Cyan
            az containerapp update --name $containerApp --resource-group $resourceGroup `
                --image $imageName --output none
            if ($LASTEXITCODE -ne 0) { throw "Container App update failed" }
            Write-Host "[OK] Container App updated" -ForegroundColor Green
        } else {
            Write-Host "[SKIP] Container App not yet provisioned (first run)" -ForegroundColor Yellow
        }
    }
    
    azd env set SERVICE_WEB_IMAGE_NAME $imageName 2>$null
} finally {
    Pop-Location
}

if ($script:HookLogFile) {
    Write-Host "[LOG] Log file: $script:HookLogFile" -ForegroundColor DarkGray
}
Stop-HookLog
