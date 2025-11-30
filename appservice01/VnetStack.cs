using Pulumi;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Linq;
using System.Collections.Immutable;

class VnetStack
{
    [Output] public Output<string> VnetId { get; set; }
    [Output] public Output<ImmutableDictionary<string, Output<string>>> subnetIds { get; set; } = null!;
    public VnetStack()
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var addressPrefixes = config.RequireObject<string[]>("addressPrefixes");

        var yaml = System.IO.File.ReadAllText($"./Pulumi.{env}_nsg.yaml");
        var deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

        var networkSecurityGroups = deserializer.Deserialize<List<Nsg>>(yaml);

        var resourceGroup = new ResourceGroup("rgVnet",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-vnet-{env}-01",
            Tags = tags,
        });

        var vnet = new VirtualNetwork("vnet",
        new VirtualNetworkArgs
        {
            VirtualNetworkName = $"vnet-{env}-01",
            Tags = tags,
            ResourceGroupName = resourceGroup.Name,
            AddressSpace = new AddressSpaceArgs
            {
                AddressPrefixes = addressPrefixes
            }
        });

        var subnetConfigs = config.RequireObject<List<SubnetConfig>>("subnets");
        var subnets = new List<Subnet>();
        var subnetIdMap = new Dictionary<string, Output<string>>();

        foreach (var snet in subnetConfigs)
        {
            List<NetworkSecurityGroup> nsgx = new List<NetworkSecurityGroup>();
            var SecurityRules = new List<Pulumi.AzureNative.Network.Inputs.SecurityRuleArgs>();
            foreach (var nsg in networkSecurityGroups.Where(a => a.snetName == snet.snetName))
            {
                foreach (var r in nsg.rules)
                {
                    var DestinationApplicationSecurityGroups = new List<Pulumi.AzureNative.Network.Inputs.ApplicationSecurityGroupArgs>();
                    foreach (var dasg in r.DestinationApplicationSecurityGroups)
                    {
                        DestinationApplicationSecurityGroups.Add(
                            new Pulumi.AzureNative.Network.Inputs.ApplicationSecurityGroupArgs
                            {
                                Id = dasg
                            }
                        );
                    }

                    var SourceApplicationSecurityGroups = new List<Pulumi.AzureNative.Network.Inputs.ApplicationSecurityGroupArgs>();
                    foreach (var sasg in r.SourceApplicationSecurityGroups)
                    {
                        SourceApplicationSecurityGroups.Add(
                            new Pulumi.AzureNative.Network.Inputs.ApplicationSecurityGroupArgs
                            {
                                Id = sasg
                            }
                        );
                    }

                    SecurityRules.Add(
                        new Pulumi.AzureNative.Network.Inputs.SecurityRuleArgs
                        {
                            Name = r.Name,
                            Access = r.Access,
                            Description = r.Description,
                            DestinationAddressPrefix = r.DestinationAddressPrefix,
                            DestinationAddressPrefixes = r.DestinationAddressPrefixes,
                            DestinationPortRange = r.DestinationPortRange,
                            DestinationPortRanges = r.DestinationPortRanges,
                            Direction = r.Direction,
                            Priority = r.Priority,
                            Protocol = r.Protocol,
                            SourceAddressPrefix = r.SourceAddressPrefix,
                            SourceAddressPrefixes = r.SourceAddressPrefixes,
                            SourcePortRange = r.sourcePortRange,
                            SourcePortRanges = r.SourcePortRanges,
                            DestinationApplicationSecurityGroups = DestinationApplicationSecurityGroups,
                            SourceApplicationSecurityGroups = SourceApplicationSecurityGroups
                        }
                    );
                }
                nsgx.Add(
                    new NetworkSecurityGroup($"nsg-snet-{nsg.snetName}", new Pulumi.AzureNative.Network.NetworkSecurityGroupArgs
                    {
                        ResourceGroupName = resourceGroup.Name,
                        NetworkSecurityGroupName = $"nsg-snet-{nsg.snetName}",
                        SecurityRules = SecurityRules
                    })
                );
            }

            var serviceEndpoints = new List<ServiceEndpointPropertiesFormatArgs>();
            foreach (var srv in snet.serviceEndpoints)
            {
                serviceEndpoints.Add(
                    new ServiceEndpointPropertiesFormatArgs
                    {
                        Service = srv
                    }
                );
            }

            InputList<DelegationArgs> delegations = [];
            if (!string.IsNullOrEmpty(snet.delegation))
            {
                delegations = new DelegationArgs
                {
                    Name = snet.delegation,
                    ServiceName = snet.delegation
                };
            }

            var subnet = new Subnet($"snet-{snet.snetName}", new Pulumi.AzureNative.Network.SubnetArgs
            {
                ResourceGroupName = resourceGroup.Name,
                VirtualNetworkName = vnet.Name,
                SubnetName = $"snet-{snet.snetName}",
                AddressPrefix = snet.prefix,
                PrivateEndpointNetworkPolicies = snet.privateEndpointNetworkPolicies,
                PrivateLinkServiceNetworkPolicies = snet.privateLinkServiceNetworkPolicies,
                Delegations = delegations,
                ServiceEndpoints = serviceEndpoints,
                NetworkSecurityGroup = nsgx.Any() ? new Pulumi.AzureNative.Network.Inputs.NetworkSecurityGroupArgs
                {
                    Id = nsgx.First().Id
                } : null
            });
            subnetIdMap[$"snet-{snet.snetName}"] = subnet.Id;
        }
        VnetId = vnet.Id;
        subnetIds = Output.Create(ImmutableDictionary.ToImmutableDictionary(subnetIdMap));
    }
}
class SubnetConfig
{
    public string snetName { get; set; } = "";
    public string prefix { get; set; } = "";
    public string privateEndpointNetworkPolicies { get; set; } = "Enabled";
    public string privateLinkServiceNetworkPolicies { get; set; } = "Enabled";
    public List<string> serviceEndpoints { get; set; } = new List<string>();
    public string? delegation { get; set; }
}

class Nsg
{
    public string snetName { get; set; } = "";
    public List<NsgRule> rules { get; set; } = new List<NsgRule>();
}

class NsgRule
{
    public string Name { get; set; } = "";
    public string Access { get; set; } = "";
    public string Description { get; set; } = "";
    public string DestinationAddressPrefix { get; set; } = "";
    public string[] DestinationAddressPrefixes { get; set; } = [];
    public string DestinationPortRange { get; set; } = "";
    public string[] DestinationPortRanges { get; set; } = [];
    public string Direction { get; set; } = "";
    public int Priority { get; set; } = 1;
    public string Protocol { get; set; } = "";
    public string SourceAddressPrefix { get; set; } = "";
    public string[] SourceAddressPrefixes { get; set; } = [];
    public string sourcePortRange { get; set; } = "";
    public string[] SourcePortRanges { get; set; } = [];
    public string[] DestinationApplicationSecurityGroups { get; set; } = [];
    public string[] SourceApplicationSecurityGroups { get; set; } = [];

}