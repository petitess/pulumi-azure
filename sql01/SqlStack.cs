using Pulumi;
using System;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using System.Collections.Immutable;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Sql.Inputs;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.ManagedIdentity;

class SqlStack
{
    [Output] public Output<ImmutableDictionary<string, Output<string>>> subnetIds { get; set; } = null!;

    public SqlStack(
        Output<ImmutableDictionary<string, Output<string>>> subnetIds,
        Output<ImmutableDictionary<string, Output<string>>> pdnszIds
    )
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var sqlServers = config.RequireObject<List<SqlServerX>>("sqlServers");


        var resourceGroup = new ResourceGroup("rgSql",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-sql-{env}-01",
            Tags = tags,
        });
        foreach (var s in sqlServers)
        {

            var sql = new Server(s.name, new ServerArgs
            {
                ServerName = s.name,
                ResourceGroupName = resourceGroup.Name,
                Tags = tags,
                PublicNetworkAccess = s.publicNetworkAccess,
                Identity = new ResourceIdentityArgs
                {
                    Type = IdentityType.SystemAssigned
                },
                RestrictOutboundNetworkAccess = s.restrictOutboundNetworkAccess,
                Administrators = new ServerExternalAdministratorArgs
                {
                    AdministratorType = AdministratorType.ActiveDirectory,
                    AzureADOnlyAuthentication = true,
                    PrincipalType = PrincipalType.Group,
                    Login = s.groupName,
                    Sid = s.groupId
                }
            });

            int i = 0;
            foreach (var ip in s.allowedIPs)
            {
                i++;
                var fw = new FirewallRule($"ip_{i}", new FirewallRuleArgs
                {
                    ServerName = sql.Name,
                    ResourceGroupName = resourceGroup.Name,
                    StartIpAddress = ip.Key,
                    EndIpAddress = ip.Value,
                    FirewallRuleName = $"ip_{i}"
                });
            }

            foreach (var d in s.databases)
            {

                var db = new Database(d.name, new DatabaseArgs
                {
                    ServerName = sql.Name,
                    DatabaseName = d.name,
                    Tags = tags,
                    ResourceGroupName = resourceGroup.Name,
                    ZoneRedundant = d.zoneRedundant,
                    RequestedBackupStorageRedundancy = d.requestedBackupStorageRedundancy,
                    Sku = new SkuArgs
                    {
                        Name = d.skuName
                    }
                });

                if (d.deployJobAgent)
                {
                    var id = new UserAssignedIdentity(d.name, new UserAssignedIdentityArgs
                    {
                        ResourceGroupName = resourceGroup.Name,
                        Tags = tags,
                        ResourceName = $"id-sqlja-{d.name}"
                    });

                    var agent = new JobAgent(d.name, new JobAgentArgs
                    {
                        DatabaseId = db.Id,
                        JobAgentName = $"sqlja-{d.name}",
                        ResourceGroupName = resourceGroup.Name,
                        ServerName = sql.Name,
                        Sku = new SkuArgs
                        {
                            Name = d?.jobAgent?.skuName ?? "JA100",
                            Capacity = d?.jobAgent?.skuCapacity
                        },
                        Identity = new JobAgentIdentityArgs
                        {
                            Type = JobAgentIdentityType.UserAssigned,
                            UserAssignedIdentities = new Output<string>[]
                            {
                                id.Id
                            }
                        }
                    });
                    //Bug:
                    // var privateEndpoint = new Pulumi.AzureNative.Sql.JobPrivateEndpoint(d.name, new Pulumi.AzureNative.Sql.JobPrivateEndpointArgs
                    // {
                    //     JobAgentName = agent.Name,
                    //     ServerName = sql.Name,
                    //     PrivateEndpointName = $"pep-sqlja-{d.name}",
                    //     TargetServerAzureResourceId = agent.Id,
                    //     ResourceGroupName = resourceGroup.Name,
                    // });

                }
            }

            if (s?.privateIP != null)
            {

                var pep = new PrivateEndpoint($"pep-{s.name}", new PrivateEndpointArgs
                {
                    PrivateEndpointName = $"pep-{s.name}",
                    CustomNetworkInterfaceName = $"nic-{s.name}",
                    ResourceGroupName = resourceGroup.Name,
                    IpConfigurations = new Pulumi.AzureNative.Network.Inputs.PrivateEndpointIPConfigurationArgs
                    {
                        GroupId = "sqlServer",
                        MemberName = "sqlServer",
                        PrivateIPAddress = s.privateIP,
                        Name = $"config"
                    },
                    Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
                    {
                        Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                        ? id
                        : throw new Exception($"snet-pep not found"))
                    },
                    PrivateLinkServiceConnections = new Pulumi.AzureNative.Network.Inputs.PrivateLinkServiceConnectionArgs
                    {
                        Name = $"config",
                        PrivateLinkServiceId = sql.Id,
                        GroupIds = new InputList<string>
                        {
                            "sqlServer"
                        }
                    }
                });

                var dnszone = new PrivateDnsZoneGroup($"dns-{s.name}", new PrivateDnsZoneGroupArgs
                {
                    Name = $"default",
                    ResourceGroupName = resourceGroup.Name,
                    PrivateEndpointName = pep.Name,
                    PrivateDnsZoneGroupName = $"vaultcore",
                    PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
                    {
                        Name = $"vaultcore",
                        // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                        PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.database.windows.net", out var id)
                        ? id
                        : throw new Exception($"Private zone privatelink.database.windows.net not found"))
                    }
                });
            }
        }
    }
}

class SqlServerX
{
    public string name { get; set; } = "";
    public string publicNetworkAccess { get; set; } = "Enabled";
    public string restrictOutboundNetworkAccess { get; set; } = "Enabled";
    public string groupName { get; set; } = "";
    public string groupId { get; set; } = "";
    public string? privateIP { get; set; }
    public Dictionary<string, string> allowedIPs { get; set; } = [];
    public List<DatabaseX> databases { get; set; } = [];
}

class DatabaseX
{
    public string name { get; set; } = "";
    public string skuName { get; set; } = "BC_Gen5_2";
    public bool zoneRedundant { get; set; } = false;
    public string requestedBackupStorageRedundancy { get; set; } = "Local";
    public bool deployJobAgent { get; set; } = false;
    public JobAgentX? jobAgent { get; set; }


}

class JobAgentX
{
    public string skuName { get; set; } = "JA100";
    public int skuCapacity { get; set; } = 100;


}