## Extracting Passwords:

To configure the workload cluster VM/s to send logs to service VM you will need passwords, as well as  kibana's and grafana's passwords, which will need to be 
extracted from a secrets file that's generated with the service cluster OVA creation and it's stored in an amazon s3 bucket named **glasswall-dev-sc-logs**.

To download the file:

​	1- Either login to AWS console and navigate to S3 then search for **glasswall-dev-sc-logs** bucket where you will find all the secrets files for all the service clusters OVAs.

​		You will find your specified file tagged with the same run ID as the service cluster OVA you are using.
​		For example: If the service cluster OVA is created with the name **ck8-cs-api-SC-CI-sc-799339985.ova**, the secrets file will be named **secrets-799339985.yaml** 

​	2- Or from your terminal Run the following to setup your AWS credentials 

```bash
export AWS_ACCESS_KEY=<Please replce with you access key>
export AWS_SECRET_ACCESS_KEY=<Please replace with secret key>
export AWS_DEFAULT_REGION=eu-west-1
```

​	Install AWS CLI and then download the secrets file in your current working directory

```bash
apt install awscli -y
aws s3 cp s3://glasswall-dev-sc-logs/secrets-<Replace with run ID as illustrated above>.yaml ./
```

Once you open the file and take note of the following

```bash
Monitoring password -> influxDB.wcWriterPassword
Logging password -> elasticsearch.fluentdPassword
Grafana password -> user.grafanaPassword
Kibana password -> elasticsearch.adminPassword
```
Use the above information to to configure the workload cluster VM/s to send logs to service VM, also login to grafana and Kibana dashboards
