using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportWorkService
{
    public class ReportSaver : IReportWorkService
    {
        IReliableDictionary<string, PlannedWork> CurrentReportDictionary;
        IReliableStateManager StateManager;


        public ReportSaver()
        {

        }

        public ReportSaver(IReliableStateManager stateManager)
        {
            StateManager = stateManager;
        }


        public async Task<bool> AddPlannedWork(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWorks, string workSteps, DateTime dateOfRepairWork)
        {
            bool result = true;
            CurrentReportDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                result = await CurrentReportDictionary.TryAddAsync(tx, idCurrentWork, new PlannedWork(idCurrentWork, airport, typeOfAirport, detailsOfWorks, workSteps, dateOfRepairWork));
                await tx.CommitAsync();
            }

            List<PlannedWork> plannedWorks = await GetAllData();

            FabricClient fabricClient = new FabricClient();
            int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/PubSubReport"))).Count;
            var binding = WcfUtility.CreateTcpClientBinding();
            int index = 0;
            for (int i = 0; i < partitionsNumber; i++)
            {
                ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                    new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding),
                    new Uri("fabric:/CloudComputingProject/PubSubReport"),
                    new ServicePartitionKey(index % partitionsNumber));
                bool tempPublish = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.PubActive(plannedWorks));
                index++;
            }


            return result;
        }

        public async Task<List<PlannedWork>> GetAllData()
        {
            List<PlannedWork> currentWorks = new List<PlannedWork>();
            CurrentReportDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentReportDictionary.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    currentWorks.Add(enumerator.Current.Value);
                }
            }

            return currentWorks;
        }

        public async Task<bool> DeleteAllData()
        {
            CurrentReportDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentReportDictionary.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    if (enumerator.Current.Value.DateOfRepairWork < DateTime.Now)
                    {
                        await CurrentReportDictionary.TryRemoveAsync(tx, enumerator.Current.Key);
                    }
                }
                await tx.CommitAsync();
            }


            FabricClient fabricClient1 = new FabricClient();
            int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/PubSubReport"))).Count;
            var binding1 = WcfUtility.CreateTcpClientBinding();
            int index1 = 0;
            for (int i = 0; i < partitionsNumber1; i++)
            {
                ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                    new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding1),
                    new Uri("fabric:/CloudComputingProject/PubSubReport"),
                    new ServicePartitionKey(index1 % partitionsNumber1));
                bool tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PubActive(new List<PlannedWork>()));
                index1++;
            }
            await SendDataToCoordinator();


            return true;
        }


        public async Task<List<PlannedWork>> GetAllDataHistory()
        {
            List<PlannedWork> plannedWorks = new List<PlannedWork>();

            CurrentReportDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentReportDictionary.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    if (enumerator.Current.Value.DateOfRepairWork < DateTime.Now)
                    {
                        plannedWorks.Add(enumerator.Current.Value);
                    }
                }
            }


            return plannedWorks;
        }


        public async Task SendDataToCoordinator()
        {
            try
            {
                bool tempPublish = false;
                List<PlannedWork> plannedWorks = new List<PlannedWork>();
                var CurrentWorkDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerator = (await CurrentWorkDict.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        plannedWorks.Add(enumerator.Current.Value);
                    }
                }
                FabricClient fabricClient1 = new FabricClient();
                int partitionsNumber1 = (await fabricClient1.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/PubSubReport"))).Count;
                var binding1 = WcfUtility.CreateTcpClientBinding();
                int index1 = 0;
                for (int i = 0; i < partitionsNumber1; i++)
                {
                    ServicePartitionClient<WcfCommunicationClient<IPubSubService>> servicePartitionClient1 = new ServicePartitionClient<WcfCommunicationClient<IPubSubService>>(
                        new WcfCommunicationClientFactory<IPubSubService>(clientBinding: binding1),
                        new Uri("fabric:/CloudComputingProject/PubSubReport"),
                        new ServicePartitionKey(index1 % partitionsNumber1));
                    while (!tempPublish)
                    {
                        tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PubActive(plannedWorks));
                    }
                    index1++;
                }
            }
            catch (Exception e)
            {
                string err = e.Message;
                ServiceEventSource.Current.Message(err);
            }
        }
    }
}