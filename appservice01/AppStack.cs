using Pulumi;
using System.Collections.Immutable;
using System.Collections.Generic;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Monitor;
using Pulumi.AzureNative.Authorization;
using System;

class AppStack
{
    [Output] public Output<ImmutableDictionary<string, Output<string>>> Ids { get; set; } = null!;

    public AppStack(
    Output<ImmutableDictionary<string, Output<string>>> subnetIds,
    Output<ImmutableDictionary<string, Output<string>>> aspIds,
    Output<ImmutableDictionary<string, Output<string>>> stIds,
    Output<ImmutableDictionary<string, Output<string>>> pdnszIds
    )
    {
        var config = new Config("param");
        string env = config.Require("env");
        string rgVnetName = config.Require("rgVnetName");
        string vnetName = config.Require("vnetName");
        var tags = config.RequireObject<Dictionary<string, string>>("tags");
        string allowedIP = "188.150.118.0/24";
        string prefix = "pulumi";
        string appName = $"app-{prefix}-{env}-01";
        string slotName = $"stage";
        string privateIPAddress = "10.100.4.7";
        string privateIPAddressSlot = "10.100.4.8";

        var clientConfig = GetClientConfig.InvokeAsync();

        List<string> logs = new List<string>
        {
            "AppServiceAntivirusScanAuditLogs",
            "AppServiceHTTPLogs",
            "AppServiceConsoleLogs",
            "AppServiceAppLogs",
            "AppServiceFileAuditLogs",
            "AppServiceAuditLogs",
            "AppServiceIPSecAuditLogs",
            "AppServicePlatformLogs",
            "AppServiceAuthenticationLogs"
        };

        var resourceGroup = new ResourceGroup($"rgApp-{prefix}",///CORECT
        new ResourceGroupArgs
        {
            ResourceGroupName = $"rg-{prefix}-app-{env}-01",
            Tags = tags,
        });

        var appService = new WebApp($"app-{prefix}", new WebAppArgs
        {
            Name = appName,
            ResourceGroupName = resourceGroup.Name,
            Tags = tags,
            Kind = "app",
            PublicNetworkAccess = "Enabled",
            ServerFarmId = aspIds.Apply(z => z[$"asp-{prefix}-{env}-01"]),
            VirtualNetworkSubnetId = subnetIds.Apply(z => z.TryGetValue($"snet-app", out var id)
                        ? id
                        : throw new Exception($"snet-app not found")),
            HttpsOnly = true,
            StorageAccountRequired = false,
            Identity = new ManagedServiceIdentityArgs
            {
                Type = Pulumi.AzureNative.Web.ManagedServiceIdentityType.SystemAssigned
            },
            SiteConfig = new SiteConfigArgs
            {
                MinTlsVersion = "1.2",
                HealthCheckPath = "/health/index.html",
                VnetRouteAllEnabled = true,
                AlwaysOn = false,
                IpSecurityRestrictions = new IpSecurityRestrictionArgs
                {
                    Action = "Allow",
                    IpAddress = allowedIP
                }
            },
        });

        var appSettings = new WebAppApplicationSettings($"appsettings-{prefix}", new WebAppApplicationSettingsArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Name = appService.Name,
            Kind = "appsettings",
            Properties = new InputMap<string>
            {
                {"WEBSITE_TIME_ZONE" , "Central European Standard Time"},
                {"SLOT_NAME", env.ToUpper()},
                {"WEBSITE_AUTH_AAD_ALLOWED_TENANTS", clientConfig.Result.TenantId },
                {"CURRENT_SUBSCRIPTION_ID", clientConfig.Result.SubscriptionId },
                {"CURRENT_OBJECT_ID", clientConfig.Result.ObjectId },
                {"CURRENT_CLIENT_ID", clientConfig.Result.ClientId },
                {"WEBSITE_RUN_FROM_AZURE_WEBAPP", "true" },
                {"MICROSOFT_PROVIDER_AUTHENTICATION_SECRET", $"@Microsoft.KeyVault(VaultName=kv-pulumi-{env}-01;SecretName=guid)"}
            }
        });

        var slotConfigNames = new WebAppSlotConfigurationNames($"appsettings-{prefix}", new WebAppSlotConfigurationNamesArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Name = appService.Name,
            Kind = "slotConfigNames",
            AppSettingNames = new InputList<string>
            {
                "SLOT_NAME"
            }
        });

        var credFtp = new WebAppFtpAllowed($"ftpCred-{prefix}", new WebAppFtpAllowedArgs
        {
            Name = appService.Name,
            ResourceGroupName = resourceGroup.Name,
            Allow = true
        });

        var credScm = new WebAppScmAllowed($"scmCred-{prefix}", new WebAppScmAllowedArgs
        {
            Name = appService.Name,
            ResourceGroupName = resourceGroup.Name,
            Allow = true
        });

        var authConfig = new WebAppAuthSettingsV2($"authV2-{prefix}", new WebAppAuthSettingsV2Args
        {
            Name = appService.Name,
            ResourceGroupName = resourceGroup.Name,
            Kind = "authsettingsV2",
            Platform = new AuthPlatformArgs
            {
                Enabled = true
            },
            Login = new LoginArgs
            {
                TokenStore = new TokenStoreArgs
                {
                    Enabled = true
                }
            },
            GlobalValidation = new GlobalValidationArgs
            {
                ExcludedPaths = ["/nosecrets/Path"],
                RedirectToProvider = "azureactivedirectory",
                RequireAuthentication = true,
                UnauthenticatedClientAction = UnauthenticatedClientActionV2.RedirectToLoginPage
            },
            HttpSettings = new HttpSettingsArgs
            {
                RequireHttps = true,
                Routes = new HttpSettingsRoutesArgs
                {
                    ApiPrefix = "/.auth"
                },
                ForwardProxy = new ForwardProxyArgs
                {
                    Convention = ForwardProxyConvention.NoProxy,
                    CustomHostHeaderName = "",
                    CustomProtoHeaderName = ""
                }
            },
            IdentityProviders = new IdentityProvidersArgs
            {
                AzureActiveDirectory = new AzureActiveDirectoryArgs
                {
                    Enabled = true,
                    IsAutoProvisioned = true,
                    Login = new AzureActiveDirectoryLoginArgs
                    {
                        DisableWWWAuthenticate = false
                    },
                    Registration = new AzureActiveDirectoryRegistrationArgs
                    {
                        ClientId = "123-4fe3-4707-b981-123",
                        ClientSecretSettingName = "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET",
                        OpenIdIssuer = $"https://sts.windows.net/{clientConfig.Result.TenantId}/v2.0"
                    },
                    Validation = new AzureActiveDirectoryValidationArgs
                    {
                        AllowedAudiences = "api://123-4fe3-4707-b981-123"
                    }
                }
            },
        });

        var slot = new WebAppSlot($"slot-{prefix}", new WebAppSlotArgs
        {
            Name = appService.Name,
            Slot = slotName,
            ResourceGroupName = resourceGroup.Name,
            Tags = tags,
            Kind = "app",
            PublicNetworkAccess = "Enabled",
            ServerFarmId = aspIds.Apply(z => z[$"asp-{prefix}-{env}-01"]),
            VirtualNetworkSubnetId = subnetIds.Apply(z => z.TryGetValue($"snet-app", out var id)
                        ? id
                        : throw new Exception($"snet-app not found")),
            HttpsOnly = true,
            StorageAccountRequired = false,
            Identity = new ManagedServiceIdentityArgs
            {
                Type = Pulumi.AzureNative.Web.ManagedServiceIdentityType.SystemAssigned
            },
            SiteConfig = new SiteConfigArgs
            {
                MinTlsVersion = "1.2",
                HealthCheckPath = "/health/index.html",
                VnetRouteAllEnabled = true,
                AlwaysOn = false,
                IpSecurityRestrictions = new IpSecurityRestrictionArgs
                {
                    Action = "Allow",
                    IpAddress = allowedIP
                }
            },
        });

        var appSettingsSlot = new WebAppApplicationSettingsSlot($"appsettings-{prefix}", new WebAppApplicationSettingsSlotArgs
        {
            ResourceGroupName = slot.ResourceGroup,
            Name = appService.Name,
            Slot = slotName,
            Kind = "appsettings",
            Properties = new InputMap<string>
            {
                {"WEBSITE_TIME_ZONE" , "Central European Standard Time"},
                {"SLOT_NAME", env.ToUpper()},
                {"WEBSITE_AUTH_AAD_ALLOWED_TENANTS", clientConfig.Result.TenantId },
                {"CURRENT_SUBSCRIPTION_ID", clientConfig.Result.SubscriptionId },
                {"CURRENT_OBJECT_ID", clientConfig.Result.ObjectId },
                {"CURRENT_CLIENT_ID", clientConfig.Result.ClientId },
                {"WEBSITE_RUN_FROM_AZURE_WEBAPP", "true" },
                {"MICROSOFT_PROVIDER_AUTHENTICATION_SECRET", "abc"}
            }
        });

        var appLogs = new InputList<Pulumi.AzureNative.Monitor.Inputs.LogSettingsArgs>();
        foreach (string l in logs)
        {
            appLogs.Add(
                new Pulumi.AzureNative.Monitor.Inputs.LogSettingsArgs
                {
                    Category = l,
                    Enabled = true
                }
            );
        }

        var diagnosticSettings = new DiagnosticSetting($"diag-{prefix}", new DiagnosticSettingArgs
        {
            Name = "diag-app",
            ResourceUri = appService.Id,
            StorageAccountId = stIds.Apply(z => z.TryGetValue($"stpulumiabcdev01", out var id)
                        ? id
                        : throw new Exception($"stpulumiabcdev01 not found")),
            Logs = appLogs
        });

        var pep = new PrivateEndpoint($"pep-{appName}", new PrivateEndpointArgs
        {
            PrivateEndpointName = $"pep-{appName}",
            CustomNetworkInterfaceName = $"nic-{appName}",
            ResourceGroupName = appService.ResourceGroup,
            IpConfigurations = new Pulumi.AzureNative.Network.Inputs.PrivateEndpointIPConfigurationArgs
            {
                GroupId = "sites",
                MemberName = "sites",
                PrivateIPAddress = privateIPAddress,
                Name = $"config"
            },
            Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
            {
                Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                        ? id
                        : throw new Exception($"snet-pep not found"))
            },
            PrivateLinkServiceConnections = new Pulumi.AzureNative.Network.Inputs.PrivateLinkServiceConnectionArgs
            {
                Name = $"config",
                PrivateLinkServiceId = appService.Id,
                GroupIds = new InputList<string>
                        {
                            "sites"
                        }
            }
        });

        var dnszone = new PrivateDnsZoneGroup($"default-{appName}", new PrivateDnsZoneGroupArgs
        {
            Name = $"default",
            ResourceGroupName = appService.ResourceGroup,
            PrivateEndpointName = pep.Name,
            PrivateDnsZoneGroupName = $"privatelink.azurewebsites.net",
            PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
            {
                Name = $"azurewebsites",
                // PrivateDnsZoneId = pdnszId.Apply(z => z[$"privatelink.{p.Key}.core.windows.net"])
                PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.azurewebsites.net", out var id)
                ? id
                : throw new Exception($"Private zone privatelink.azurewebsites.net not found"))
            }
        });

        var pepSlot = new PrivateEndpoint($"pep-{appName}-{slotName}", new PrivateEndpointArgs
        {
            PrivateEndpointName = $"pep-{appName}-{slotName}",
            CustomNetworkInterfaceName = $"nic-{appName}-{slotName}",
            ResourceGroupName = slot.ResourceGroup,
            IpConfigurations = new Pulumi.AzureNative.Network.Inputs.PrivateEndpointIPConfigurationArgs
            {
                GroupId = "sites-stage",
                MemberName = "sites-stage",
                PrivateIPAddress = privateIPAddressSlot,
                Name = $"config"
            },
            Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
            {
                Id = subnetIds.Apply(z => z.TryGetValue($"snet-pep", out var id)
                        ? id
                        : throw new Exception($"snet-pep not found"))
            },
            PrivateLinkServiceConnections = new Pulumi.AzureNative.Network.Inputs.PrivateLinkServiceConnectionArgs
            {
                Name = $"config",
                PrivateLinkServiceId = appService.Id,
                GroupIds = new InputList<string>
                {
                    "sites-stage"
                }
            }
        });

        var dnszoneSlot = new PrivateDnsZoneGroup($"default-{appName}-{slotName}", new PrivateDnsZoneGroupArgs
        {
            Name = $"default",
            ResourceGroupName = slot.ResourceGroup,
            PrivateEndpointName = pepSlot.Name,
            PrivateDnsZoneGroupName = $"privatelink.azurewebsites.net",
            PrivateDnsZoneConfigs = new Pulumi.AzureNative.Network.Inputs.PrivateDnsZoneConfigArgs
            {
                Name = $"azurewebsites",
                PrivateDnsZoneId = pdnszIds.Apply(z => z.TryGetValue($"privatelink.azurewebsites.net", out var id)
                ? id
                : throw new Exception($"Private zone privatelink.azurewebsites.net not found"))
            }
        });

        var rbacKv = new RoleAssignment("rbacKv", new RoleAssignmentArgs
        {
            RoleAssignmentName = GuidX.CreateGuidV3("rbacKv").ToString(),
            Scope = $"subscriptions/{clientConfig.Result.SubscriptionId}/resourceGroups/rg-pulumi-kv-{env}-01",
            PrincipalId = appService.Identity.Apply(x => x != null ? x.PrincipalId : ""),
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = RbacRole.GetRoleIds().Apply(x => x["KeyVaultAdmin"])
        });
    }
}
