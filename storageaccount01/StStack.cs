using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;
using Pulumi.AzureNative.Network;
using System.Collections.Immutable;
using Pulumi.AzureNative.Authorization;
using System;
class StStack
{
    [Output] public Output<string>? PrimaryStorageKey { get; set; }
    [Output] public Output<ImmutableDictionary<string, Output<string>>> Ids { get; set; } = null!;

    public StStack(
        Output<ImmutableDictionary<string, Output<string>>> subnetIds,
        Output<ImmutableDictionary<string, Output<string>>> pdnszIds
        )
    {
        var config = new Config("param");
        var env = config.Require("env");
        var rgVnetName = config.Require("rgVnetName");
        var vnetName = config.Require("vnetName");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var storageAccounts = config.RequireObject<List<StAccount>>("storageAccounts");
        var clientConfig = GetClientConfig.InvokeAsync();

        var resourceGroup = new ResourceGroup("rgSt",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-st-{env}-01",
            Tags = tags,
        });

        var idMap = new Dictionary<string, Output<string>>();

        foreach (var st in storageAccounts)
        {
            List<IPRuleArgs> IpRules = [];
            foreach (var ip in st.allowedIPs)
            {
                IpRules.Add(
                new IPRuleArgs
                {
                    IPAddressOrRange = ip
                });
            }

            var storageAccount = new StorageAccount(st.name, new StorageAccountArgs
            {
                AccountName = st.name,
                ResourceGroupName = resourceGroup.Name,
                Sku = new SkuArgs
                {
                    Name = st.skuName
                },
                Kind = Kind.StorageV2,
                PublicNetworkAccess = st.publicNetworkAccess,
                DefaultToOAuthAuthentication = true,
                NetworkRuleSet = new NetworkRuleSetArgs
                {
                    Bypass = "AzureServices",
                    DefaultAction = DefaultAction.Deny,
                    IpRules = IpRules
                }
            });

            foreach (var p in st.privateEndpoints)
            {

                var pep = new PrivateEndpoint($"pep-{st.name}-{p.Key}", new PrivateEndpointArgs
                {
                    PrivateEndpointName = $"pep-{st.name}-{p.Key}",
                    CustomNetworkInterfaceName = $"nic-{st.name}-{p.Key}",
                    ResourceGroupName = resourceGroup.Name,
                    IpConfigurations = new Pulumi.AzureNative.Network.Inputs.PrivateEndpointIPConfigurationArgs
                    {
                        GroupId = p.Key,
                        MemberName = p.Key,
                        PrivateIPAddress = p.Value,
                        Name = $"config-{p}"
                    },
                    Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
                    {
                        Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                        ? id
                        : throw new System.Exception($"snet-pep not found"))
                    },
                    PrivateLinkServiceConnections = new Pulumi.AzureNative.Network.Inputs.PrivateLinkServiceConnectionArgs
                    {
                        Name = $"config-{p}",
                        PrivateLinkServiceId = storageAccount.Id,
                        GroupIds = new InputList<string>
                        {
                            p.Key
                        }
                    }
                });

                var dnszone = new PrivateDnsZoneGroup($"default-{p.Key}", new PrivateDnsZoneGroupArgs
                {
                    Name = $"default-{p.Key}",
                    ResourceGroupName = resourceGroup.Name,
                    PrivateEndpointName = pep.Name,
                    PrivateDnsZoneGroupName = $"privatelink-{p.Key}-core-windows-net",
                    PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
                    {
                        Name = $"privatelink-{p.Key}-core-windows-net",
                        // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                        PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.{p.Key}.core.windows.net", out var id)
                        ? id
                        : throw new System.Exception($"Private zone privatelink.{p.Key}.core.windows.net not found"))
                    }
                });
            }

            var storageAccountKeys = ListStorageAccountKeys.Invoke(new ListStorageAccountKeysInvokeArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = storageAccount.Name
            });
            var primaryStorageKey = storageAccountKeys.Apply(accountKeys =>
            {
                var firstKey = accountKeys.Keys[0].Value;
                return Output.CreateSecret(firstKey);
            });

            primaryStorageKey.Apply(key =>
            {
                // System.IO.File.WriteAllText("storage-key.txt", key);
                // System.Console.WriteLine(key);
                return key;
            });

            PrimaryStorageKey = primaryStorageKey;
            idMap[st.name] = storageAccount.Id;
        }

        var rbacKv = new RoleAssignment($"rbacStCurrent-{clientConfig.Result.ObjectId}", new RoleAssignmentArgs
        {
            RoleAssignmentName = GuidX.CreateGuidV3($"rbacStCurrent-{clientConfig.Result.ObjectId}").ToString(),
            Scope = resourceGroup.Id,
            PrincipalId = clientConfig.Result.ObjectId,
            PrincipalType = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULUMI_STACK")) ? PrincipalType.User : PrincipalType.ServicePrincipal,
            RoleDefinitionId = RbacRole.GetRoleIds().Apply(x => x["StorageBlobDataContributor"])
        });

        Ids = Output.Create(ImmutableDictionary.ToImmutableDictionary(idMap));
    }
}

class StAccount
{
    public string name { get; set; } = "";
    public string skuName { get; set; } = "Standard_LRS";
    public string[] allowedIPs { get; set; } = [];
    public string publicNetworkAccess { get; set; } = "Enabled";
    public Dictionary<string, string> privateEndpoints { get; set; } = new Dictionary<string, string>();
}