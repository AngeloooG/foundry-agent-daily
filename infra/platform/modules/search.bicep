// Azure AI Search (Basic or higher: the Free tier has no SLA, no private endpoints and no shared private
// links, and production must not depend on its outbound managed-identity behavior).
//
// Private networking needs S1 or higher (created after 2024-04-03): Basic does not support private
// connections from an indexer with a skillset, nor shared private links to a Foundry resource
// (https://learn.microsoft.com/azure/search/search-limits-quotas-capacity#shared-private-link-resource-limits).
// With shared private links the indexer must run with "executionEnvironment": "private" (data-plane
// indexer definition, not ARM).

param name string
param location string
param tags object

@allowed([
  'basic'
  'standard'
  'standard2'
  'standard3'
])
param sku string

@minValue(1)
param replicaCount int

@minValue(1)
param partitionCount int

@allowed([
  'free'
  'standard'
])
param semanticSearch string

@description('Disable API keys. Only after the consumer inventory (web app/Foundry, Copilot Studio, Power Automate) shows nobody uses keys.')
param disableLocalAuth bool

param enablePrivateNetworking bool

@description('Keep the public endpoint enabled even with private networking (Copilot Studio migration window).')
param keepPublicEndpoint bool = false

@description('Shared private link to the SQL server (indexer -> SQL over Private Link). Empty = none.')
param sqlServerId string = ''

@description('Shared private link to the Foundry/AI Services account (embedding skill and vectorizer). Empty = none.')
param foundryAccountId string = ''

param logAnalyticsWorkspaceId string

resource search 'Microsoft.Search/searchServices@2025-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: 'Default'
    semanticSearch: semanticSearch
    disableLocalAuth: disableLocalAuth
    // Entra ID and keys while local auth stays enabled; 401 with a bearer challenge for Entra clients.
    authOptions: disableLocalAuth ? null : {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
    publicNetworkAccess: enablePrivateNetworking && !keepPublicEndpoint ? 'disabled' : 'enabled'
  }
}

// Shared private links are created one after the other; each one must be approved on the target
// resource (private endpoint connection) before the indexer can use it.
resource splSql 'Microsoft.Search/searchServices/sharedPrivateLinkResources@2025-05-01' = if (enablePrivateNetworking && !empty(sqlServerId)) {
  parent: search
  name: 'spl-sql'
  properties: {
    privateLinkResourceId: sqlServerId
    groupId: 'sqlServer'
    requestMessage: 'Azure AI Search indexer -> Azure SQL (Daily)'
  }
}

resource splFoundry 'Microsoft.Search/searchServices/sharedPrivateLinkResources@2025-05-01' = if (enablePrivateNetworking && !empty(foundryAccountId)) {
  parent: search
  name: 'spl-foundry'
  properties: {
    privateLinkResourceId: foundryAccountId
    // "openai_account": embedding skill and vectorizer reach the Foundry resource through its Azure OpenAI
    // endpoint (documented group id; the az CLI may reject it, ARM/REST accepts it with 2025-05-01).
    groupId: 'openai_account'
    requestMessage: 'Azure AI Search embeddings/vectorizer -> Azure OpenAI (Daily)'
  }
  dependsOn: [
    splSql
  ]
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: search
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output id string = search.id
output name string = search.name
output endpoint string = 'https://${name}.search.windows.net'
output principalId string = search.identity.principalId
