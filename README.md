# Adliance AzureTools

[![Build Status](https://dev.azure.com/adliance/AzureTools/_apis/build/status/Adliance.AzureTools?branchName=master)](https://dev.azure.com/adliance/AzureTools/_build/latest?definitionId=93&branchName=master)
[![NuGet](https://img.shields.io/nuget/v/Adliance.AzureTools.svg)](https://www.nuget.org/packages/Adliance.AzureTools/)

## What is AzureTools
AzureTools is a set of common operations for Azure that we use on a daily basis.

## Installation

````
dotnet tool install -g Adliance.AzureTools
````

## Operations

### Mirroring storage
Mirrors an Azure Storage account (BLOB storage) either to another Storage account, or to the local file system, or vice versa.

````
azuretools mirror-storage-to-local --source "DefaultEndpointsProtocol=https; AccountName=my_account; AccountKey=my_key; EndpointSuffix=core.windows.net" --target "c:\my_local_directory"
````

Additionaly, these commands are available for all combinations of storage/local:
- `mirror-local-to-storage`
- `mirror-storage-to-storage`
- `mirror-local-to-local`

### Copying Azure SQL database
Please note that this command uses `sqlpackage` internally. While `sqlpackage` is packaged with AzureTools, currently only the Windows version of `sqlpackage` is included, so currently this command only works on Windows.

````
azuretools copy-database --source "Server=tcp:myserver.database.windows.net,1433; Initial Catalog=my_db; Persist Security Info=False; User ID=my_user; Password=my_pass;" --target "Data Source=(localdb)\MSSQLLocalDB; Initial Catalog=my_local_db; Integrated Security=true;"
````

#### Additional parameters

- `--elastic-pool "<elastic_pool_name>"`: Sets the Azure SQL elastic pool of the target database after it has been restored
- `--use-local-backpac`: Each database gets downloaded to database-specific, local BACPAC to the user profile directory. If this parameter is set, and the BACPAC file exists locally, no new BACPAC will be downloaded and the local BACPAC will be restored.
- `--force`: Force copy-database operation without any user confirmation or interaction.
