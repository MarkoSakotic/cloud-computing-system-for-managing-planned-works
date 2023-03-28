using Common;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PubSubReport
{
    public class PubSubService : IPubSubService
    {
        IReliableStateManager StateManager;
        IReliableDictionary<string, PlannedWork> ActiveData;
        IReliableDictionary<string, PlannedWork> HistoryData;


        public PubSubService()
        {

        }

        public PubSubService(IReliableStateManager stateManager)
        {
            StateManager = stateManager;
        }


        public async Task<List<PlannedWork>> GetActiveData()
        {
            List<PlannedWork> plannedWorks = new List<PlannedWork>();
            ActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("ActiveData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await ActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    plannedWorks.Add(enumerator.Current.Value);
                }
            }
            return plannedWorks;
        }

        public async Task<List<PlannedWork>> GetHistoryData()
        {
            List<PlannedWork> plannedWorks = new List<PlannedWork>();
            HistoryData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("HistoryData");
            using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await HistoryData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    plannedWorks.Add(enumerator.Current.Value);
                }
            }
            return plannedWorks;
        }

        public async Task<bool> PubActive(List<PlannedWork> plannedWorks)
        {
            try
            {
                ActiveData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("ActiveData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerator = (await ActiveData.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        await ActiveData.TryRemoveAsync(tx, enumerator.Current.Key);
                    }

                    foreach (PlannedWork currentWork in plannedWorks)
                    {
                        await ActiveData.TryAddAsync(tx, currentWork.IdCurrentWork, currentWork);
                    }
                    await tx.CommitAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PubHistory(List<PlannedWork> plannedWorks)
        {
            try
            {
                HistoryData = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, PlannedWork>>("HistoryData");
                using (var tx = this.StateManager.CreateTransaction())
                {
                    foreach (PlannedWork plannedWork in plannedWorks)
                    {
                        await HistoryData.TryAddAsync(tx, plannedWork.IdCurrentWork, plannedWork);
                    }
                    await tx.CommitAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
