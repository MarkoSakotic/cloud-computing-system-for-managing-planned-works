using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace ReportWorkService
{
    [ServiceContract]
    public interface IReportWorkService
    {
        [OperationContract]
        Task<bool> AddPlannedWork(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWorks, string workSteps);

        [OperationContract]
        Task<List<PlannedWork>> GetAllData();
    }
}
