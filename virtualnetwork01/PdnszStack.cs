using Pulumi;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using Pulumi.AzureNative.PrivateDns;
using Pulumi.AzureNative.PrivateDns.Inputs;
using System.Collections.Immutable;

class PdnszStack
{
    [Output] public Output<ImmutableArray<Output<string>>> PrivateZoneNames { get; set; } = null!;
    [Output] public Output<ImmutableDictionary<string, Output<string>>> PrivateZoneIds { get; set; } = null!;

    public PdnszStack(Output<string> vnetId)
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var dnszones = config.RequireObject<string[]>("dnszones");

        var resourceGroup = new ResourceGroup("rgPdnsz",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-dns-{env}-01",
            Tags = tags,
        });

        var zoneNames = new List<Output<string>>();
        var zoneIdMap = new Dictionary<string, Output<string>>();

        foreach (var d in dnszones)
        {
            var pdnsz = new PrivateZone(d, new PrivateZoneArgs
            {
                Location = "global",
                PrivateZoneName = d,
                ResourceGroupName = resourceGroup.Name,
                Tags = tags
            });

            var link = new VirtualNetworkLink($"l-{d}", new VirtualNetworkLinkArgs
            {
                PrivateZoneName = d,
                ResourceGroupName = resourceGroup.Name,
                Location = "global",
                RegistrationEnabled = false,
                VirtualNetworkLinkName = $"l-{d}",
                VirtualNetwork = new SubResourceArgs
                {
                    Id = vnetId
                }
            });
            zoneNames.Add(pdnsz.Name);
            zoneIdMap[d] = pdnsz.Id;
        }

        PrivateZoneNames = Output.Create(ImmutableArray.ToImmutableArray(zoneNames));
        PrivateZoneIds = Output.Create(ImmutableDictionary.ToImmutableDictionary(zoneIdMap));
    }
}