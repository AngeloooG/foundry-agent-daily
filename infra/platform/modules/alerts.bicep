// Alerts: indexer failures (from Search diagnostic logs in Log Analytics).

param namePrefix string
param location string
param tags object
param logAnalyticsWorkspaceId string

@description('Email for alert notifications (empty = alert rule without action group)')
param alertEmail string = ''

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmail)) {
  name: 'ag-${namePrefix}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: take(replace(namePrefix, '-', ''), 12)
    enabled: true
    emailReceivers: [
      {
        name: 'team'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// Validate the query in dev: Search writes OperationLogs to AzureDiagnostics (resource-specific
// tables are not used by default).
resource indexerFailures 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-${namePrefix}-indexer-failures'
  location: location
  tags: tags
  properties: {
    displayName: 'Daily: indexer execution failures'
    description: 'Azure AI Search indexer operations that did not succeed in the last hour.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: 'AzureDiagnostics | where ResourceProvider == "MICROSOFT.SEARCH" | where OperationName startswith "Indexers." | where toint(resultSignature_d) >= 400 or ResultType != "Success"'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: empty(alertEmail) ? [] : [
        actionGroup.id
      ]
    }
  }
}
