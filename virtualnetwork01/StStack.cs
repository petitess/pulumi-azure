using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.PrivateDns;
using System.Collections.Immutable;

class StStack
{
    [Output] public Output<string>? PrimaryStorageKey { get; set; }
    public StStack(Output<ImmutableDictionary<string, Output<string>>> pdnszId)
    {
        var config = new Config("param");
        var env = config.Require("env");
        var rgVnetName = config.Require("rgVnetName");
        var vnetName = config.Require("vnetName");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var storageAccounts = config.RequireObject<List<StAccount>>("storageAccounts");

        var snet = GetSubnet.Invoke(new GetSubnetInvokeArgs
        {
            ResourceGroupName = rgVnetName,
            VirtualNetworkName = vnetName,
            SubnetName = "snet-pep"
        });

        var resourceGroup = new ResourceGroup("rgSt",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-st-{env}-01",
            Tags = tags,
        });

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
                var pdnsz = GetPrivateZone.Invoke(new GetPrivateZoneInvokeArgs
                {
                    ResourceGroupName = $"rg-pulumi-dns-{env}-01",
                    PrivateZoneName = $"privatelink.{p.Key}.core.windows.net"
                });

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
                        Id = snet.Apply(v => v.Id)
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
                        PrivateDnsZoneId = pdnszId.Apply(z => z.TryGetValue($"privatelink.{p.Key}.core.windows.net", out var id)
                        ? id
                        : throw new System.Exception($"Private zone privatelink.{p.Key}.core.windows.net not found"))
                    }
                });
            }

            var storageAccountKeys = ListStorageAccountKeys.Invoke(new ListStorageAccountKeysInvokeArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = st.name
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
        }
    }
}

class StAccount
{
    public string name { get; set; }
    public string skuName { get; set; }
    public string[] allowedIPs { get; set; }
    public string publicNetworkAccess { get; set; }
    public Dictionary<string, string> privateEndpoints { get; set; }
}