using Pulumi;
using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Authorization;

class KvStack
{
    [Output] public Output<ImmutableDictionary<string, Output<string>>> Ids { get; set; } = null!;

    public KvStack(
    Output<ImmutableDictionary<string, Output<string>>> subnetIds,
    Output<ImmutableDictionary<string, Output<string>>> pdnszIds
    )
    {
        var config = new Config("param");
        string env = config.Require("env");
        string rgVnetName = config.Require("rgVnetName");
        string vnetName = config.Require("vnetName");
        var keyVaults = config.RequireObject<List<KeyVaultX>>("keyVaults");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        string prefix = "pulumi";

        var resourceGroup = new ResourceGroup($"rg-Kv",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-{prefix}-kv-{env}-01",
            Tags = tags,
        });

        foreach (var k in keyVaults)
        {
            List<IPRuleArgs> IpRules = [];
            foreach (var ip in k.allowedIPs)
            {
                IpRules.Add(
                new IPRuleArgs
                {
                    Value = ip
                });
            }

            var kv = new Vault(k.name, new VaultArgs
            {
                VaultName = k.name,
                ResourceGroupName = k?.rgName != null ? k.rgName : resourceGroup.Name,
                Tags = tags,
                Properties = new VaultPropertiesArgs
                {
                    Sku = new SkuArgs
                    {
                        Family = "A",
                        Name = SkuName.Standard
                    },
                    TenantId = GetClientConfig.InvokeAsync().Result.TenantId,
                    EnabledForDeployment = k?.enabledForDeployment,
                    EnabledForDiskEncryption = k?.enabledForDiskEncryption,
                    EnabledForTemplateDeployment = k?.enabledForTemplateDeployment,
                    EnableRbacAuthorization = k?.enableRbacAuthorization,
                    EnableSoftDelete = k?.enableSoftDelete,
                    PublicNetworkAccess = k?.publicNetworkAccess ?? "Enabled",
                    NetworkAcls = new NetworkRuleSetArgs
                    {
                        Bypass = "AzureServices",
                        DefaultAction = "Deny",
                        IpRules = IpRules
                    }
                }
            });

            var key = new Key("key", new KeyArgs
            {
                KeyName = "key4096",
                ResourceGroupName = resourceGroup.Name,
                Tags = tags,
                VaultName = kv.Name,
                Properties = new KeyPropertiesArgs
                {
                    Kty = JsonWebKeyType.RSA,
                    KeySize = 4096
                }
            });
            var sec = new Secret("sec", new SecretArgs
            {
                SecretName = "guid",
                ResourceGroupName = resourceGroup.Name,
                Tags = tags,
                VaultName = kv.Name,
                Properties = new SecretPropertiesArgs
                {
                    ContentType = "pulumi",
                    Value = GuidX.CreateGuidV3("rbacKv").ToString()
                }
            });

            if (k?.privateIP != null)
            {

                var pep = new PrivateEndpoint($"pep-{k.name}", new PrivateEndpointArgs
                {
                    PrivateEndpointName = $"pep-{k.name}",
                    CustomNetworkInterfaceName = $"nic-{k.name}",
                    ResourceGroupName = resourceGroup.Name,
                    IpConfigurations = new Pulumi.AzureNative.Network.Inputs.PrivateEndpointIPConfigurationArgs
                    {
                        GroupId = "vault",
                        MemberName = "default",
                        PrivateIPAddress = k.privateIP,
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
                        PrivateLinkServiceId = kv.Id,
                        GroupIds = new InputList<string>
                        {
                            "vault"
                        }
                    }
                });

                var dnszone = new PrivateDnsZoneGroup($"dns-{k.name}", new PrivateDnsZoneGroupArgs
                {
                    Name = $"default",
                    ResourceGroupName = resourceGroup.Name,
                    PrivateEndpointName = pep.Name,
                    PrivateDnsZoneGroupName = $"vaultcore",
                    PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
                    {
                        Name = $"vaultcore",
                        // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                        PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.vaultcore.azure.net", out var id)
                        ? id
                        : throw new Exception($"Private zone privatelink.vaultcore.azure.net not found"))
                    }
                });
            }
        }
    }
}

class KeyVaultX
{
    public string name { get; set; } = "";
    public string? rgName { get; set; }
    public string publicNetworkAccess { get; set; } = "Enabled";
    public bool? enabledForDeployment { get; set; }
    public bool? enabledForDiskEncryption { get; set; }
    public bool? enabledForTemplateDeployment { get; set; }
    public bool? enableRbacAuthorization { get; set; } = true;
    public bool? enableSoftDelete { get; set; }
    public string[] allowedIPs { get; set; } = [];
    public string? privateIP { get; set; }

}