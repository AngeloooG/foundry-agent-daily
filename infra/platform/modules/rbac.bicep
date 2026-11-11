// Least-privilege role assignments between Daily's identities (see docs/hardening/matriz-rbac.md).
// Scoped to the individual resource (never resource group or subscription).

param webIdentityPrincipalId string
param foundryProjectPrincipalId string
param searchPrincipalId string

param foundryAccountName string
param foundryProjectName string
param searchServiceName string
param appInsightsName string
param sqlServerName string

var roles = {
  foundryUser: '53ca6127-db72-4b80-b1b0-d745d6d5456d' // Foundry User (formerly Azure AI User)
  openAiUser: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' // Cognitive Services OpenAI User
  searchIndexDataReader: '1407120a-92aa-4202-b7e9-c0e197c71c8f' // Search Index Data Reader
  monitoringMetricsPublisher: '3913510d-42f4-4e42-8a64-420c390055eb' // Monitoring Metrics Publisher
  reader: 'acdd72a7-3385-48ef-bd42-f606fba81ae7' // Reader
}

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' existing = {
  parent: account
  name: foundryProjectName
}

resource search 'Microsoft.Search/searchServices@2025-05-01' existing = {
  name: searchServiceName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' existing = {
  name: sqlServerName
}

// Web app -> Foundry project: call agents, conversations, responses and files (replaces Azure AI
// Developer + Cognitive Services OpenAI Contributor + Cognitive Services User; validate in dev first).
resource webToProject 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(project.id, webIdentityPrincipalId, roles.foundryUser)
  scope: project
  properties: {
    principalId: webIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.foundryUser)
  }
}

// Web app -> Application Insights: Entra-authenticated telemetry (needed once local auth is disabled).
resource webToAppInsights 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, webIdentityPrincipalId, roles.monitoringMetricsPublisher)
  scope: appInsights
  properties: {
    principalId: webIdentityPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.monitoringMetricsPublisher)
  }
}

// Foundry project -> Search: read index data through the Entra ID connection.
resource projectToSearch 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, foundryProjectPrincipalId, roles.searchIndexDataReader)
  scope: search
  properties: {
    principalId: foundryProjectPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.searchIndexDataReader)
  }
}

// Search -> Foundry account: embeddings for the skillset and the query vectorizer.
resource searchToOpenAi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, searchPrincipalId, roles.openAiUser)
  scope: account
  properties: {
    principalId: searchPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.openAiUser)
  }
}

// Search -> SQL server: Reader (documented requirement for the managed-identity indexer connection with
// ResourceId). Data access itself is granted inside the database: contained user for the search service
// with SELECT on dbo.vw_DailyConocimiento_Search only (T-SQL, not ARM; see infra/platform/README.md).
resource searchToSqlReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sqlServer.id, searchPrincipalId, roles.reader)
  scope: sqlServer
  properties: {
    principalId: searchPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.reader)
  }
}
