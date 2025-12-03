```cs
var appConfigurations = config.RequireObject<List<AppConfigX>>("appConfigurations");
```
```yml
config:
  param:appConfigurations:
    - name: appcs-pulumi-dev-01
      publicNetworkAccess: Enabled
      skuName: Standard
      privateIP: 10.100.4.10
      featureFlags:
        - id: CoworkerSearch
          description: "Switches between new coworker and old coworker view"
          enabled: false
          conditions: 
            client_filters: []
        - id: ActivityTaskMutations
          description: "Toggles ActivityTask mutations"
          enabled: false
          conditions: 
            client_filters: []
```
### Example 1
```cs
//dotnet add package Newtonsoft.Json
var flag = Newtonsoft.Json.Linq.JObject.Parse(JObject.Parse(appConfigurations[0].featureFlags[0].ToString()).ToString());
Console.WriteLine(flag["id"]);
Console.WriteLine(flag["enabled"]);
```
### Example 2
```cs
var net = System.Text.Json.Nodes.JsonNode.Parse(appConfigurations[0].featureFlags[0].ToString());
Console.WriteLine(net["id"]);
Console.WriteLine(net["enabled"]);
```
```cs
class AppConfigX
{
    public string name { get; set; } = "";
    public string? rgName { get; set; }
    public string publicNetworkAccess { get; set; } = "Enabled";
    public string AuthenticationMode { get; set; } = "Pass-through";
    public string PrivateLinkDelegation { get; set; } = "Enabled";
    public string skuName { get; set; } = "Standard";
    public string? privateIP { get; set; }
    public object[] featureFlags { get; set; }
}
```
