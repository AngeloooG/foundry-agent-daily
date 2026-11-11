// Development. No secrets here: the SQL admin group comes from environment variables.
using './main.bicep'

param environmentName = 'dev'
param location = 'centralus'
param tags = {
  owner: 'consein'
  costCenter: 'daily'
}

// Public endpoints in dev (cheaper; no VNet). Keys stay enabled: Copilot Studio uses a Search key
// and the Foundry -> Search connection still uses a key until action A1 is done.
param enablePrivateNetworking = false
param disableLocalAuth = false

// Basic: lowest tier with SLA-capable features, private endpoints and shared private links.
param searchSku = 'basic'
param searchReplicaCount = 1
param searchPartitionCount = 1
param semanticSearch = 'free'

param sqlEntraAdminLogin = readEnvironmentVariable('DAILY_SQL_ADMIN_GROUP_NAME', 'sg-daily-sql-admins')
param sqlEntraAdminObjectId = readEnvironmentVariable('DAILY_SQL_ADMIN_GROUP_OBJECT_ID', '00000000-0000-0000-0000-000000000000')

// Serverless with auto-pause; free offer that pauses (never bills) when the monthly allowance is used.
param sqlDatabaseSku = {
  name: 'GP_S_Gen5'
  tier: 'GeneralPurpose'
  family: 'Gen5'
  capacity: 2
}
param sqlDatabaseProperties = {
  autoPauseDelay: 60
  minCapacity: json('0.5')
  useFreeLimit: true
  freeLimitExhaustionBehavior: 'AutoPause'
}
// Explicit IPs only (never 0.0.0.0). Basic Search cannot use a private indexer with a skillset, so in
// dev the indexer reaches SQL publicly: add the Search outbound IP(s) here (see infra/platform/README.md).
param sqlFirewallRules = []

param modelDeployments = [
  {
    name: 'gpt-5.1'
    modelName: 'gpt-5.1'
    modelVersion: '2025-11-13'
    skuName: 'GlobalStandard'
    capacity: 50
  }
  {
    name: 'text-embedding-3-small'
    modelName: 'text-embedding-3-small'
    modelVersion: '1'
    skuName: 'GlobalStandard'
    capacity: 120
  }
]

param logRetentionInDays = 30
param logDailyQuotaGb = 1
param alertEmail = readEnvironmentVariable('DAILY_ALERT_EMAIL', '')
