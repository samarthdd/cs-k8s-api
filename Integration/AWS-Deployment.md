# Deploying compliant k8s Workload cluster 

- Navigate to AWS > AMIs
- Search for the AMI with specific ID (make sure you are in the correct region)
- Instance can be launch from AMIs or EC2 space
    - From AMIs workspace click on specific AMI > Choose `Launch` 
    - Set instance type to `t3.2xlarge` (8CPUs and 32GB RAM)
    - Skip configuring Instance details and adding the storage (that can be left default if not specified differently)
    - Add any tags if needed
    - Security Group: 
      - Create a new security group > Add Rule:
        - HTTP > Port 80 
        - HTTPS > Port 443 
        - Custom TCP > Port 8080
        - SSH > Port 22
    - Click on `Review and Launch`
    - Select `Create or use existing key pair` [Note: Your key pair is important for SSH]
    - **Important Note**: Wait for instance to be initialized ~10 minutes
    - Use VM public IP to access File Drop web interface from your Browser
    - To access Management UI in your hosts file add `<VM IP> management-ui.glasswall-icap.com` and access it from your Browser `https://management-ui.glasswall-icap.com/login`

# Deploying compliant k8s Service cluster

- Navigate to AWS > AMIs
- Search for the AMI with specific ID (make sure you are in the correct region)
- Instance can be launch from AMIs or EC2 space
    - From AMIs workspace click on specific AMI > Choose `Launch` 
    - Set instance type to `t3.xlarge` (8CPUs and 32GB RAM)
    - Skip configuring Instance details and adding the storage (that can be left default if not specified differently)
    - Add any tags if needed
    - Security Group: 
      - Create a new security group > Add Rule:
        - HTTP > Port 80 
        - HTTPS > Port 443 
        - SSH > Port 22
        - Custom TCP > Port 3000
        - Custom TCP > Port 5601
    - Click on `Review and Launch`
    - Select `Create or use existing key pair` [Note: Your key pair is important for SSH]
    - **Important Note**: Wait for instance to be initialized ~10 minutes

## Instructions to integrate Service Cluster and Workload Cluster of Complaint K8 Cloud SDK

### Prerequisites

- URL/ID of the Workflow cluster
- URL/ID of the Service cluster
- The following passwords (click [here](https://github.com/k8-proxy/cs-k8s-api/blob/main/Integration/Password-extraction) for instructions on how to extract the passwords)
  - Monitoring password
  - Logging password
  - Kibana password
  - Grafana password
  
### The following steps are needed to configure the workload cluster VM/s to send logs to service VM

- SSH to Workload Cluster VM (`ssh -i yourkey.pem ubuntu@<WC VM IP>`), navigate to `/home/ubuntu` and switch to root by `sudo su`

- **Impotant Note**: Below commands will work only if executed as root user

- Verify presence of below files by issuing command `ls`

   ```
    /home/ubuntu/monitoring-username.txt
    /home/ubuntu/monitoring-password.txt
    /home/ubuntu/logging-username.txt
    /home/ubuntu/logging-password.txt
    /home/ubuntu/service-cluster.txt
    /home/ubuntu/service-cluster-ip.txt
    /home/ubuntu/cluster.txt
    /home/ubuntu/wc-coredns-configmap.yml
    /home/ubuntu/setupscCluster.sh
    ```
- In case you are missing `setupscCluster.sh` run: 

   ```
   wget https://raw.githubusercontent.com/k8-proxy/vmware-scripts/cs-api-ck8/packer/setupscCluster.sh
   ```
- In case you are missing the rest of the files also, create and edit them using commands below. **Note: Please replace placeholders**

 ```
  echo "wcWriter" > monitoring-username.txt
  echo "<Add monitoring password>" > monitoring-password.txt
  echo "fluentd" > logging-username.txt
  echo "<Add logging password>" > logging-password.txt
  echo "ops.default.compliantkuberetes" > service-cluster.txt
  echo "<service-cluster-ip>" > service-cluster-ip.txt
  echo "<Unique Identifier of workload instance E.g., GWSDKWC01>" > cluster.txt
 ```
 
 - Currently the below files need to be manually configured using `vi/vim`:
 ```
/home/ubuntu/service-cluster-ip.txt  #Add IP of service cluster
/home/ubuntu/monitoring-password.txt #Add monitoring password
/home/ubuntu/logging-password.txt    #Add logging password
/home/ubuntu/cluster.txt             #Add Unique Identifier of workload instance E.g., GWSDKWC01
 ```

- Change permission of *setupscCluster.sh* running command: `chmod +x setupscCluster.sh`

- Execute *setupscCluster.sh* running command **NOTE: Make sure you run the following script as root user** : `./setupscCluster.sh`
    
- Wait for all commands to complete. Once completed, login to Grafana and Kibana using service cluster IP address on ports 5601 for grafana and port 3000 for kibana

    - `http://<service-cluster-ip>:5601/` >> For Kibana
    
        ![image](https://user-images.githubusercontent.com/70108899/116088990-afd7cb80-a6a2-11eb-96bf-31d2898b910e.png)
        
    - `http://<service-cluster-ip>:3000/` >> For grafana
    
        ![image](https://user-images.githubusercontent.com/70108899/116088330-0f81a700-a6a2-11eb-970a-a0a4fbbd4823.png)

    Username: `admin`
    Password: `Will be shared as part of delivery` or can be extracted from the secrets file mentioned above
    

## Testing workflow
- To check API health, from Browser access `<WC VM IP>/api/health` and verify its ok

    ![image](https://user-images.githubusercontent.com/70108899/116484783-179c3b00-a88a-11eb-9c79-c70e10847bed.png)
  
- To rebuild files, from Browser access Filedrop `<WC VM IP>` and select any file you want to rebuild 
- After file is rebuilt you will be able to download protected file along with XML report

    ![image](https://user-images.githubusercontent.com/70108899/116483290-13225300-a887-11eb-9187-2327fc559a47.png)
    
- To access the management UI for the workload cluster, add the following  line to your hosts file.

  ```bash
  <WORKLOAD CLUSTER IP ADDRESS> management-ui.glasswall-icap.com  
  ```
    
- On Managment UI `https://management-ui.glasswall-icap.com/analytics` you will be able to see statistics of rebuild files, your request history and modify policies

    ![image](https://user-images.githubusercontent.com/70108899/116484583-a8264b80-a889-11eb-8cdd-e06627ddf1e8.png)
    
- To see more details on traffic you are generating you can access Elastic or Grafana
- For Elastic from browser navigate to `http://<SC VM IP>:5601`
   - From settings choose `Discover` and select one of three options for logs (kubeaudit*, kubernetes* or other*)
   
        ![image](https://user-images.githubusercontent.com/70108899/116484905-53370500-a88a-11eb-8477-d55c1db73519.png)
        
   - From settings choose `Dashboard` and select one of two available or create custom one. This option will give you more of a grafical overview compared to `Discover`
   
        ![image](https://user-images.githubusercontent.com/70108899/116485151-cf314d00-a88a-11eb-99d7-b5a7e1d15a91.png)
     
- For Grafana from browser navigate to `http://<SC VM IP>:3000`

   - Click on `Search` and type `Kubernetes / Compute Resources / Namespace (Pods)` and select the dashboard from search result

        ![image](https://user-images.githubusercontent.com/64204445/116515131-85c41a80-a8e9-11eb-9d98-cf26f9b6f4e4.png)
        
   - Here you can switch between Workload clusters and also namespaces to see metrics
   
        ![image](https://user-images.githubusercontent.com/64204445/116515563-14d13280-a8ea-11eb-900b-58fe934cad07.png)


   - `ck8s-metrics` data set is added and you can use it when creating custom dashbords
  
        ![image](https://user-images.githubusercontent.com/70108899/116485399-65fe0980-a88b-11eb-84ba-0d4e7d77c379.png)

