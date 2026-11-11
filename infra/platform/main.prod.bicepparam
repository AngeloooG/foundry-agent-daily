// Production. No secrets here: the SQL admin group comes from environment variables.
using './main.bicep'

param environmentName = 'prod'
param location = 'centralus'
param tags = {
  owner: 'consein'
  costCenter: 'daily'
}

// Private endpoints, private DNS and shared private links; public access disabled.
// Copilot Studio reaching a private Search endpoint is NOT assumed: it needs Power Platform VNet
// support (managed environment, VNets in eastus + westus peered to centralus). Until that is validated
// Search keeps its public endpoint (Entra ID / query key); see docs/hardening/red-privada.md.
param enablePrivateNetworking = true
param keepSearchPublic = true
param enableAgentVnetInjection = true

// Keys stay enabled until the consumer inventory proves no one uses them (acciones-azure.md).
// Switch to true only after A1 (Foundry -> Search via Entra) and the Copilot Studio migration.
param disableLocalAuth = false

// Standard (S1) with 2 replicas: read SLA. S1 is also the minimum for private networking with this
// skillset (Basic has no shared private link to a Foundry resource nor private indexers with skillsets).
param searchSku = 'standard'
param searchReplicaCount = 2
param searchPartitionCount = 1
param semanticSearch = 'standard'

param sqlEntraAdminLogin = readEnvironmentVariable('DAILY_SQL_ADMIN_GROUP_NAME', 'sg-daily-sql-admins')
param sqlEntraAdminObjectId = readEnvironmentVariable('DAILY_SQL_ADMIN_GROUP_OBJECT_ID', '00000000-0000-0000-0000-000000000000')

// Dedicated provisioned database (no auto-pause: no cold starts for the indexer or ingestion).
param sqlDatabaseSku = {
  name: 'GP_Gen5'
  tier: 'GeneralPurpose'
  family: 'Gen5'
  capacity: 2
}
param sqlDatabaseProperties = {}
param sqlFirewallRules = []

param modelDeployments = [
  {
    name: 'gpt-5.1'
    modelName: 'gpt-5.1'
    modelVersion: '2025-11-13'
    skuName: 'GlobalStandard'
    capacity: 100
  }
  {
    name: 'text-embedding-3-small'
    modelName: 'text-embedding-3-small'
    modelVersion: '1'
    skuName: 'GlobalStandard'
    capacity: 120
  }
]

param logRetentionInDays = 90
param logDailyQuotaGb = 5
param alertEmail = readEnvironmentVariable('DAILY_ALERT_EMAIL', '')
