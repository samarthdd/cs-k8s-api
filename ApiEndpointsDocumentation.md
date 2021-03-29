## Endpoints

### api/FileTypeDetection/base64

**Description:**
- The endpoint detects the uploaded file type
  
**Request:**

`POST api/FileTypeDetection/base64`

**Headers:**  
- Content-Type: application/json  

**Payload:**  
- base64 encoded file (see [base64.json](./Samples/base64.json))
  
- On success, the output is JSON formated data like in the sample below:  
  
    ```
    {
        "fileTypeName": "pdf",
        "fileSize": 189167
    }
    ```
  
### api/Analyse/base64

**Description:**
- The endpoint analyses the file uploaded for a possibility to rebuild it.

**Request:**

`POST api/Analyse/base64`

**Headers:**  
- Content-Type: application/json  

**Payload:**  
- base64 encoded file (see [base64.json](./Samples/base64.json))
  
- On success, the output is an XML formated report (see [report.xml](./Samples/report.xml))  

### api/Rebuild/base64
**Description:**
- The endpoint rebuilds the file uploaded.
**Request:**

`POST api/Rebuild/base64`
 
**Headers:**  
- Content-Type: application/json  

**Payload:** 
- base64 encoded file (see [base64.json](./Samples/base64.json))
  
- On success, the output is a base64 encoded cleaned up file  

### api/Rebuld/file
**Description:**
- The endpoint rebuilds the file uploaded.

**Request:**

`POST api/Rebuld/file`
 
**Headers:**  
- Content-Type: multipart/form-data

**Payload:**  
- form-data with `file` named field that contains the file binary
  
- On success, the output is the cleaned up file binary
