﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using ReportWorkService;
using WebClient.Models;
using Common;

namespace WebClient.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        
        [HttpPost]
        [Route("/HomeController/AddPlannedWork")]
        public async Task<IActionResult> AddPlannedWork(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWorks, string workSteps)
        {

            try
            {
                bool result = true;
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/ReportWorkService"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();
                int index = 0;
                for (int i = 0; i < partitionsNumber; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<IReportWorkService>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IReportWorkService>>(
                        new WcfCommunicationClientFactory<IReportWorkService>(clientBinding: binding),
                        new Uri("fabric:/CloudComputingProject/ReportWorkService"),
                        new ServicePartitionKey(index % partitionsNumber));
                    result = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.AddPlannedWork(idCurrentWork, airport, typeOfAirport, detailsOfWorks, workSteps));
                    index++;
                }

                if (result)
                {
                    ViewData["Title"] = "New work ADDED successfully!";
                }
                else
                {
                    ViewData["Title"] = "New work NOT added successfully!";
                }

                return View("Index");
            }
            catch
            {
                ViewData["Title"] = "New work NOT added successfully!";
                return View("Index");
            }
            
        }


        public async Task<IActionResult> Contact()
        {
            ViewData["Contact"] = null;
            List<PlannedWork> plannedWorks = new List<PlannedWork>();

            try
            {
                FabricClient fabricClient1 = new FabricClient();
                int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/ReportWorkService"))).Count;
                var binding1 = WcfUtility.CreateTcpClientBinding();
                int index1 = 0;
                for (int i = 0; i < partitionsNumber1; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<IReportWorkService>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<IReportWorkService>>(
                        new WcfCommunicationClientFactory<IReportWorkService>(clientBinding: binding1),
                        new Uri("fabric:/CloudComputingProject/ReportWorkService"),
                        new ServicePartitionKey(index1 % partitionsNumber1));
                    plannedWorks = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.GetAllData());
                    index1++;
                }
                return View(plannedWorks);
            }
            catch
            {
                ViewData["Contact"] = "Service is not available currently";
                return View();
            }
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
