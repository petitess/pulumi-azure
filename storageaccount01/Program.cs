using System.Threading.Tasks;
using Pulumi;
using System.Collections.Generic;

class Program
{
    static async Task Main()
    {
        await Deployment.RunAsync(() =>
        {
            var vnetStack = new VnetStack();
            var pdnszStack = new PdnszStack(vnetStack.VnetId);
            var stStack = new StStack(vnetStack.subnetIds,pdnszStack.Ids);

            return new Dictionary<string, object?>
            {
                ["vnetId"] = vnetStack.VnetId,
                ["pdnszIds"] = pdnszStack.Ids,
                ["subnetIds"] = vnetStack.subnetIds,
                ["stIds"] = stStack.Ids,
                ["ReaderId"] = RbacRole.GetRoleIds().Apply(x => x["Reader"])
            };
        });
    }
}