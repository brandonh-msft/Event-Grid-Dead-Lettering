# Event Grid Dead-Lettering sample
This contains a way to test out Event Grid's [new](http://ilikesqldata.com/event-grid-june-updates-dead-lettering-retry-policies-global-availability-and-more/) [dead-lettering and retry policy](https://docs.microsoft.com/en-us/azure/event-grid/manage-event-delivery) capabilities.

## Setup
1. Create an Azure Storage account
2. Add a new container to the Azure Storage account
3. Create a `local.settings.json` file for this Azure Function project with contents:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "DeadLetterBlobStorageConnectionString": "<conn string to storage account>",
    "DeadLetterContainerName": "<name of blob storage container>"
  }
}
```
4. Create a new Event Grid topic via [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/?view=azure-cli-latest):

~~~
az login

rg=<new resource group name>
az group create --name $rg --location westus2

az provider register --namespace Microsoft.EventGrid

topicname=<your-topic-name>

az eventgrid topic create --name $topicname -l westus2 -g $rg
~~~

5. Follow [my instructions for testing event grid with local Functions](https://aka.ms/ngrokFunctions)
6. Create an Event Grid subscription for your newly-hosted local function endpoint (in the previous Azure CLI session):

~~~

storagename=<storage account name>
containername=<storage container name>

storageid=$(az storage account show --name $storagename --resource-group $rg --query id --output tsv)

subscriptionName=<whatever you want here>
functionUrl=<[ngrok endpoint]/api/Function1>

az eventgrid event-subscription create \
  -g gridResourceGroup \
  --topic-name $topicname \
  --name $subscriptionName \
  --endpoint $functionUrl \
  --max-delivery-attempts 3 \
  --deadletter-endpoint $storageid/blobServices/default/containers/$containername
~~~

While this runs, you should see your Function get hit and send back the EG Subscription Validation code; the process should complete successfully.

7. HTTP POST to your Event Grid custom topic

~~~
endpoint=$(az eventgrid topic show --name $topicname -g $rg --query "endpoint" --output tsv)
key=$(az eventgrid topic key list --name $topicname -g $rg --query "key1" --output tsv)

body=$(eval echo "'$(curl https://raw.githubusercontent.com/Azure/azure-docs-json-samples/master/event-grid/customevent.json)'")

curl -X POST -H "aeg-sas-key: $key" -d "$body" $endpoint
~~~

What you should see here is your Function get hit once, throw an exception. Then again 10 seconds later (EG exponential backoff has kicked in). Then again 30 seconds later. Then finally ~5 minutes after that last hit you should see the Dead Letter trigger get fired off.

## Notable notes:
- You can only customize the **number of retries** and the **lifetime of an event**. You cannot set the rate at which retries are fired off from EG to your endpoint.
- The ~5 minute delay from the time of the last failure to the time of dead-lettering is purposeful; it will batch up multiple dead-lettered events and insert them all at once in to Blob Storage if necessary. This is a deliberate optimization and cannot be customized
