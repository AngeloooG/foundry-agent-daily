// Private endpoint for one resource + DNS zone group (A records in the private DNS zones).

param name string
param location string
param tags object

@description('Subnet for private endpoints')
param subnetId string

@description('Resource the endpoint connects to')
param privateLinkServiceId string

@description('Private link group id, e.g. searchService, sqlServer, account, vault')
param groupId string

@description('Private DNS zone resource ids for this group')
param privateDnsZoneIds array

resource endpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: name
        properties: {
          privateLinkServiceId: privateLinkServiceId
          groupIds: [
            groupId
          ]
        }
      }
    ]
  }
}

resource dnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: endpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [for (zoneId, i) in privateDnsZoneIds: {
      name: 'zone-${i}'
      properties: {
        privateDnsZoneId: zoneId
      }
    }]
  }
}
