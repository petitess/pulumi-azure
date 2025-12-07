var provider = new Pulumi.AzureNative.Resources.Resource($"id-{d.name}", new Pulumi.AzureNative.Resources.ResourceArgs
{
    ResourceGroupName = $"rg-pulumi-sql-{env}-01",
    ApiVersion = "2024-11-30",
    ResourceProviderNamespace = "Microsoft.ManagedIdentity",
    ResourceName = $"id-custom-{d.name}",
    ResourceType = "/userassignedidentities",
    ParentResourcePath = "/",
    Properties = new InputMap<object>
    {
        ["isolationScope"] = "None"
    }
});
