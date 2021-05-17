## This document explains different scenarios that may occure when rebuilding ZIP files with Compliant Kubernetes

- ### Scenario-1 (Status 200)
All files are processed and the response returns a ZipFile with the sanitized files inside

- ### Scenario-2 (Status 207)
In this scenario the Zip file is processed, however some of the files inside it could not be processed. Nevertheless the response will return a Zip file with the sanitized files
plus an `ErrorReport.html` for each file that could not be rebuilded.

Example of the output of the `ErrorReport.html` is:

![image](https://user-images.githubusercontent.com/6232283/118471256-d8823c80-b707-11eb-81c8-9ae51204a387.png)
