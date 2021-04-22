# Deploying CS-API-CK8S WC OVA on ESXI

- Download CS-API-CK8S  OVA
- Login to VMware ESXi
- From left bar navigate to Virtual machines
- From top bar on the right choose Create / Register VM
- Choose Deploy a virtual machine from OVF or OVA file
- Type VM name
- Click on section Click to select files or drag/drop and select the downloaded OVA file
- Select desired storage and optionally tweak VM configuration
- Before importing, uncheck the box for Power on automatically
- Click on Actions > Edit Settings > Increase the following (4GB RAM, 2 Cores, 20 GB disk)
- Wait for the import to finish
- Once VM is imported, click on the VM name
- Click on Console in top left corner and select "Open browser console"
- Login to VM using provided credentials
- Change network configuration by running network wizard:
```
wizard
```
- Add IP, Gateway and DNS addresses
  - Note: Use bottom arrow to navigate through these 3 fields and tab to move to Submit button
  - Note: To set up your OVA without internet access, for DNS, enter the same IP as your VM IP.
- Important note: After the IP change wizard will return to the same window, navigate back to CMD by pressing Cancel
- If needed you can use wizard to change password by selecting Change password
- Verify that correct IP address is set by running ip -4 a and verifying IP for eth0
- Give the VM ~10 minutes to initialize, then open your browser and access FileDrop UI on http://<VM IP>





