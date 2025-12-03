using Pulumi.AzureNative.Authorization;
using Pulumi;
using System.Collections.Generic;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using System.Collections.Immutable;

class AmplStack
{
    public AmplStack(
        Output<ImmutableDictionary<string, Output<string>>> subnetIds,
        Output<ImmutableDictionary<string, Output<string>>> pdnszIds
        )
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var clientConfig = GetClientConfig.InvokeAsync();
        var amplName = $"ampl-pulumi-{env}-01";
        var privateIP = "10.100.4.11";
        var TenantRootGroup = "52658ee1-97c1-4c2b-9a34-f177ebda5098";

        var resourceGroup = new ResourceGroup("rgAmpl",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-ampl-{env}-01",
            Tags = tags,
        });
        var ampl = new ResourceManagementPrivateLink("ampl", new ResourceManagementPrivateLinkArgs
        {
            RmplName = amplName,
            ResourceGroupName = resourceGroup.Name
        });

        var amplA = new PrivateLinkAssociation("sd", new PrivateLinkAssociationArgs
        {
            GroupId = TenantRootGroup,
            PlaId = GuidX.CreateGuidV3("PlaId").ToString(),
            Properties = new Pulumi.AzureNative.Authorization.Inputs.PrivateLinkAssociationPropertiesArgs
            {
                PrivateLink = ampl.Id,
                PublicNetworkAccess = "Enabled"
            }
        });

        if (privateIP != null)
        {
            var pep = new PrivateEndpoint($"pep-{amplName}", new PrivateEndpointArgs
            {
                PrivateEndpointName = $"pep-{amplName}",
                CustomNetworkInterfaceName = $"nic-{amplName}",
                ResourceGroupName = resourceGroup.Name,
                IpConfigurations = new PrivateEndpointIPConfigurationArgs
                {
                    GroupId = "ResourceManagement",
                    MemberName = "ResourceManagement",
                    PrivateIPAddress = privateIP,
                    Name = $"config"
                },
                Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
                {
                    Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                    ? id
                    : throw new System.Exception($"snet-pep not found"))
                },
                PrivateLinkServiceConnections = new PrivateLinkServiceConnectionArgs
                {
                    Name = $"config",
                    PrivateLinkServiceId = ampl.Id,
                    GroupIds = new InputList<string>
                        {
                            "ResourceManagement"
                        }
                }
            });

            var dnszone = new PrivateDnsZoneGroup($"dns-{amplName}", new PrivateDnsZoneGroupArgs
            {
                Name = $"default",
                ResourceGroupName = resourceGroup.Name,
                PrivateEndpointName = pep.Name,
                PrivateDnsZoneGroupName = $"azure",
                PrivateDnsZoneConfigs = new PrivateDnsZoneConfigArgs
                {
                    Name = $"azure",
                    // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                    PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.azure.com", out var id)
                    ? id
                    : throw new System.Exception($"Private zone privatelink.azure.com not found"))
                }
            });
        }
    }
}