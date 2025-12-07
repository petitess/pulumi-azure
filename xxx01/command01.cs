class SqlStack
{
    public SqlStack()
    {
        var body = new
        {
            Properties = new
            {
                targetServerAzureResourceId = "/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}"
            }
        };

        string jsonPretty = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false
        });

        var contentPut = new System.Net.Http.StringContent(jsonPretty, System.Text.Encoding.UTF8, "application/json");

        var url = $"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/jobAgents/sqlja-{d.name}/privateEndpoints/pep-sqlja-{d.name}?api-version=2023-08-01";

        var azCliPep = new Pulumi.Command.Local.Command(d.name, new Pulumi.Command.Local.CommandArgs
        {
            Create = $"az rest --method put --url {url} --body {jsonPretty}",
            Update = $"az rest --method put --url {url} --body {jsonPretty}",
            Triggers = new[]
            {
                            System.DateTime.UtcNow.ToString("O")
                        },
        }, new Pulumi.CustomResourceOptions
        {
            DependsOn = new Pulumi.InputList<Pulumi.Resource>
                        {
                            agent
                        }
        });
    }
}
