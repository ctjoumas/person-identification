# PersonIdentificationApi

# Dependencies

[Azure AI Face Service](https://learn.microsoft.com/en-us/azure/ai-services/computer-vision/overview-identity)

[Azure Machine Learning](https://learn.microsoft.com/en-us/azure/machine-learning/?view=azureml-api-2)

Azure Machine Learning Model: <b>automl-image-instance-segment-4</b>

<b>Azure SQL</b>

General Purpose - Serverless: Gen5, 1 vCore
  
The database needs to be created manually in the Azure Portal. Then create the tables:

- PersonGroup
- PersonGroupPerson
- PersonGroupPersonFace
