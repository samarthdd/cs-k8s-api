 
# Deploying OVA in GCP

```
WC OVA : s3://glasswall-sow-ova/vms/csapi-ck8icap/ck8-cs-api-WC-CI-wc-841981802.ova
SC OVA : s3://glasswall-sow-ova/vms/csapi-ck8icap/ck8-cs-api-SC-CI-sc-841981802.ova
```

## Pre requisites 
Google cloud sdk - https://cloud.google.com/sdk/docs/install

### Steps:
- Download OVA 
- Upload it to GCP Storage
- Deploy OVA using below command from terminal

    ```

      gcloud compute instances import glasswall-wc --source-uri=<ova link>                     --zone=us-central1-a --machine-type=n1-standard-8

    ```	
- Create network configuration

    ```        
        sudo vim  /etc/netplan/20-internal-network.yaml 
        
        network:
        version: 2
        ethernets:
        "lo:0":
        match:
            name: lo
        dhcp4: false
        addresses:
        - 172.17.0.100/32
    ```

    ```
    sudo netplan apply
    ```

- Execute below command to configure `KUBECONFIG`

    ```
    sudo cp -i /etc/kubernetes/admin.conf $HOME/.kube/config
    sudo chown $(id -u):$(id -g) $HOME/.kube/config
    export KUBECONFIG=.kube/config
    ```


## Creating google image from VM instances

- Go to images and click on Create image

![image](https://user-images.githubusercontent.com/60857664/120461513-5ac26000-c39a-11eb-8455-63a1d96f5644.png)



- Select appropriate name, source and source disk and click on create image
	
![image](https://user-images.githubusercontent.com/60857664/120461713-86dde100-c39a-11eb-9e52-a43929b07ad8.png)


## Sharing custom images publicly
- Prerequisites - User must have Compute Image User role

    `roles/compute.imageUser`

- Run below command by replacing <image_name> to required image to share image to public

    ```
    gcloud compute images add-iam-policy-binding <image_name> \
        --member='allAuthenticatedUsers' \
        --role='roles/compute.imageUser'
    ```

## Deploying Google Images
 
- WC image name  = cs-api-sc-802
- SC image  name  = cs-api-sc-802

## Deploying Google Images from UI

### Steps

- Go to Images, choose the WC image and click on create instance
	
    ![image](https://user-images.githubusercontent.com/60857664/120462104-e4722d80-c39a-11eb-91fc-061e0a652963.png)


    ![image](https://user-images.githubusercontent.com/60857664/120462236-053a8300-c39b-11eb-862b-d8fbdbe7d1af.png)






- Choose below configuration
    - Name - Any identifier name
    - Region - Required region
    - Machine configuration:
    - Machine family  - General Purpose
    - Series                - n1
    - Machine type     - n1-standard-8

    
    ![image](https://user-images.githubusercontent.com/60857664/120462417-374be500-c39b-11eb-8ef0-dc73a250e472.png)


        
- After setting up the above configuration, click on create.

    ![image](https://user-images.githubusercontent.com/60857664/120462522-50ed2c80-c39b-11eb-9782-70bad6c28c63.png)
         
- Repeat the same procedure to deploy SC google image
- ssh into WC using command using given username and password

    `ssh <username>@<IP>`



## Deploying Google Images from Google Cloud SDK

- Run below command from terminal to set zone and region

    ```
    gcloud config set compute/zone <zone>
    gcloud config set compute/region <region>
    ```

    Example: 

    ```
    gcloud config set compute/zone us-central1-a
    gcloud config set compute/region us-central1
    ```

- Deploy google image
    ```
    gcloud compute instances create <instance_name> \
        --image=<image_name> \
        --image-project=<project_name> \
        --machine-type=n1-standard-8
    ```

- Repeat the same procedure to deploy SC google image
- ssh into WC using command using given username and password

    `ssh <username>@<IP>` 

## Integration of Workload cluster and Service Cluster

- Use the manual steps explained in below link to integrate WC and SC instances

https://k8-proxy.github.io/k8-proxy-documentation/docs/products/gw%20cloud%20sdk/c-fd-integration-aws/#instructions-to-integrate-service-cluster-and-workload-cluster-of-complaint-k8-cloud-sdk

 

