using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace HistoryReportService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class HistoryReportService : StatelessService
    {
        public HistoryReportService(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { new ServiceInstanceListener(context => this.CreateInternalListener(context)) };
        }


        private ICommunicationListener CreateInternalListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;

            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("HistoryServiceEndpoint");
            int port = endpointConfig.Port;
            var scheme = endpointConfig.Protocol.ToString();
            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/HistoryServiceEndpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IHistoryService>(
                serviceContext: context,
                wcfServiceObject: new HistoryService(),
                listenerBinding: WcfUtility.CreateTcpListenerBinding(maxMessageSize: 1024 * 1024 * 1024),
                address: new System.ServiceModel.EndpointAddress(uri)
                );

            ServiceEventSource.Current.Message("Listener created!");
            return listener;
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            await SendDataToCoordinator(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await GetDataFromCurrentWork();

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }


        async Task<bool> GetDataFromCurrentWork()
        {
            FabricClient fabricClient = new FabricClient();
            int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/ReportWorkService"))).Count;
            var binding = WcfUtility.CreateTcpClientBinding();
            int index = 0;
            int index2 = 0;
            List<PlannedWork> plannedWorks = new List<PlannedWork>();
            for (int i = 0; i < partitionsNumber; i++)
            {
                ServicePartitionClient<WcfCommunicationClient<IReportWorkService>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IReportWorkService>>(
                    new WcfCommunicationClientFactory<IReportWorkService>(clientBinding: binding),
                    new Uri("fabric:/CloudComputingProject/ReportWorkService"),
                    new ServicePartitionKey(index % partitionsNumber));
                plannedWorks = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.GetAllDataHistory());
                index++;
            }

            if (plannedWorks.Count > 0)
            {
                try
                {
                    CloudStorageAccount _storageAccount;
                    CloudTable _table;
                    string a = ConfigurationManager.AppSettings["DataConnectionString"];
                    _storageAccount = CloudStorageAccount.Parse(a);
                    CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                    _table = tableClient.GetTableReference("WorkDataStorage");
                    foreach (PlannedWork plannedWork in plannedWorks)
                    {
                        PlannedWorkTable plannedWorkTable = new PlannedWorkTable(plannedWork.IdCurrentWork, plannedWork.Airport, plannedWork.TypeOfAirport, plannedWork.DetailsOfWorks, plannedWork.WorkSteps, plannedWork.DateOfRepairWork, true);
                        TableOperation insertOperation = TableOperation.InsertOrReplace(plannedWorkTable);
                        _table.Execute(insertOperation);
                    }
                    bool tempBool = false;
                    for (int i = 0; i < partitionsNumber; i++)
                    {
                        ServicePartitionClient<WcfCommunicationClient<IReportWorkService>> servicePartitionClient2 = new ServicePartitionClient<WcfCommunicationClient<IReportWorkService>>(
                            new WcfCommunicationClientFactory<IReportWorkService>(clientBinding: binding),
                            new Uri("fabric:/CloudComputingProject/ReportWorkService"),
                            new ServicePartitionKey(index2 % partitionsNumber));
                        tempBool = await servicePartitionClient2.InvokeWithRetryAsync(client => client.Channel.DeleteAllData());
                        index2++;
                    }

                    List<PlannedWork> historyData = GetAllHistoricalData();
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
                        bool tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PubHistory(historyData));
                        index1++;
                    }
                }
                catch
                {
                    ServiceEventSource.Current.Message("There is no created cloud!");
                }
            }
            return true;
        }


        public List<PlannedWork> GetAllHistoricalData()
        {
            List<PlannedWork> currentWorks = new List<PlannedWork>();
            try
            {
                CloudStorageAccount _storageAccount;
                CloudTable _table;
                string a = ConfigurationManager.AppSettings["DataConnectionString"];
                _storageAccount = CloudStorageAccount.Parse(a);
                CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                _table = tableClient.GetTableReference("WorkDataStorage");
                var results = from pwt in _table.CreateQuery<PlannedWorkTable>() where pwt.PartitionKey == "CurrentPlannedWorkData" && pwt.ArchivedData select pwt;
                foreach (PlannedWorkTable currentWorkEntity in results.ToList())
                {
                    currentWorks.Add(new PlannedWork(currentWorkEntity.RowKey, currentWorkEntity.Airport, currentWorkEntity.TypeOfAirport, currentWorkEntity.DetailsOfWorks, currentWorkEntity.WorkSteps, currentWorkEntity.DateOfRepairWork));
                }
            }
            catch (Exception e)
            {
                string err = e.Message;
                ServiceEventSource.Current.Message(err);
            }
            return currentWorks;
        }

        
        public async Task SendDataToCoordinator(CancellationToken cancellationToken)
        {
            try
            {
                bool tempPublish = false;
                List<PlannedWork> currentWorks = GetAllHistoricalData();
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
                        tempPublish = await servicePartitionClient1.InvokeWithRetryAsync(client => client.Channel.PubHistory(currentWorks));
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
