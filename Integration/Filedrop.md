# Deploying CS-API-CK8S-WC OVA on ESXI

1.- Download OVA: https://glasswall-sow-ova.s3-eu-west-1.amazonaws.com/vms/csapi-ck8icap/ck8-cs-api-WC-CI-wc-773653164.ova

2.- Deploy OVA on VMware ESXi
  - From left bar navigate to Virtual machines
  - From top bar on the right choose Create / Register VM
  - Choose Deploy a virtual machine from OVF or OVA file
  - Type VM name
  - Click on section Click to select files or drag/drop and select the downloaded OVA file
  - Select desired storage and optionally tweak VM configuration
  - Before importing, uncheck the box for Power on automatically
  - Click on Actions > Edit Settings > Increase the following (16GB RAM, 8 Cores, 50 GB disk)
  - Wait for the import to finish
  - Once VM is imported, click on the VM name

![image](https://user-images.githubusercontent.com/64204445/115719302-96c8d500-a399-11eb-8d6e-c8a506ed22c7.png)

3.- Configure IP and credentials
  - Click on Console in top left corner and select "Open browser console"
  - Login to VM using provided credentials
  - Change network configuration by running network wizard:
```
wizard
```
  - Add IP, Gateway and DNS addresses
    - Note: Use bottom arrow to navigate through these 3 fields and tab to move to Submit button
    - Note: To set up your OVA without internet access, for DNS, enter the same IP as your VM IP.

![image](https://user-images.githubusercontent.com/64204445/115719636-ec04e680-a399-11eb-8ec0-3a37b1c5d267.png)

![image](https://user-images.githubusercontent.com/64204445/115719445-b7912a80-a399-11eb-9e38-619c622fb2e5.png)

- Important note: After the IP change wizard will return to the same window, navigate back to CMD by pressing Cancel
- If needed you can use wizard to change password by selecting Change password
- Verify that correct IP address is set by running ip -4 a and verifying IP for eth0

4.- Launch FileDrop
  - Give the VM ~10 minutes to initialize, then open your browser and access FileDrop UI on http://<VM IP>

![image](https://user-images.githubusercontent.com/64204445/115719738-03dc6a80-a39a-11eb-93d0-39597d65e6ee.png)




