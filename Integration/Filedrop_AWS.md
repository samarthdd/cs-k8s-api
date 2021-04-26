## Deploying GW Cloud SDK with compliant kubernetes and filedrop integrated

- Navigate to AWS > AMIs
- Search for the AMI with specific ID (make sure you are in the correct region)
- Instance can be launch from AMIs or EC2 space
    - From AMIs workspace click on specific AMI > Choose `Launch` 
    - Set instance type to `t3.xlarge` (4CPUs and 16GB RAM)
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

## Deploying Service cluster

- Navigate to AWS > AMIs
- Search for the AMI with specific ID (make sure you are in the correct region)
- Instance can be launch from AMIs or EC2 space
    - From AMIs workspace click on specific AMI > Choose `Launch` 
    - Set instance type to `t2.large` (2CPUs and 8GB RAM)
    - Skip configuring Instance details and adding the storage (that can be left default if not specified differently)
    - Add any tags if needed
    - Security Group: 
      - Create a new security group > Add Rule:
        - HTTP > Port 80 
        - HTTPS > Port 443 
        - SSH > Port 22
    - Click on `Review and Launch`
    - Select `Create or use existing key pair` [Note: Your key pair is important for SSH]
    - Wait for instance to be initialized (~10 minutes) and use public IP to access File Drop web interface

## Instructions to integrate Service Cluster and Workload Cluster of Complaint K8 Cloud SDK
- Login to virtual machine using SSH and navigate to `/home/ubuntu` and switch to root by `sudo su`
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
    `http://<service-cluster-ip>:5601/  - Kibana`
    `http://<service-cluster-ip>:3000/  - Grafana`

    Username: `admin`
    Password: `Will be shared as part of delivery`
