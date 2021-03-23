# C# Rest API for ICAP Service Kubernetes cluster

## Archetecture

## Implemented endpoints



#### api/FileTypeDetection/base64

The endpoint accepts HTTP POST requests with:  

Headers:  
- Content-Type: application/json  

Payload:  
- base64 encoded file (see [Sample data](./Samples/base64.json))
  
On success the output is JSON formated data like in the sample below:  
  
```
{
    "fileTypeName": "pdf",
    "fileSize": 189167
}
```
  
#### api/Analyse/base64

#### api/Rebuild/base64

### api/Rebuld/file



## Docker build

## Deployment within ICAP Service cluster