// Daily Enhanced – shared platform (Search, SQL, Foundry, Key Vault, identities, monitoring,
// optional private networking). Not deployed by azd: the web app itself (Container App, ACR,
// Entra app) stays in infra/main.bicep.
//
// Build:    az bicep build --file infra/platform/main.bicep
// What-if:  az deployment sub what-if --location centralus --template-file infra/platform/main.bicep --parameters infra/platform/main.dev.bicepparam
//           (prod: add --parameters enableAgentVnetInjection=false; ARM what-if cannot analyze networkInjections)
// See infra/platform/README.md. Deployment requires approval (security, network, identity, RBAC).

targetScope = 'subscription'

@allowed([
  'dev'
  'prod'
])
param environmentName string

param location string = 'centralus'

@description('Workload short name used in resource names')
param workload string = 'daily'

param tags object = {}

// ---- Security switches ----
@description('Private endpoints, private DNS and shared private links; public network access disabled.')
param enablePrivateNetworking bool

@description('Keep the Search public endpoint enabled while the rest is private. Required until Copilot Studio reaches Search through Power Platform VNet support (docs/hardening/red-privada.md).')
param keepSearchPublic bool = false

@description('Foundry Agent Service VNet injection (agent tools such as azure_ai_search reach private endpoints). Only with private networking; immutable after the account is created. ARM what-if returns InternalServerError when it is true: run what-if with false.')
param enableAgentVnetInjection bool = true

@description('Disable keys on Search, Foundry and Application Insights. Precondition: inventory proves no consumer uses keys (Copilot Studio uses a Search key today).')
param disableLocalAuth bool

// ---- Search ----
param searchSku string
param searchReplicaCount int
param searchPartitionCount int
param semanticSearch string

// ---- SQL ----
param sqlEntraAdminLogin string
param sqlEntraAdminObjectId string
param sqlDatabaseSku object
param sqlDatabaseProperties object = {}
param sqlFirewallRules array = []

// ---- Foundry ----
param modelDeployments array

// ---- Monitoring ----
param logRetentionInDays int
param logDailyQuotaGb int
param alertEmail string = ''

// ---- Network ----
param vnetAddressPrefix string = '10.40.0.0/16'
param containerAppsSubnetPrefix string = '10.40.0.0/23'
param privateEndpointsSubnetPrefix string = '10.40.2.0/24'
param agentSubnetPrefix string = '10.40.3.0/24'

// Names: <abbr>-<workload>-<env>[-<suffix>] (globally unique services get a short stable suffix).
var suffix = take(uniqueString(subscription().id, workload, environmentName), 5)
var rgName = 'rg-${workload}-${environmentName}-${location}'
var allTags = union(tags, {
  workload: workload
  environment: environmentName
})
var names = {
  logAnalytics: 'log-${workload}-${environmentName}'
  appInsights: 'appi-${workload}-${environmentName}'
  vnet: 'vnet-${workload}-${environmentName}'
  webIdentity: 'id-web-${workload}-${environmentName}'
  keyVault: 'kv-${workload}-${environmentName}-${suffix}'
  foundryAccount: 'ais-${workload}-${environmentName}-${suffix}'
  foundryProject: 'proj-${workload}-${environmentName}'
  search: 'srch-${workload}-${environmentName}-${suffix}'
  sqlServer: 'sql-${workload}-${environmentName}-${suffix}'
  sqlDatabase: 'sqldb-${workload}-knowledge'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: rgName
  location: location
  tags: allTags
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    logAnalyticsName: names.logAnalytics
    appInsightsName: names.appInsights
    location: location
    tags: allTags
    retentionInDays: logRetentionInDays
    dailyQuotaGb: logDailyQuotaGb
    disableLocalAuth: disableLocalAuth
  }
}

module network 'modules/network.bicep' = if (enablePrivateNetworking) {
  name: 'network'
  scope: rg
  params: {
    vnetName: names.vnet
    location: location
    tags: allTags
    addressPrefix: vnetAddressPrefix
    containerAppsSubnetPrefix: containerAppsSubnetPrefix
    privateEndpointsSubnetPrefix: privateEndpointsSubnetPrefix
    agentSubnetPrefix: agentSubnetPrefix
  }
}

module webIdentity 'modules/identity.bicep' = {
  name: 'web-identity'
  scope: rg
  params: {
    name: names.webIdentity
    location: location
    tags: allTags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    name: names.keyVault
    location: location
    tags: allTags
    enablePrivateNetworking: enablePrivateNetworking
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    serverName: names.sqlServer
    databaseName: names.sqlDatabase
    location: location
    tags: allTags
    entraAdminLogin: sqlEntraAdminLogin
    entraAdminObjectId: sqlEntraAdminObjectId
    enablePrivateNetworking: enablePrivateNetworking
    databaseSku: sqlDatabaseSku
    databaseProperties: sqlDatabaseProperties
    firewallRules: sqlFirewallRules
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

// Search first (without shared private links) so Foundry can reference it in the project connection.
module search 'modules/search.bicep' = {
  name: 'search'
  scope: rg
  params: {
    name: names.search
    location: location
    tags: allTags
    sku: searchSku
    replicaCount: searchReplicaCount
    partitionCount: searchPartitionCount
    semanticSearch: semanticSearch
    disableLocalAuth: disableLocalAuth
    enablePrivateNetworking: enablePrivateNetworking
    keepPublicEndpoint: keepSearchPublic
    sqlServerId: sql.outputs.serverId
    foundryAccountId: resourceId(subscription().subscriptionId, rgName, 'Microsoft.CognitiveServices/accounts', names.foundryAccount)
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
  dependsOn: [
    foundry
  ]
}

module foundry 'modules/foundry.bicep' = {
  name: 'foundry'
  scope: rg
  params: {
    accountName: names.foundryAccount
    projectName: names.foundryProject
    location: location
    tags: allTags
    disableLocalAuth: disableLocalAuth
    enablePrivateNetworking: enablePrivateNetworking
    modelDeployments: modelDeployments
    searchEndpoint: 'https://${names.search}.search.windows.net'
    searchResourceId: resourceId(subscription().subscriptionId, rgName, 'Microsoft.Search/searchServices', names.search)
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    agentSubnetId: enablePrivateNetworking && enableAgentVnetInjection ? network!.outputs.agentSubnetId : ''
  }
}

module rbac 'modules/rbac.bicep' = {
  name: 'rbac'
  scope: rg
  params: {
    webIdentityPrincipalId: webIdentity.outputs.principalId
    foundryProjectPrincipalId: foundry.outputs.projectPrincipalId
    searchPrincipalId: search.outputs.principalId
    foundryAccountName: foundry.outputs.accountName
    foundryProjectName: foundry.outputs.projectName
    searchServiceName: search.outputs.name
    appInsightsName: monitoring.outputs.appInsightsName
    sqlServerName: sql.outputs.serverName
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  scope: rg
  params: {
    namePrefix: '${workload}-${environmentName}'
    location: location
    tags: allTags
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    alertEmail: alertEmail
  }
}

// ---- Private endpoints (only with private networking) ----

module peSearch 'modules/private-endpoint.bicep' = if (enablePrivateNetworking) {
  name: 'pe-search'
  scope: rg
  params: {
    name: 'pe-${names.search}'
    location: location
    tags: allTags
    subnetId: network!.outputs.privateEndpointsSubnetId
    privateLinkServiceId: search.outputs.id
    groupId: 'searchService'
    privateDnsZoneIds: [
      network!.outputs.privateDnsZoneIds['privatelink.search.windows.net']
    ]
  }
}

module peSql 'modules/private-endpoint.bicep' = if (enablePrivateNetworking) {
  name: 'pe-sql'
  scope: rg
  params: {
    name: 'pe-${names.sqlServer}'
    location: location
    tags: allTags
    subnetId: network!.outputs.privateEndpointsSubnetId
    privateLinkServiceId: sql.outputs.serverId
    groupId: 'sqlServer'
    privateDnsZoneIds: [
      #disable-next-line no-hardcoded-env-urls
      network!.outputs.privateDnsZoneIds['privatelink.database.windows.net']
    ]
  }
}

module peFoundry 'modules/private-endpoint.bicep' = if (enablePrivateNetworking) {
  name: 'pe-foundry'
  scope: rg
  params: {
    name: 'pe-${names.foundryAccount}'
    location: location
    tags: allTags
    subnetId: network!.outputs.privateEndpointsSubnetId
    privateLinkServiceId: foundry.outputs.accountId
    groupId: 'account'
    privateDnsZoneIds: [
      network!.outputs.privateDnsZoneIds['privatelink.cognitiveservices.azure.com']
      network!.outputs.privateDnsZoneIds['privatelink.openai.azure.com']
      network!.outputs.privateDnsZoneIds['privatelink.services.ai.azure.com']
    ]
  }
}

module peKeyVault 'modules/private-endpoint.bicep' = if (enablePrivateNetworking) {
  name: 'pe-keyvault'
  scope: rg
  params: {
    name: 'pe-${names.keyVault}'
    location: location
    tags: allTags
    subnetId: network!.outputs.privateEndpointsSubnetId
    privateLinkServiceId: keyVault.outputs.id
    groupId: 'vault'
    privateDnsZoneIds: [
      network!.outputs.privateDnsZoneIds['privatelink.vaultcore.azure.net']
    ]
  }
}

// Outputs (no secrets): identifiers the web app deployment needs.
output resourceGroupName string = rg.name
output foundryProjectEndpoint string = foundry.outputs.projectEndpoint
output searchEndpoint string = search.outputs.endpoint
output webIdentityClientId string = webIdentity.outputs.clientId
output webIdentityResourceId string = webIdentity.outputs.id
output containerAppsSubnetId string = enablePrivateNetworking ? network!.outputs.containerAppsSubnetId : ''
