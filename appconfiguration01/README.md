## Commands
```pwsh
az login
pulumi login --local
pulumi new azure-csharp -y -s dev
pulumi config set azure-native:location swedencentral
pulumi stack init prod
pulumi stack select prod
pulumi stack export
pulumi refresh --yes
pulumi preview --diff --neo
pulumi up --yes --diff --neo
pulumi state delete <URN>
pulumi destroy --stack prod
dotnet add package Pulumi.AzureNative
```
## Environment variables
```pwsh
$env:PULUMI_CONFIG_PASSPHRASE = "12345678.abc"
$env:AZURE_SUBSCRIPTION_ID = "11111111-1111-1111-1111-111111111111"
$env:AZURE_TENANT_ID       = "22222222-2222-2222-2222-222222222222"  # optional
$env:AZURE_CLIENT_ID       = "..."   # only if using service principal
$env:AZURE_CLIENT_SECRET   = "..."   # only if using service principal
```
#### https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/create-private-link-access-commands?tabs=azure-cli
<img style="width:1000px" src="https://learn.microsoft.com/en-us/azure/includes/media/resource-manager-create-rmpl/resource-management-private-link.svg"/>