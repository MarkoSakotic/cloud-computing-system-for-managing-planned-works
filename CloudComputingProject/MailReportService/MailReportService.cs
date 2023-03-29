using System;
using System.Collections.Generic;
using System.Fabric;
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
using Microsoft.ServiceFabric.Services.Runtime;
using Spire.Email.Pop3;
using Spire.Email;

namespace MailReportService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class MailReportService : StatefulService
    {
        public MailReportService(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
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
            int a;

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

                a = await MailServiceFunction();
                if (a > 0)
                {
                    await SendMailsData();
                }
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        public async Task<int> MailServiceFunction()
        {
            try
            {
                Pop3Client pop = new Pop3Client();
                pop.Host = "pop.gmail.com";
                pop.Username = "marcodeee@gmail.com";
                pop.Password = "Am931bb";
                pop.Port = 995;
                pop.EnableSsl = true;
                pop.Connect();
                int numberofMails = pop.GetMessageCount();
                var CurrentMeterActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    for (int i = 1; i <= numberofMails; i++)
                    {
                        MailMessage message = pop.GetMessage(i);
                        string[] mail = message.BodyText.Split(';');
                        try
                        {
                            PlannedWork plannedWork = new PlannedWork();
                            plannedWork.IdCurrentWork = i.ToString();
                            plannedWork.Airport = mail[0];
                            plannedWork.TypeOfAirport = mail[1];
                            plannedWork.DetailsOfWorks = mail[2];
                            plannedWork.WorkSteps = mail[3];
                            await CurrentMeterActiveData.TryAddAsync(tx, plannedWork.IdCurrentWork, plannedWork);
                        }
                        catch
                        {
                            ServiceEventSource.Current.Message("E-mail is wrong formatted!");
                        }
                    }
                    await tx.CommitAsync();
                }
                pop.DeleteAllMessages();
                pop.Disconnect();
                return numberofMails;
            }
            catch
            {
                ServiceEventSource.Current.Message("Email service is not available");
                return 0;
            }

        }
        public async Task SendMailsData()
        {
            try
            {
                var ReportWorkActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
                FabricClient fabricClient = new FabricClient();
                int partitionsNumber = (await fabricClient.QueryManager.GetPartitionListAsync(new Uri("fabric:/CloudComputingProject/ReportWorkService"))).Count;
                var binding = WcfUtility.CreateTcpClientBinding();
                int index = 0;

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerator = (await ReportWorkActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        PlannedWork plannedWork = (await ReportWorkActiveData.TryGetValueAsync(tx, enumerator.Current.Key)).Value;
                        bool result = true;
                        for (int i = 0; i < partitionsNumber; i++)
                        {
                            ServicePartitionClient<WcfCommunicationClient<IReportWorkService>> servicePartitionClient = new ServicePartitionClient<WcfCommunicationClient<IReportWorkService>>(
                                new WcfCommunicationClientFactory<IReportWorkService>(clientBinding: binding),
                                new Uri("fabric:/CloudComputingProject/ReportWorkService"),
                                new ServicePartitionKey(index % partitionsNumber));
                            result = await servicePartitionClient.InvokeWithRetryAsync(client => client.Channel.AddPlannedWork(plannedWork.IdCurrentWork, plannedWork.Airport, plannedWork.TypeOfAirport, plannedWork.DetailsOfWorks, plannedWork.WorkSteps, plannedWork.DateOfRepairWork));
                            index++;
                        }

                        if (result)
                        {
                            await ReportWorkActiveData.TryRemoveAsync(tx, enumerator.Current.Key);
                        }
                    }
                    await tx.CommitAsync();
                }

            }
            catch
            {
                ServiceEventSource.Current.Message("Service is not available!");
            }
        }
    }
}

