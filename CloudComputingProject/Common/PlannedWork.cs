using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class PlannedWork
    {
        public string IdCurrentWork { get; set; }
        public string Airport { get; set; }
        public string TypeOfAirport { get; set; }
        public string DetailsOfWorks { get; set; }
        public string WorkSteps { get; set; }
        public DateTime DateOfRepairWork { get; set; }
        public Boolean Archived { get; set; }

        public PlannedWork()
        {

        }

        public PlannedWork(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWork, string workSteps, DateTime dateOfRepairWork)
        {
            IdCurrentWork = idCurrentWork;
            Airport = airport;
            TypeOfAirport = typeOfAirport;
            DetailsOfWorks = detailsOfWork;
            WorkSteps = workSteps;
            DateOfRepairWork = dateOfRepairWork;
            Archived = false;
        }
    }
}
