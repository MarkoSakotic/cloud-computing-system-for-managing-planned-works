using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace ReportWorkService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class ReportWorkService : StatefulService
    {
        ReportSaver myReportSaver;

        public ReportWorkService(StatefulServiceContext context)
            : base(context)
        {
            myReportSaver = new ReportSaver(this.StateManager);
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[] { new ServiceReplicaListener(context => this.CreateInternalListener(context)) };
        }


        private ICommunicationListener CreateInternalListener(ServiceContext context)
        {
            EndpointResourceDescription internalEndpoint = context.CodePackageActivationContext.GetEndpoint("ReportingServiceEndpoint");
            string uriPrefix = String.Format(
                   "{0}://+:{1}/{2}/{3}-{4}/",
                   internalEndpoint.Protocol,
                   internalEndpoint.Port,
                   context.PartitionId,
                   context.ReplicaOrInstanceId,
                   Guid.NewGuid());
            string nodeIP = FabricRuntime.GetNodeContext().IPAddressOrFQDN;
            string uriPublished = uriPrefix.Replace("+", nodeIP);
            return new WcfCommunicationListener<IReportWorkService>(context, myReportSaver, WcfUtility.CreateTcpListenerBinding(), uriPrefix);
        }


        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            var CurrentReportActive = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");

            await ReadFromTable();

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            await SendDataToCoordinator(cancellationToken);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await AddToTable();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }


        public async Task ReadFromTable()
        {
            try
            {
                CloudStorageAccount _storageAccount;
                CloudTable _table;
                string appSettingString = ConfigurationManager.AppSettings["DataConnectionString"];
                _storageAccount = CloudStorageAccount.Parse(appSettingString);
                CloudTableClient tableCloudClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                _table = tableCloudClient.GetTableReference("WorkDataStorage");

                var results = from pwt in _table.CreateQuery<PlannedWorkTable>() where pwt.PartitionKey == "CurrentPlannedWorkData" && !pwt.ArchivedData select pwt;

                if (results.ToList().Count > 0)
                {
                    var CurrentReportData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        foreach (PlannedWorkTable currentReport in results.ToList())
                        {
                            await CurrentReportData.TryAddAsync(tx, currentReport.RowKey, new PlannedWork(currentReport.RowKey, currentReport.Airport, currentReport.TypeOfAirport, currentReport.DetailsOfWorks, currentReport.WorkSteps, currentReport.DateOfRepairWork));
                        }
                        await tx.CommitAsync();
                    }
                }
            }
            catch
            {
                ServiceEventSource.Current.Message("There is no created cloud!");
            }
        }

        public async Task AddToTable()
        {
            List<PlannedWorkTable> plannedWorkTableEntities = new List<PlannedWorkTable>();
            var CurrentWorkActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");

            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await CurrentWorkActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    PlannedWork plannedWork = (await CurrentWorkActiveData.TryGetValueAsync(tx, enumerator.Current.Key)).Value;
                    plannedWorkTableEntities.Add(new PlannedWorkTable(plannedWork.IdCurrentWork, plannedWork.Airport, plannedWork.TypeOfAirport, plannedWork.DetailsOfWorks, plannedWork.WorkSteps, plannedWork.DateOfRepairWork, false));
                }
            }

            try
            {
                CloudStorageAccount _storageAccount;
                CloudTable _table;
                string appSettingString = ConfigurationManager.AppSettings["DataConnectionString"];
                _storageAccount = CloudStorageAccount.Parse(appSettingString);
                CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
                _table = tableClient.GetTableReference("WorkDataStorage");
                foreach (PlannedWorkTable plannedWorkTable in plannedWorkTableEntities)
                {
                    TableOperation insertOperation = TableOperation.InsertOrReplace(plannedWorkTable);
                    _table.Execute(insertOperation);
                }
            }
            catch
            {
                ServiceEventSource.Current.Message("There is no created cloud!");
            }
        }

        public async Task SendDataToCoordinator(CancellationToken cancellationToken)
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
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
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
