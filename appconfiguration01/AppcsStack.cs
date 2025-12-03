using Pulumi;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using Pulumi.AzureNative.AppConfiguration;
using System;
using System.Text.Json;
using System.Collections.Immutable;
using Pulumi.AzureNative.Authorization;
using System.Text.Json.Nodes;

class AppcsStack
{
    public AppcsStack(
        Output<ImmutableDictionary<string, Output<string>>> subnetIds,
        Output<ImmutableDictionary<string, Output<string>>> pdnszIds
        )
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var appConfigurations = config.RequireObject<List<AppConfigX>>("appConfigurations");
        var clientConfig = GetClientConfig.InvokeAsync();

        var resourceGroup = new ResourceGroup("rgAppcs",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-appcs-{env}-01",
            Tags = tags,
        });

        var rbacAppcs = new RoleAssignment("rbacAppcs", new RoleAssignmentArgs
        {
            RoleAssignmentName = GuidX.CreateGuidV3("rbacAppcs").ToString(),
            Scope = $"subscriptions/{clientConfig.Result.SubscriptionId}/resourceGroups/rg-pulumi-appcs-{env}-01",
            PrincipalId = clientConfig.Result.ObjectId,
            PrincipalType = PrincipalType.User,
            RoleDefinitionId = RbacRole.GetRoleIds().Apply(x => x["AppConfigurationDataOwner"])
        });

        foreach (var app in appConfigurations)
        {
            var appcs = new ConfigurationStore("appConfigStore", new ConfigurationStoreArgs
            {
                ConfigStoreName = app.name,
                ResourceGroupName = app?.rgName != null ? app.rgName : resourceGroup.Name,
                Tags = tags,
                PublicNetworkAccess = app?.publicNetworkAccess ?? "Enabled",
                DisableLocalAuth = true,
                DataPlaneProxy = new Pulumi.AzureNative.AppConfiguration.Inputs.DataPlaneProxyPropertiesArgs
                {
                    AuthenticationMode = app?.AuthenticationMode ?? "Pass-through",
                    PrivateLinkDelegation = app?.PrivateLinkDelegation ?? "Enabled",
                },
                Identity = new Pulumi.AzureNative.AppConfiguration.Inputs.ResourceIdentityArgs
                {
                    Type = IdentityType.SystemAssigned
                },
                Sku = new Pulumi.AzureNative.AppConfiguration.Inputs.SkuArgs
                {
                    Name = app.skuName
                }
            });
            //Instead of YAML
            // var featureFlags = new[]
            // {
            // new { id = "CoworkerSearch", description = "Switches between new coworker and old coworker view", enabled = false, conditions = new {client_filters = new object[0]} },
            // new { id = "ActivityTaskMutations", description = "Toggles ActivityTask mutations", enabled = false, conditions = new {client_filters = new object[0]} }
            // };

            foreach (var flag in app.featureFlags)
            {
                var f = JsonNode.Parse(flag.ToString());
                var keyValue = new KeyValue($"ff-{f?["id"]}", new KeyValueArgs
                {
                    ConfigStoreName = appcs.Name,
                    ResourceGroupName = resourceGroup.Name,
                    ContentType = "application/vnd.microsoft.appconfig.ff+json;charset=utf-8",
                    KeyValueName = $".appconfig.featureflag~2F${f?["id"]}",
                    Value = JsonSerializer.Serialize(flag, new JsonSerializerOptions { WriteIndented = false }),
                    Tags = tags
                });
            }

            if (app?.privateIP != null)
            {

                var pep = new PrivateEndpoint($"pep-{app.name}", new PrivateEndpointArgs
                {
                    PrivateEndpointName = $"pep-{app.name}",
                    CustomNetworkInterfaceName = $"nic-{app.name}",
                    ResourceGroupName = resourceGroup.Name,
                    IpConfigurations = new PrivateEndpointIPConfigurationArgs
                    {
                        GroupId = "configurationStores",
                        MemberName = "configurationStores",
                        PrivateIPAddress = app.privateIP,
                        Name = $"config"
                    },
                    Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
                    {
                        Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                        ? id
                        : throw new Exception($"snet-pep not found"))
                    },
                    PrivateLinkServiceConnections = new PrivateLinkServiceConnectionArgs
                    {
                        Name = $"config",
                        PrivateLinkServiceId = appcs.Id,
                        GroupIds = new InputList<string>
                        {
                            "configurationStores"
                        }
                    }
                });

                var dnszone = new PrivateDnsZoneGroup($"dns-{app.name}", new PrivateDnsZoneGroupArgs
                {
                    Name = $"default",
                    ResourceGroupName = resourceGroup.Name,
                    PrivateEndpointName = pep.Name,
                    PrivateDnsZoneGroupName = $"azconfig",
                    PrivateDnsZoneConfigs = new PrivateDnsZoneConfigArgs
                    {
                        Name = $"azconfig",
                        // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                        PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.azconfig.io", out var id)
                        ? id
                        : throw new Exception($"Private zone privatelink.azconfig.io not found"))
                    }
                });
            }
        }
    }
}

class AppConfigX
{
    public string name { get; set; } = "";
    public string? rgName { get; set; }
    public string publicNetworkAccess { get; set; } = "Enabled";
    public string AuthenticationMode { get; set; } = "Pass-through";
    public string PrivateLinkDelegation { get; set; } = "Enabled";
    public string skuName { get; set; } = "Standard";
    public string? privateIP { get; set; }
    public object[] featureFlags { get; set; }
}