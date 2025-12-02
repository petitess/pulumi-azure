using Pulumi;
using Pulumi.AzureNative.Authorization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

return await Deployment.RunAsync(() =>
{
    var config = GetClientConfig.InvokeAsync().Result;
    var getToken = GetClientToken.InvokeAsync().Result;
    Environment.SetEnvironmentVariable("AZ_TOKENX", getToken.Token);
    string azTokenX = Environment.GetEnvironmentVariable("AZ_TOKENX");

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    httpClient.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", azTokenX);

    var get = httpClient.GetAsync($"https://management.azure.com/subscriptions/{config.SubscriptionId}?api-version=2025-04-01");
    Log.Info("MESSAGE_GET: " + get.Result.StatusCode);
    Log.Info("JSON_GET: " + get.Result.Content.ReadAsStringAsync().Result);
    // get.Result.EnsureSuccessStatusCode();

    var logAnalytics = new
    {
        Name = "log-pulumi-01",
        Location = "swedencentral",
        Properties = new
        {
            sku = new
            {
                name = "PerGB2018"
            }
        },
        Identity = new
        {
            type = "SystemAssigned"
        }
    };

    string jsonPretty = JsonSerializer.Serialize(logAnalytics, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    var contentPut = new StringContent(jsonPretty, System.Text.Encoding.UTF8, "application/json");

    var put = httpClient.PutAsync($"https://management.azure.com/subscriptions/{config.SubscriptionId}/resourceGroups/rg-common/providers/Microsoft.OperationalInsights/workspaces/{logAnalytics.Name}?api-version=2025-07-01", contentPut);
    Log.Info("MESSAGE_PUT: " + put.Result.StatusCode);
    Log.Info("JSON_PUT: " + put.Result.Content.ReadAsStringAsync().Result);
    return new Dictionary<string, object?>
    {
        ["get_req"] = get.Result.IsSuccessStatusCode,
        ["my_az_tokenX"] = azTokenX,
    };
});
