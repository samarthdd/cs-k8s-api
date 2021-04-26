## Deploying GW Cloud SDK with compliant kubernetes and filedrop integrated

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
    - Wait for instance to be initialized (~10 minutes) and use public IP to access File Drop web interface from your Browser
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
    - Wait for instance to be initialized (~10 minutes) and use public IP to access File Drop web interface

## Instructions to integrate Service Cluster and Workload Cluster of Complaint K8 Cloud SDK
- Login to GW SDK CK8s (with Filedrop integrated) CM using SSH and navigate to `/home/ubuntu` and switch to root by `sudo su`
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
- In case you are missing `wc-coredns-configmap.yml`, `setupscCluster.sh` run: 
   ```
   wget https://raw.githubusercontent.com/k8-proxy/vmware-scripts/cs-api-ck8/packer/wc-coredns-configmap.yml
   wget https://raw.githubusercontent.com/k8-proxy/vmware-scripts/cs-api-ck8/packer/setupscCluster.sh
   ```
- In case you are missing the rest of the files also create and edit them (using vi/vim) with values as shown below

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
