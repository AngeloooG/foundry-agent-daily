// Virtual network and private DNS zones for private endpoints (deployed only when private networking is enabled).

param vnetName string
param location string
param tags object

@description('VNet address space, e.g. 10.40.0.0/16')
param addressPrefix string

@description('Subnet for the Container Apps environment (workload profiles, delegated, /23 minimum)')
param containerAppsSubnetPrefix string

@description('Subnet for private endpoints')
param privateEndpointsSubnetPrefix string

@description('Subnet for Foundry Agent Service VNet injection (delegated to Microsoft.App/environments, /27 minimum, dedicated, size cannot change later)')
param agentSubnetPrefix string

@description('Private DNS zones to create and link to the VNet')
param privateDnsZoneNames array = [
  'privatelink.search.windows.net'
  #disable-next-line no-hardcoded-env-urls
  'privatelink.database.windows.net'
  'privatelink.cognitiveservices.azure.com'
  'privatelink.openai.azure.com'
  'privatelink.services.ai.azure.com'
  'privatelink.vaultcore.azure.net'
]

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: 'snet-aca'
        properties: {
          addressPrefix: containerAppsSubnetPrefix
          delegations: [
            {
              name: 'aca'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'snet-pe'
        properties: {
          addressPrefix: privateEndpointsSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        name: 'snet-agent'
        properties: {
          addressPrefix: agentSubnetPrefix
          delegations: [
            {
              name: 'foundry-agent'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
    ]
  }
}

resource zones 'Microsoft.Network/privateDnsZones@2024-06-01' = [for zoneName in privateDnsZoneNames: {
  name: zoneName
  location: 'global'
  tags: tags
}]

resource links 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = [for (zoneName, i) in privateDnsZoneNames: {
  parent: zones[i]
  name: 'link-${vnetName}'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}]

output vnetId string = vnet.id
output containerAppsSubnetId string = vnet.properties.subnets[0].id
output privateEndpointsSubnetId string = vnet.properties.subnets[1].id
output agentSubnetId string = vnet.properties.subnets[2].id
output privateDnsZoneIds object = toObject(privateDnsZoneNames, name => name, name => resourceId('Microsoft.Network/privateDnsZones', name))
