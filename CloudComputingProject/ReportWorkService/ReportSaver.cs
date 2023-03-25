using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
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


        public async Task<bool> AddPlannedWork(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWorks, string workSteps)
        {
            bool result = true;
            CurrentReportDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("CurrentReportActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                result = await CurrentReportDictionary.TryAddAsync(tx, idCurrentWork, new PlannedWork(idCurrentWork, airport, typeOfAirport, detailsOfWorks, workSteps));
                await tx.CommitAsync();
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

    }
}