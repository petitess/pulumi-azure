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
            var appcsStack = new AppcsStack(vnetStack.subnetIds, pdnszStack.Ids);
            var amplStack = new AmplStack(vnetStack.subnetIds, pdnszStack.Ids);

            return new Dictionary<string, object?>
            {
                ["vnetId"] = vnetStack.VnetId,
            };
        });
    }
}