using Pulumi;
using System;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using System.Collections.Immutable;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Sql.Inputs;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.ManagedIdentity;
using Pulumi.AzureNative.Authorization;
using System.Text.Json;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;

class SqlStack
{
    [Output] public Output<ImmutableDictionary<string, Output<string>>> SqlIds { get; set; } = null!;
    public SqlStack(
        Output<ImmutableDictionary<string, Output<string>>> subnetIds,
        Output<ImmutableDictionary<string, Output<string>>> pdnszIds
        )
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var sqlServers = config.RequireObject<List<SqlServerX>>("sqlServers");

        var clientConfig = GetClientConfig.InvokeAsync().Result;
        var getToken = GetClientToken.InvokeAsync().Result;
        Environment.SetEnvironmentVariable("AZ_TOKENX", getToken.Token);
        string azTokenX = Environment.GetEnvironmentVariable("AZ_TOKENX");

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", azTokenX);

        var resourceGroup = new ResourceGroup("rgSql",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-sql-{env}-01",
            Tags = tags,
        });

        var sqlIdMap = new Dictionary<string, Output<string>>();

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
                    PrincipalType = Pulumi.AzureNative.Sql.PrincipalType.Group,
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

            JobAgentIdentityArgs? identity = null;
            if (s.databases.Any())
            {
                var id = new UserAssignedIdentity(s.name, new UserAssignedIdentityArgs
                {
                    ResourceGroupName = resourceGroup.Name,
                    Tags = tags,
                    ResourceName = $"id-sqlja-common-{env}-01"
                });

                identity = new JobAgentIdentityArgs
                {
                    Type = JobAgentIdentityType.UserAssigned,
                    UserAssignedIdentities = new Output<string>[]
                    {
                        id.Id
                    }
                };
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
                        Identity = identity
                    });

                    var body = new
                    {
                        Properties = new
                        {
                            targetServerAzureResourceId = $"/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}"
                        }
                    };

                    string jsonPretty = JsonSerializer.Serialize(body, new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });

                    var contentPut = new StringContent(jsonPretty, System.Text.Encoding.UTF8, "application/json");

                    var url = $"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/jobAgents/sqlja-{d.name}/privateEndpoints/pep-sqlja-{d.name}?api-version=2023-08-01";

                    var azCliPep = new Pulumi.Command.Local.Command(d.name, new Pulumi.Command.Local.CommandArgs
                    {
                        Create = $"az rest --method put --url {url} --body {jsonPretty}",
                        Update = $"az rest --method put --url {url} --body {jsonPretty}",
                        Triggers = new[]
                        {
                            DateTime.UtcNow.ToString("O") //If you want to run this every time
                        },
                    }, new CustomResourceOptions
                    {
                        DependsOn = new InputList<Pulumi.Resource>
                        {
                            agent
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

                    //Doesnt work for the first time
                    azCliPep.Stdout.Apply(async output =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        Log.Info("APPROVING PEP");

                        if (false && !Pulumi.Deployment.Instance.IsDryRun)
                        {
                            HttpResponseMessage list = null;

                            try
                            {
                                list = await httpClient.GetAsync($"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/privateEndpointConnections?api-version=2023-08-01");
                                list.EnsureSuccessStatusCode();
                            }
                            catch
                            {
                                Log.Error("ERROR LISTING PRIVATE ENDPOINT");
                            }

                            var peps = JsonNode.Parse(list.Content.ReadAsStringAsync().Result);
                            var pepsFilter = peps?["value"].AsArray().Where(x => x["name"].ToString().Contains($"pep-sqlja-{d.name}"));
                            // var pepsFilter = peps?["value"].AsArray().Where(x=>x["name"].ToString() == "EJ_8445365c-4fe8-4c38-a22b-8d9beaa2abcf_pep-sqlja-db-log-877047e1-0da7-432b-a58b-658baab5b35e");

                            Log.Info("Sql Agent Pep Count: " + pepsFilter?.ToArray().Count());
                            Log.Info("Sql Pep Count: " + peps?["value"]?.AsArray().Count);

                            foreach (var pep in pepsFilter)
                            {
                                if (pep?["properties"]?["privateLinkServiceConnectionState"]?["status"]?.ToString() == "Pending")
                                {
                                    var pepName = pep?["name"]?.ToString();
                                    Log.Info("Pep Connection name: " + pepName);

                                    var pepPut = new
                                    {
                                        Properties = new
                                        {
                                            privateLinkServiceConnectionState = new
                                            {
                                                status = "Approved",
                                                description = $"Approved by REST API: {clientConfig.ObjectId}"
                                            }
                                        }
                                    };
                                    Log.Info("Pep Connection Json: " + pep);

                                    var jsonPrettyPep = JsonSerializer.Serialize(pepPut, new JsonSerializerOptions
                                    {
                                        WriteIndented = true
                                    });

                                    var contentPutPep = new StringContent(jsonPrettyPep, System.Text.Encoding.UTF8, "application/json");
                                    try
                                    {
                                        var approve = await httpClient.PutAsync($"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/privateEndpointConnections/{pepName}?api-version=2023-08-01", contentPutPep);
                                        Log.Info("MESSAGE_APPROVE: " + approve.StatusCode);
                                        Log.Info("JSON_APPROVE: " + approve.Content.ReadAsStringAsync().Result);
                                        approve.EnsureSuccessStatusCode();
                                    }
                                    catch
                                    {
                                        Log.Error("ERROR APPROVING PRIVATE ENDPOINT");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Info($"{output}. IsDryRun: {Pulumi.Deployment.Instance.IsDryRun}");
                        }
                        return Task.CompletedTask;
                    });
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
                    PrivateDnsZoneGroupName = $"database",
                    PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
                    {
                        Name = $"database",
                        // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                        PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.database.windows.net", out var id)
                        ? id
                        : throw new Exception($"Private zone privatelink.database.windows.net not found"))
                    }
                });
            }
            sqlIdMap[s.name] = sql.Id;
        }
        SqlIds = Output.Create(ImmutableDictionary.ToImmutableDictionary(sqlIdMap));
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
