# ==============================================================================
# Azure Infrastructure Provisioning Script for Connect (Sprint 7.5)
# Target OS: Windows (PowerShell)
# Subscription ID: 787de81a-4d56-4048-b30a-2414c153e3e1
# Preferred Region: centralindia (Central India - low latency to Chennai)
# ==============================================================================

param(
    [string]$SubscriptionId = "787de81a-4d56-4048-b30a-2414c153e3e1",
    [string]$ResourceGroupName = "connect-rg",
    [string]$Location = "centralindia",
    [string]$SqlAdminUser = "connectadmin",
    [string]$SqlAdminPassword = "",
    [string]$VmAdminUser = "azureuser",
    [string]$VmAdminPassword = ""
)

$ErrorActionPreference = "Stop"

# Locate Azure CLI executable / python module on Windows if not in PATH
$AzCmd = "az"
$AzArgs = @()
$CliPython = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\python.exe"
if (Test-Path $CliPython) {
    $AzCmd = $CliPython
    $AzArgs = @("-m", "azure.cli")
} else {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        $CliPaths = @(
            "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
        )
        foreach ($path in $CliPaths) {
            if (Test-Path $path) {
                $AzCmd = $path
                break
            }
        }
    }
}

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Connect Azure Infrastructure Provisioning Script" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# Prompt for passwords if not supplied as parameters
if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
    $SqlAdminPassword = Read-Host -Prompt "Enter password for Azure SQL Admin ($SqlAdminUser)" -AsSecureString | ConvertFrom-SecureString -AsPlainText
}
if ([string]::IsNullOrWhiteSpace($VmAdminPassword)) {
    $VmAdminPassword = Read-Host -Prompt "Enter password for TURN Server VM Admin ($VmAdminUser)" -AsSecureString | ConvertFrom-SecureString -AsPlainText
}

# Auto-detect local dev machine public IP for SQL firewall and SSH restriction
$MyIp = $null
try {
    $MyIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    Write-Host "Detected local dev machine public IP: $MyIp" -ForegroundColor Gray
} catch {
    Write-Warning "Could not auto-detect public IP via ipify.org."
}

# 1. Set Subscription
Write-Host "`n[1/8] Setting active Azure subscription..." -ForegroundColor Yellow
& $AzCmd $AzArgs account set --subscription $SubscriptionId
& $AzCmd $AzArgs account show --query "{Subscription:name, ID:id, Tenant:tenantId}" -o table

# 2. Register Required Resource Providers
Write-Host "`n[2/8] Registering Azure Resource Providers..." -ForegroundColor Yellow
$Providers = @("Microsoft.Web", "Microsoft.Sql", "Microsoft.Compute", "Microsoft.Network")
foreach ($provider in $Providers) {
    Write-Host "Registering $provider..." -ForegroundColor Gray
    & $AzCmd $AzArgs provider register --namespace $provider --wait
}

# 3. Create Resource Group
Write-Host "`n[3/8] Creating Resource Group: $ResourceGroupName in $Location..." -ForegroundColor Yellow
& $AzCmd $AzArgs group create --name $ResourceGroupName --location $Location -o table

# 4. Generate/Detect Resource Names
$ExistingSqlServer = (& $AzCmd $AzArgs sql server list --resource-group $ResourceGroupName --query "[0].name" -o tsv)
if ($ExistingSqlServer) {
    $SqlServerName = $ExistingSqlServer
    $UniqueSuffix = $SqlServerName.Replace("connect-sql-", "")
} else {
    $UniqueSuffix = (Get-Random -Minimum 1000 -Maximum 9999).ToString()
    $SqlServerName = "connect-sql-$UniqueSuffix"
}
$SqlDbName = "connect-db"
$AppPlanName = "connect-app-plan"

$ExistingWebApp = (& $AzCmd $AzArgs webapp list --resource-group $ResourceGroupName --query "[0].name" -o tsv)
if ($ExistingWebApp) {
    $WebAppName = $ExistingWebApp
} else {
    $WebAppName = "connect-api-$UniqueSuffix"
}

$StaticWebAppName = "connect-web-$UniqueSuffix"
$VmName = "connect-turn-vm"

Write-Host "Resource Names:" -ForegroundColor Green
Write-Host " - SQL Server:       $SqlServerName"
Write-Host " - App Service API:  $WebAppName"
Write-Host " - Static Web App:   $StaticWebAppName"
Write-Host " - TURN Server VM:   $VmName"

# 5. Provision Azure SQL Database (Free Tier / Serverless GP_S_Gen5_1 with Free Limit)
Write-Host "`n[4/8] Creating Azure SQL Server and Database..." -ForegroundColor Yellow
$sqlServerExists = (& $AzCmd $AzArgs sql server list --resource-group $ResourceGroupName --query "[?name=='$SqlServerName'].name" -o tsv)
if (-not $sqlServerExists) {
    & $AzCmd $AzArgs sql server create `
        --name $SqlServerName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --admin-user $SqlAdminUser `
        --admin-password $SqlAdminPassword -o table

    Write-Host "Allowing Azure App Service and local client access to Azure SQL..." -ForegroundColor Gray
    & $AzCmd $AzArgs sql server firewall-rule create `
        --resource-group $ResourceGroupName `
        --server $SqlServerName `
        --name "AllowAzureServices" `
        --start-ip-address 0.0.0.0 `
        --end-ip-address 0.0.0.0 -o table

    if ($MyIp) {
        Write-Host "Adding local dev machine IP ($MyIp) to Azure SQL firewall..." -ForegroundColor Gray
        & $AzCmd $AzArgs sql server firewall-rule create `
            --resource-group $ResourceGroupName `
            --server $SqlServerName `
            --name "AllowLocalDev" `
            --start-ip-address $MyIp `
            --end-ip-address $MyIp -o table
    }

    Write-Host "Creating Azure SQL Database (Free Tier offer enabled)..." -ForegroundColor Gray
    & $AzCmd $AzArgs sql db create `
        --resource-group $ResourceGroupName `
        --server $SqlServerName `
        --name $SqlDbName `
        --edition GeneralPurpose `
        --family Gen5 `
        --capacity 1 `
        --compute-model Serverless `
        --use-free-limit true `
        --free-limit-exhaustion-behavior AutoPause -o table
} else {
    Write-Host "SQL Server $SqlServerName already exists. Skipping creation." -ForegroundColor Green
}

# 6. Provision Azure App Service (Linux, F1 Free Tier)
Write-Host "`n[5/8] Creating App Service Plan (Linux F1 Free) and Web App..." -ForegroundColor Yellow
$planExists = (& $AzCmd $AzArgs appservice plan list --resource-group $ResourceGroupName --query "[?name=='$AppPlanName'].name" -o tsv)
if (-not $planExists) {
    & $AzCmd $AzArgs appservice plan create `
        --name $AppPlanName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --is-linux `
        --sku F1 -o table
} else {
    Write-Host "App Service Plan $AppPlanName already exists. Skipping." -ForegroundColor Green
}

$webAppExists = (& $AzCmd $AzArgs webapp list --resource-group $ResourceGroupName --query "[?name=='$WebAppName'].name" -o tsv)
if (-not $webAppExists) {
    & $AzCmd $AzArgs webapp create `
        --name $WebAppName `
        --resource-group $ResourceGroupName `
        --plan $AppPlanName `
        --runtime "DOTNETCORE|8.0" -o table

    Write-Host "Enabling WebSockets on App Service for SignalR..." -ForegroundColor Gray
    & $AzCmd $AzArgs webapp config set `
        --resource-group $ResourceGroupName `
        --name $WebAppName `
        --web-sockets-enabled true -o table
} else {
    Write-Host "Web App $WebAppName already exists. Skipping creation." -ForegroundColor Green
}

# 7. Provision Azure Static Web App (Free Tier in East Asia)
Write-Host "`n[6/8] Creating Azure Static Web App (Free Tier in East Asia)..." -ForegroundColor Yellow
$staticExists = (& $AzCmd $AzArgs staticwebapp list --resource-group $ResourceGroupName --query "[?name=='$StaticWebAppName'].name" -o tsv)
if (-not $staticExists) {
    & $AzCmd $AzArgs staticwebapp create `
        --name $StaticWebAppName `
        --resource-group $ResourceGroupName `
        --location "eastasia" `
        --sku Free -o table
} else {
    Write-Host "Static Web App $StaticWebAppName already exists. Skipping creation." -ForegroundColor Green
}

# 8. Provision Coturn TURN Server VM (Standard_F2as_v6, Ubuntu 22.04 LTS)
Write-Host "`n[7/8] Provisioning Azure VM for Coturn TURN Server..." -ForegroundColor Yellow
$NsgName = "$VmName-nsg"
$vmExists = (& $AzCmd $AzArgs vm list --resource-group $ResourceGroupName --query "[?name=='$VmName'].name" -o tsv)

if (-not $vmExists) {
    Write-Host "Cleaning up any orphaned network resources before VM creation..." -ForegroundColor Gray
    & $AzCmd $AzArgs network nic delete --resource-group $ResourceGroupName --name "$VmName`VMNic" 2>$null
    & $AzCmd $AzArgs network public-ip delete --resource-group $ResourceGroupName --name "$VmName`PublicIP" 2>$null

    & $AzCmd $AzArgs vm create `
        --resource-group $ResourceGroupName `
        --name $VmName `
        --image "Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest" `
        --size "Standard_F2as_v6" `
        --admin-username $VmAdminUser `
        --admin-password $VmAdminPassword `
        --location $Location `
        --nsg $NsgName `
        --public-ip-sku Standard -o table

    Write-Host "Configuring Network Security Group (NSG) rules for Coturn TURN Server..." -ForegroundColor Gray
    if ($MyIp) {
        Write-Host "Restricting SSH (port 22) access on TURN VM to dev IP: $MyIp (Priority 900)" -ForegroundColor Gray
        & $AzCmd $AzArgs network nsg rule create `
            --resource-group $ResourceGroupName `
            --nsg-name $NsgName `
            --name AllowSSH `
            --priority 900 `
            --destination-port-ranges 22 `
            --protocol Tcp `
            --source-address-prefixes $MyIp `
            --access Allow -o table
    } else {
        Write-Warning "Local dev IP was not detected; creating AllowSSH rule without source IP restriction."
        & $AzCmd $AzArgs network nsg rule create `
            --resource-group $ResourceGroupName `
            --nsg-name $NsgName `
            --name AllowSSH `
            --priority 900 `
            --destination-port-ranges 22 `
            --protocol Tcp `
            --access Allow -o table
    }

    # Port 3478 - STUN/TURN TCP/UDP (Open to any client)
    & $AzCmd $AzArgs network nsg rule create --resource-group $ResourceGroupName --nsg-name $NsgName --name AllowCoturn3478TCP --priority 1010 --destination-port-ranges 3478 --protocol Tcp --access Allow -o table
    & $AzCmd $AzArgs network nsg rule create --resource-group $ResourceGroupName --nsg-name $NsgName --name AllowCoturn3478UDP --priority 1011 --destination-port-ranges 3478 --protocol Udp --access Allow -o table
    # Port 5349 - TURNS TCP/UDP (Open to any client)
    & $AzCmd $AzArgs network nsg rule create --resource-group $ResourceGroupName --nsg-name $NsgName --name AllowCoturn5349TCP --priority 1020 --destination-port-ranges 5349 --protocol Tcp --access Allow -o table
    & $AzCmd $AzArgs network nsg rule create --resource-group $ResourceGroupName --nsg-name $NsgName --name AllowCoturn5349UDP --priority 1021 --destination-port-ranges 5349 --protocol Udp --access Allow -o table
    # Ports 49152-65535 - Coturn UDP Media Relay (Open to any client)
    & $AzCmd $AzArgs network nsg rule create --resource-group $ResourceGroupName --nsg-name $NsgName --name AllowCoturnRelayUDP --priority 1030 --destination-port-ranges 49152-65535 --protocol Udp --access Allow -o table
} else {
    Write-Host "TURN Server VM $VmName already exists. Skipping creation." -ForegroundColor Green
}

# 9. Output Summary & Connection Information
Write-Host "`n[8/8] Provisioning Complete! Summary of Created Resources:" -ForegroundColor Green
$SqlFqdn = "$SqlServerName.database.windows.net"
$ConnectionString = "Server=tcp:$SqlFqdn,1433;Initial Catalog=$SqlDbName;Persist Security Info=False;User ID=$SqlAdminUser;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$WebAppUrl = "https://$WebAppName.azurewebsites.net"
$VmPublicIp = (& $AzCmd $AzArgs vm list-ip-addresses --resource-group $ResourceGroupName --name $VmName --query "[0].virtualMachine.network.publicIpAddresses[0].ipAddress" -o tsv)

Write-Host "-----------------------------------------------------" -ForegroundColor Cyan
Write-Host " Resource Group:     $ResourceGroupName ($Location)"
Write-Host " Azure SQL Server:   $SqlFqdn"
Write-Host " Azure SQL DB:       $SqlDbName"
Write-Host " App Service API:    $WebAppUrl"
Write-Host " Static Web App:     $StaticWebAppName"
Write-Host " Coturn VM IP:       $VmPublicIp"
Write-Host "-----------------------------------------------------" -ForegroundColor Cyan

# Save summary variables to local file for reference
$ConnectionStringForEnv = "Server=tcp:$SqlFqdn,1433;Initial Catalog=$SqlDbName;Persist Security Info=False;User ID=$SqlAdminUser;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$SummaryContent = @"
AZURE_SUBSCRIPTION_ID=$SubscriptionId
AZURE_RESOURCE_GROUP=$ResourceGroupName
AZURE_LOCATION=$Location
AZURE_SQL_SERVER=$SqlFqdn
AZURE_SQL_DATABASE=$SqlDbName
AZURE_SQL_USER=$SqlAdminUser
AZURE_SQL_PASSWORD=$SqlAdminPassword
AZURE_SQL_CONNECTION_STRING=$ConnectionStringForEnv
AZURE_APP_SERVICE_URL=$WebAppUrl
AZURE_STATIC_WEB_APP_NAME=$StaticWebAppName
AZURE_TURN_VM_IP=$VmPublicIp
AZURE_TURN_VM_ADMIN=$VmAdminUser
AZURE_TURN_VM_PASSWORD=$VmAdminPassword
"@

$SummaryContent | Out-File -FilePath "$PSScriptRoot\azure-deployment-info.env" -Encoding utf8
Write-Host "Saved deployment metadata to infra/azure-deployment-info.env" -ForegroundColor Green
