using Pulumi;
using System.Threading.Tasks;
using System.Collections.Generic;
using Pulumi.Command.Local;
using System.Text.Json;

class Program
{
    static async Task Main()
    {
        await Deployment.RunAsync(() =>
        {
            var vnetStack = new VnetStack();
            var pdnszStack = new PdnszStack(vnetStack.VnetId);
            var sqlStack = new SqlStack(vnetStack.subnetIds, pdnszStack.Ids);

            var azCli = new Command("run-azure-cli", new CommandArgs
            {
                Create = $"az account show",
            });

            var azCliJson = azCli.Stdout.Apply(x => JsonSerializer.Deserialize<AzureCliX>(x));

            return new Dictionary<string, object?>
            {
                ["vnetId"] = vnetStack.VnetId,
                ["pdnszIds"] = pdnszStack.Ids,
                ["subnetIds"] = vnetStack.subnetIds,
                ["ReaderId"] = RbacRole.GetRoleIds().Apply(x => x["Reader"]),
                ["azureCli"] = azCliJson.Apply(x => x?.id),
            };
        });
    }
}

public class AzureCliX
{
    public string environmentName { get; set; }
    public string homeTenantId { get; set; }
    public string id { get; set; }
    public bool isDefault { get; set; }
    public List<object> managedByTenants { get; set; }
    public string name { get; set; }
    public string state { get; set; }
    public string tenantDefaultDomain { get; set; }
    public string tenantDisplayName { get; set; }
    public string tenantId { get; set; }
    public User user { get; set; }
}

public class User
{
    public string name { get; set; }
    public string type { get; set; }
}
