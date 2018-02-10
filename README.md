# Planetsino
A sample app using Azure Cosmos DB.

This app demonstrates how to build a web app that uses a globally distributed Azure Cosmos DB setup. Combined with the Azure Traffic Manager, this can give very low latency for users all over the world.

To test the app, do the following:
* Create the necessary services in Azure: at least one Azure Cosmos DB account and at least one Azure Web App. Use another name than Planetsino, since it has to be unique in Azure.
* Edit Web.config with your own EndpointURLs and AuthKeys. Also edit the preferred locations.
* Test run the web app locally in Visual Studio. That will help you resolve any configuration errors.
* Publish the web app to Azure. If you have co-located your Azure Cosmos DB accounts with the Web Apps, you should get very low latency.
* Finally, you can add an Azure Traffic Manager to automatically direct users to the nearest Web App.

Planetsino also contains functionality to do performance testing on your Azure Cosmos DB accounts.


# More reading
Visit [my blog](https://www.johanahlen.info/en/tag/azure-cosmos-db/) for more reading about Azure Cosmos DB


# Screenshots
![Planetsino screenshot 1](/SCREENSHOT1.png?raw=true "Planetsino screenshot 1")
![Planetsino screenshot 2](/SCREENSHOT2.png?raw=true "Planetsino screenshot 2")
