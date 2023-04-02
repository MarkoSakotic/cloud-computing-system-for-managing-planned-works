# cloud-computing-system-for-managing-planned-works
Cloud computing in infrastructure systems - System for managing planned works (on airplanes)


## Overview
This is a cloud application, which is used to record planned works on airplanes. The application is based on a microservice architecture. First, it is necessary to enter the work on the page for entering works. After that, the work is written to azure table storage. After that, the work appears in the summary with other works, in which it is located until the date for its repair has passed. After that, it is included in the historical summary. The integration with the email service has been arranged, so that based on the received email, a new work can be entered into the table. Also, there is a Pub/Sub service.
<br>
Used C# programming language, .NET Framework, Azure table storage for database, MVC pattern, which is also used for frontend and architecture is based on a microservice.
 
## Requirements

* Visual Studio 2017
* Install Service Fabric (Microsoft Azure Service Fabric v 8.2.1235 and Microsoft Azure Service Fabric SDK v 5.2.1235)
