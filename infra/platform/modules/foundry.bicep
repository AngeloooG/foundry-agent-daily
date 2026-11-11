// Microsoft Foundry: AI Services account (project management enabled), project, model deployments
// and the project connection to Azure AI Search with Entra ID (no key).

param accountName string
param projectName string
param location string
param tags object

@description('Disable API keys on the account. Only after the consumer inventory shows nobody uses keys (see docs/hardening/acciones-azure.md).')
param disableLocalAuth bool

param enablePrivateNetworking bool

@description('Model deployments: [{ name, modelName, modelVersion, skuName, capacity }]')
param modelDeployments array

@description('Azure AI Search endpoint for the project connection (empty = no connection)')
param searchEndpoint string = ''

@description('Azure AI Search resource id for the project connection')
param searchResourceId string = ''

param logAnalyticsWorkspaceId string

@description('Subnet delegated to Microsoft.App/environments for Agent Service VNet injection (private networking only). Required so the azure_ai_search tool can reach a private Search endpoint. Can only be set when the account is created.')
param agentSubnetId string = ''

var useAgentInjection = enablePrivateNetworking && !empty(agentSubnetId)

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: accountName
    allowProjectManagement: true
    disableLocalAuth: disableLocalAuth
    publicNetworkAccess: enablePrivateNetworking ? 'Disabled' : 'Enabled'
    networkAcls: {
      defaultAction: enablePrivateNetworking ? 'Deny' : 'Allow'
      // Trusted services (Azure AI Search embedding skill/vectorizer) keep working if the shared
      // private link does not carry a call; see docs/hardening/red-privada.md.
      bypass: 'AzureServices'
    }
    // Agent Service runs its tools inside the VNet (BYO VNet). Immutable after creation.
    networkInjections: useAgentInjection ? [
      {
        scenario: 'agent'
        subnetArmId: agentSubnetId
        useMicrosoftManagedNetwork: false
      }
    ] : null
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectName
    description: 'Daily Enhanced'
  }
}

// Deployments must be created one at a time on the same account.
@batchSize(1)
resource deployments 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = [for d in modelDeployments: {
  parent: account
  name: d.name
  sku: {
    name: d.skuName
    capacity: d.capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: d.modelName
      version: d.modelVersion
    }
  }
}]

// Entra ID connection (the project identity needs Search Index Data Reader, granted in rbac.bicep).
resource searchConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01' = if (!empty(searchEndpoint)) {
  parent: project
  name: 'search-daily'
  properties: {
    category: 'CognitiveSearch'
    target: searchEndpoint
    authType: 'AAD'
    isSharedToAll: false
    metadata: {
      ApiType: 'Azure'
      ResourceId: searchResourceId
    }
  }
}

// Project-level capability host: without it, tool calls through the injected VNet fail with
// "name or host unknown" (an account-level capability host alone is not enough).
resource projectCapabilityHost 'Microsoft.CognitiveServices/accounts/projects/capabilityHosts@2025-04-01-preview' = if (useAgentInjection) {
  parent: project
  name: 'caphost-agents'
  properties: {
    // Accepted by the API (official foundry-samples template 11-private-network-basic-vnet) but missing
    // from the Bicep type definition of this preview version.
    #disable-next-line BCP037
    capabilityHostKind: 'Agents'
  }
  dependsOn: [
    searchConnection
  ]
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: account
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'audit'
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

output accountId string = account.id
output accountName string = account.name
output accountPrincipalId string = account.identity.principalId
output projectName string = project.name
output projectPrincipalId string = project.identity.principalId
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'
output openAiEndpoint string = 'https://${accountName}.openai.azure.com'
