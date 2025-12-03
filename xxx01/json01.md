### Example 1
```cs
var featureFlags = new[]
{
    new
    {
        id = "CoworkerSearch",
        description = "Switches between new coworker and old coworker view",
        enabled = false,
        conditions = new {client_filters = new object[0]}
    },
    new
    {
        id = "ActivityTaskMutations",
        description = "Toggles ActivityTask mutations",
        enabled = false,
        conditions = new {client_filters = new object[0]}
    }
};

foreach (var flag in featureFlags)
{
    var keyValue = new KeyValue($"ff-{flag.id}", new KeyValueArgs
    {
        ConfigStoreName = appcs.Name,
        ResourceGroupName = resourceGroup.Name,
        ContentType = "application/vnd.microsoft.appconfig.ff+json;charset=utf-8",
        KeyValueName = $".appconfig.featureflag~2F${flag.id}",
        Value = $"{{\"id\": \"{flag.id}\", \"description\": \"{flag.description}\", \"enabled\": {flag.enabled.ToString().ToLower()}, \"conditions\": {{\"client_filters\":[]}}}}",
        Tags = tags
    });
}
```
### Example 2
```cs
var featureFlags = new[]
{
    new
    {
        id = "CoworkerSearch",
        description = "Switches between new coworker and old coworker view",
        enabled = false,
        conditions = new {client_filters = new object[0]}
    },
    new
    {
        id = "ActivityTaskMutations",
        description = "Toggles ActivityTask mutations",
        enabled = false,
        conditions = new {client_filters = new object[0]}
    }
};

Console.WriteLine("1: " + featureFlags[0]);
Console.WriteLine("2: " + JsonSerializer.Serialize(featureFlags[0], new JsonSerializerOptions { WriteIndented = true }));

foreach (var flag in featureFlags)
{
    var keyValue = new KeyValue($"ff-{flag.id}", new KeyValueArgs
    {
        ConfigStoreName = appcs.Name,
        ResourceGroupName = resourceGroup.Name,
        ContentType = "application/vnd.microsoft.appconfig.ff+json;charset=utf-8",
        KeyValueName = $".appconfig.featureflag~2F${flag.id}",
        //WriteIndented = false JSON is serialized without any extra white space
        Value = JsonSerializer.Serialize(flag, new JsonSerializerOptions { WriteIndented = false }), 
        Tags = tags
    });
}
```
