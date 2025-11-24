## Commands
```pwsh
az login
$env:PULUMI_CONFIG_PASSPHRASE = "12345678.abc"
pulumi login --local
pulumi new azure-csharp -y
pulumi config set azure-native:location swedencentral
dotnet add package Pulumi.AzureNative
pulumi stack select dev
pulumi preview
pulumi up --yes
pulumi state delete <URN>
```
## Envir
```
$env:AZURE_SUBSCRIPTION_ID = "11111111-1111-1111-1111-111111111111"
$env:AZURE_TENANT_ID       = "22222222-2222-2222-2222-222222222222"  # optional
$env:AZURE_CLIENT_ID       = "..."   # only if using service principal
$env:AZURE_CLIENT_SECRET   = "..."   # only if using service principal
```