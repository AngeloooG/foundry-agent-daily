// Azure SQL logical server (Microsoft Entra-only authentication) and the knowledge database.
// No "Allow Azure services" (0.0.0.0) rule: access is via private endpoint / shared private link,
// or explicit firewall rules when public access is used in dev.

param serverName string
param databaseName string
param location string
param tags object

@description('Display name of the Entra group that administers the server')
param entraAdminLogin string

@description('Object id of the Entra admin group (no SQL logins / passwords)')
param entraAdminObjectId string

param enablePrivateNetworking bool

@description('Database SKU, e.g. { name: "GP_S_Gen5", tier: "GeneralPurpose", family: "Gen5", capacity: 2 }')
param databaseSku object

@description('Extra database properties (serverless: autoPauseDelay, minCapacity; free offer: useFreeLimit, freeLimitExhaustionBehavior)')
param databaseProperties object = {}

@description('Explicit public firewall rules for dev only: [{ name, startIpAddress, endIpAddress }]')
param firewallRules array = []

param logAnalyticsWorkspaceId string

resource server 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: enablePrivateNetworking ? 'Disabled' : 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      principalType: 'Group'
      login: entraAdminLogin
      sid: entraAdminObjectId
      tenantId: tenant().tenantId
    }
  }
}

resource rules 'Microsoft.Sql/servers/firewallRules@2023-08-01' = [for rule in (enablePrivateNetworking ? [] : firewallRules): {
  parent: server
  name: rule.name
  properties: {
    startIpAddress: rule.startIpAddress
    endIpAddress: rule.endIpAddress
  }
}]

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: databaseSku
  properties: union({
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }, databaseProperties)
}

// Auditing to Log Analytics (server-level, sent through the master database diagnostic setting).
resource auditing 'Microsoft.Sql/servers/auditingSettings@2023-08-01' = {
  parent: server
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

resource master 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  parent: server
  name: 'master'
}

resource auditDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'sql-audit-to-log-analytics'
  scope: master
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
    ]
  }
  dependsOn: [
    auditing
  ]
}

output serverId string = server.id
output serverName string = server.name
output databaseName string = database.name
