sql.State.Apply(async state =>
        {
            if (state == "Ready" && !Pulumi.Deployment.Instance.IsDryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(20));
                HttpResponseMessage list = null;

                try
                {
                    list = await httpClient.GetAsync($"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/privateEndpointConnections?api-version=2023-08-01");
                    list.EnsureSuccessStatusCode();
                }
                catch
                {
                    Log.Error("ERROR LISTING PRIVATE ENDPOINT");
                }

                var peps = JsonNode.Parse(list.Content.ReadAsStringAsync().Result);
                var pepsFilter = peps?["value"].AsArray().Where(x => x["name"].ToString().Contains("pep-sqlja"));
                // var pepsFilter = peps?["value"].AsArray().Where(x=>x["name"].ToString() == "EJ_8445365c-4fe8-4c38-a22b-8d9beaa2abcf_pep-sqlja-db-log-877047e1-0da7-432b-a58b-658baab5b35e");

                Log.Info("Sql Agent Pep Count: " + pepsFilter?.ToArray().Count());
                Log.Info("Sql Pep Count: " + peps?["value"]?.AsArray().Count);

                foreach (var pep in pepsFilter)
                {

                    if (pep?["properties"]?["privateLinkServiceConnectionState"]?["status"]?.ToString() == "Pending")
                    {
                        string pepName = pep?["name"].ToString();
                        Log.Info("Pep Connection name: " + pepName);

                        var pepPut = new
                        {
                            Properties = new
                            {
                                privateLinkServiceConnectionState = new
                                {
                                    status = "Approved",
                                    description = $"Approved by REST API: {clientConfig.ObjectId}"
                                }
                            }
                        };
                        Log.Info("Pep Connection Json: " + pep);


                        var jsonPrettyPep = JsonSerializer.Serialize(pepPut, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                        var contentPutPep = new StringContent(jsonPrettyPep, System.Text.Encoding.UTF8, "application/json");
                        try
                        {
                            var approve = await httpClient.PutAsync($"https://management.azure.com/subscriptions/{clientConfig.SubscriptionId}/resourceGroups/rg-pulumi-sql-{env}-01/providers/Microsoft.Sql/servers/{s.name}/privateEndpointConnections/{pepName}?api-version=2023-08-01", contentPutPep);
                            Log.Info("MESSAGE_APPROVE: " + approve.StatusCode);
                            Log.Info("JSON_APPROVE: " + approve.Content.ReadAsStringAsync().Result);
                            approve.EnsureSuccessStatusCode();
                        }
                        catch
                        {
                            Log.Error("ERROR APPROVING PRIVATE ENDPOINT");
                        }
                    }
                }
            }
            else
            {
                Log.Info($"Sql is {state}. IsDryRun: {Pulumi.Deployment.Instance.IsDryRun}");
            }
            return Task.CompletedTask;
        });
