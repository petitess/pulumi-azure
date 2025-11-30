using Pulumi;
using System.Collections.Immutable;
using System.Collections.Generic;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using System.Linq;

class AspStack
{
    [Output] public Output<ImmutableDictionary<string, Output<string>>> Ids { get; set; } = null!;

    public AspStack()
    {
        var config = new Config("param");
        var env = config.Require("env");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        var appServicePlans = config.RequireObject<List<AppServicePlanC>>("appServicePlans");

        var idMap = new Dictionary<string, Output<string>>();

        var resourceGroup = new ResourceGroup("rgAsp",
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-pulumi-asp-{env}-01",
            Tags = tags,
        });
        foreach (var asp in appServicePlans)
        {
            var kindAllowedValues = new[] { "app", "app,linux", "app,linux,container", "hyperV", "app,container,windows", "app,linux,kubernetes", "app,linux,container,kubernetes", "functionapp", "functionapp,linux", "functionapp,linux,container,kubernetes", "functionapp,linux,kubernetes" };
            if (!kindAllowedValues.Contains(asp.kind))
            {
                throw new System.Exception($"Invalid kind '{asp.kind}'. Allowed values are: {string.Join(", ", kindAllowedValues)}");
            }

            var appServicePlan = new AppServicePlan(asp.name, new AppServicePlanArgs
            {
                Name = asp.name,
                ResourceGroupName = resourceGroup.Name,
                Kind = asp.kind,
                Sku = new Pulumi.AzureNative.Web.Inputs.SkuDescriptionArgs
                {
                    Name = asp.skuName,
                    Tier = asp.skuTier
                }
            });
            idMap[asp.name] = appServicePlan.Id;
        }
        Ids = Output.Create(ImmutableDictionary.ToImmutableDictionary(idMap));
    }
}

class AppServicePlanC
{
    public string name { get; set; } = "";
    public string skuName { get; set; } = "P0v4";
    public string skuTier { get; set; } = "Premium0V4";
    public string kind { get; set; } = "app";
}