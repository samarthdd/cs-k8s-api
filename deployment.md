# Deploy GW Cloud SDK with compliant kubernetes (worker and service cluster)

![image](https://user-images.githubusercontent.com/58347752/115372624-64697d00-a1cb-11eb-96e0-7f6ba5cdc595.png)

## Deployment instructions using AMI

Both worker and service cluster AMIs are automatically created by running **CK8icap-GW_CloudSDK** workflow in GitHub Actions using Packer.
Amazon CloudFormation launch Template is provided  in the repository to facilitate configuration of the deployment.

Use `Compliant_Kubernetes_ICAP_Service_with_Service_Cluster_Proxy_REST_API.json` CloudFormation template to deploy a number of Workload Clusters behind a loadbalancer and a single Service Cluster.

| Region | Stack |
| --- | --- |
| Ireland | [![Launch Stack](https://s3.amazonaws.com/cloudformation-examples/cloudformation-launch-stack.png)](https://console.aws.amazon.com/cloudformation/home?region=eu-west-1#/stacks/new?stackName=compliant-k8s-stack&templateURL=https://cf-templates-compliant-k8s-eu-west-1.s3-eu-west-1.amazonaws.com/Compliant_Kubernetes_ICAP_Service_with_Service_Cluster_Proxy_REST_API.json) |

* Or click on one of the buttons above depending on the region you want to work on
* When prompted with the image below, click Next

  ![Screenshot from 2021-03-25 17-13-06](https://user-images.githubusercontent.com/7603614/112506566-2fb20380-8d8e-11eb-9476-909cc8a751ed.png)

* Enter `Stack name`

  ![Screenshot from 2021-03-25 17-13-45](https://user-images.githubusercontent.com/7603614/112506657-45bfc400-8d8e-11eb-91a9-59e3c0b558ef.png)

  Set **Credentials**:

  * `Service Cluster Key Name` and `Workload Cluster Key Name` to the names of key pairs (previously uploaded to AWS) that will be allowed to SSH into VMs

    * 

    ![Screenshot from 2021-03-25 17-14-04](https://user-images.githubusercontent.com/7603614/112506741-55d7a380-8d8e-11eb-8627-8427d194eeed.png)

  * `Logging Password`  Fluentd password

  * `Monitoring Password` to plain text of InfluxDB WC writer password

    The **Logging Password** & **Monitoring Password** are the elasticsearch.fluentdPassword & influxDB.wcWriterPassword Values respectively that are generated during the github action run which produced the used service cluster AMI

    ![image](https://user-images.githubusercontent.com/58347752/115334524-d75aff80-a19b-11eb-94da-6784912c8b0a.png)

    

  Make sure that the following configuration parameters for **Load Balancer** are not used by other stacks in the AWS region:

  * `Load Balancer Name`
  * `Target Group Name`
  * `Elastic IP`

  ![Screenshot from 2021-03-25 17-14-34](https://user-images.githubusercontent.com/7603614/112506814-66881980-8d8e-11eb-9658-1a75fc15e043.png)

  

  * **Service Cluster** specification mainly AMI ID and Instance size, make sure the AMI is the one generated in the same github action run where you get the logging and monitory passwords above.

    ![image](https://user-images.githubusercontent.com/58347752/115334763-3de01d80-a19c-11eb-9b90-c51201b81b29.png)

  * **Workload Cluster** specification mainly AMI ID, Instance size and number of instances.

    ![image](https://user-images.githubusercontent.com/58347752/115334920-88619a00-a19c-11eb-9ca6-a0de8f8855e1.png)

  * **Docker Images** used for the Glasswall services (by default latest images will be pre-provided)

* When the stack creation is complete, in the **Outputs** tab you can find:
  * `Load Balancer DNS Name` which stand in-front of the workload cluster instances and accept requests on port `8080`
  * `Service Cluster IP`  that exposes Grafana on port `3000` and Kibana on port `5601`

  ![Screenshot from 2021-03-25 18-05-43](https://user-images.githubusercontent.com/7603614/112513618-ced9f980-8d94-11eb-9559-61cee07e7a93.png)



## Testing

* Testing workload cluster :

  * Send and http request to `<LoadBalancer DNS Name>:8080/api/health`  you should receive `200 OK` which indicates healthy workload cluster 

    ![image](https://user-images.githubusercontent.com/58347752/115336433-877e3780-a19f-11eb-81b7-0ce2b0e70faa.png)

* Testing service cluster :

  * From your browser go to service cluster IP provided in the outputs tab above on port 3000 to access Grafana and on port 5601 to acces Kibana

  * **Grafana's Password** & **Kibana's Password** are the user.grafanaPassword & elasticsearch.adminPassword Values respectively that are generated during the github action run which produced the used service cluster AMI

    ![image](https://user-images.githubusercontent.com/58347752/115337642-a4b40580-a1a1-11eb-9fd3-d13246de3f54.png)