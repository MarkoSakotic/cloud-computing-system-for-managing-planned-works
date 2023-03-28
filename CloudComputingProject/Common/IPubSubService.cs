using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IPubSubService
    {
        [OperationContract]
        Task<List<PlannedWork>> GetActiveData();

        [OperationContract]
        Task<List<PlannedWork>> GetHistoryData();

        [OperationContract]
        Task<bool> PubActive(List<PlannedWork> plannedWorks);

        [OperationContract]
        Task<bool> PubHistory(List<PlannedWork> plannedWorks);

    }
}
