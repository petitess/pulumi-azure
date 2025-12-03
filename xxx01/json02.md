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
    public object[] featureFlags { get; set; }
}
```
