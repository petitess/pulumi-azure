using Pulumi;
using Pulumi.AzureNative.Authorization;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class RbacRole
{
    public static Output<ImmutableDictionary<string, string>> GetRoleIds()
    {
        var builtInRoles = new Dictionary<string, string>
        {
            ["KeyVaultAdmin"] = "00482a5a-887f-4fb3-b363-3b7fe8e74483",
            ["Contributor"] = "b24988ac-6180-42a0-ab88-20f7382dd24c",
            ["Reader"] = "acdd72a7-3385-48ef-bd42-f606fba81ae7",
            ["StorageBlobDataContributor"] = "ba92f5b4-2d11-453d-a403-e96b0029c9fe",
            ["MonitoringMetricsPublisher"] = "3913510d-42f4-4e42-8a64-420c390055eb"
        };

        return Output.Create(GetClientConfig.InvokeAsync()).Apply(clientConfig =>
        {
            var scope = $"/subscriptions/{clientConfig.SubscriptionId}";
            var tasks = new List<Input<string>>();

            foreach (var role in builtInRoles)
            {
                var roleDef = GetRoleDefinition.Invoke(new GetRoleDefinitionInvokeArgs
                {
                    RoleDefinitionId = role.Value,
                    Scope = scope
                });

                tasks.Add(roleDef.Apply(def => def.Id));
            }

            return Output.All(tasks.ToArray()).Apply(ids =>
            {
                var result = ImmutableDictionary.CreateBuilder<string, string>();
                int i = 0;
                foreach (var role in builtInRoles)
                {
                    result[role.Key] = ids[i];
                    i++;
                }
                return result.ToImmutableDictionary();
            });
        });
    }
}