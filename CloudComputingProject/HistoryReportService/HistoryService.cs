using Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HistoryReportService
{
    public class HistoryService : IHistoryService
    {
        public HistoryService()
        {

        }

        public List<PlannedWork> GetAllHistoryFromStorage()
        {
            List<PlannedWork> plannedWorks = new List<PlannedWork>();
            CloudStorageAccount _storageAccount;
            CloudTable _table;
            string a = ConfigurationManager.AppSettings["HistoryConnectionString"];
            _storageAccount = CloudStorageAccount.Parse(a);
            CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
            _table = tableClient.GetTableReference("CurrentWorkDataStorage");
            var results = from pwt in _table.CreateQuery<PlannedWorkTable>() where pwt.PartitionKey == "CurrentPlannedWorkData" && pwt.ArchivedData select pwt;
            foreach (PlannedWorkTable plannedWorkTable in results.ToList())
            {
                plannedWorks.Add(new PlannedWork(plannedWorkTable.IdCurrentWork, plannedWorkTable.Airport, plannedWorkTable.TypeOfAirport, plannedWorkTable.DetailsOfWorks, plannedWorkTable.WorkSteps));
            }
            return plannedWorks;
        }

    }
}
