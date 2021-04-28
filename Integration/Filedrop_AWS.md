## Deploying Workload cluster (GW Cloud SDK with compliant kubernetes and filedrop integrated)

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

## Deploying Service cluster

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
- SSH to Workload Cluster VM (`ssh -i yourkey.pem ubuntu@<WC VM IP>`), navigate to `/home/ubuntu` and switch to root by `sudo su`
- **Impotant Note**: Below commands will work just if executed as root user
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
- In case you are missing `wc-coredns-configmap.yml` and `setupscCluster.sh` run: 
   ```
   wget https://raw.githubusercontent.com/k8-proxy/vmware-scripts/cs-api-ck8/packer/wc-coredns-configmap.yml
   wget https://raw.githubusercontent.com/k8-proxy/vmware-scripts/cs-api-ck8/packer/setupscCluster.sh
   ```
- In case you are missing the rest of the files also, create and edit them (using vi/vim) with values as shown below

- Update each text file with corresponding values:
```
    monitoring-username.txt - wcWriter
    monitoring-password.txt - <Add monitoring password>
    logging-username.txt - fluentd
    logging-password.txt - <Add logging password>
    service-cluster.txt - ops.default.compliantkuberetes
    service-cluster-ip.txt - <service-cluster-ip>
    cluster.txt - <Unique Identifier of workload instance> E.g., GWSDKWC01
```
- Change permission of `setupscCluster.sh` by below command:
    `chmod +x setupscCluster.sh`
- Execute setupscCluster by below command:
    `./setupscCluster.sh`
- Wait for all commands to complete. Once completed, login to Grafana and Kibana in service cluster
    - `http://<service-cluster-ip>:5601/  - Kibana`
    ![image](https://user-images.githubusercontent.com/70108899/116088990-afd7cb80-a6a2-11eb-96bf-31d2898b910e.png)
        
    - `http://<service-cluster-ip>:3000/  - Grafana`
    
    ![image](https://user-images.githubusercontent.com/70108899/116088330-0f81a700-a6a2-11eb-970a-a0a4fbbd4823.png)

    Username: `admin`
    Password: `Will be shared as part of delivery`
    

## Testing workflow
- To check API health, from Browser access `<WC VM IP>/api/health` and verify its ok
  ![image](https://user-images.githubusercontent.com/70108899/116484783-179c3b00-a88a-11eb-9c79-c70e10847bed.png)
- To rebuild files, from Browser access Filedrop `<WC VM IP>` and select any file you want to rebuild 
- After file is rebuilt you will be able to download protected file along with XML report
 ![image](https://user-images.githubusercontent.com/70108899/116483290-13225300-a887-11eb-9187-2327fc559a47.png)
- On Managment UI `https://management-ui.glasswall-icap.com/analytics` you will be able to see statistics of rebuild files, your request history and modify policies
 ![image](https://user-images.githubusercontent.com/70108899/116484583-a8264b80-a889-11eb-8cdd-e06627ddf1e8.png)
- To see more details on traffic you are generating you can access Elastic or Grafana
- For Elastic from browser navigate to `http://<SC VM IP>:5601`
   - From settings choose `Discover` and select one of three options for logs (kubespray*, kubernetes* or other*)
     ![image](https://user-images.githubusercontent.com/70108899/116484905-53370500-a88a-11eb-8477-d55c1db73519.png)
   - From settings choose `Dashboard` and select one of two available or create custom one. This option will give you more of a grafical overview compared to `Discover`
     ![image](https://user-images.githubusercontent.com/70108899/116485151-cf314d00-a88a-11eb-99d7-b5a7e1d15a91.png)
- For Grafana from browser navigate to `http://<SC VM IP>:3000`
   - `ck8s-metrics` data set is added and you can use it when creating custom dashbords
     ![image](https://user-images.githubusercontent.com/70108899/116485399-65fe0980-a88b-11eb-84ba-0d4e7d77c379.png)

