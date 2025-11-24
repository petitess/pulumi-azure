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
            var stStack = new StStack(pdnszStack.PrivateZoneIds);

            return new Dictionary<string, object?>
            {
                ["vnetId"] = vnetStack.VnetId,
                ["pdnszIds"] = pdnszStack.PrivateZoneIds,
                ["pdnszNames"] = pdnszStack.PrivateZoneNames
            };
        });
    }
}