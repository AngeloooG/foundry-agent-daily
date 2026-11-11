// Log Analytics + workspace-based Application Insights.

@description('Log Analytics workspace name')
param logAnalyticsName string

@description('Application Insights name')
param appInsightsName string

param location string
param tags object

@minValue(30)
@maxValue(730)
param retentionInDays int

@description('Daily ingestion cap in GB (-1 = no cap). Protects against runaway cost.')
param dailyQuotaGb int

@description('Disable local (instrumentation key) authentication. Requires every producer to use Entra ID (Monitoring Metrics Publisher). Keep false until the producer inventory is done.')
param disableLocalAuth bool

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    DisableLocalAuth: disableLocalAuth
  }
}

output workspaceId string = workspace.id
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
