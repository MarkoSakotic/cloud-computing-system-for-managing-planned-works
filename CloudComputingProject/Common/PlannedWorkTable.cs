﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class PlannedWorkTable : TableEntity
    {
        public string IdCurrentWork { get; set; }
        public string Airport { get; set; }
        public string TypeOfAirport { get; set; }
        public string DetailsOfWorks { get; set; }
        public string WorkSteps { get; set; }
        public Boolean Archived { get; set; }

        public PlannedWorkTable()
        {

        }

        public PlannedWorkTable(string idCurrentWork, string airport, string typeOfAirport, string detailsOfWork, string workSteps)
        {
            RowKey = idCurrentWork;
            PartitionKey = "CurrentPlannedWorkData";

            IdCurrentWork = idCurrentWork;
            Airport = airport;
            TypeOfAirport = typeOfAirport;
            DetailsOfWorks = detailsOfWork;
            WorkSteps = workSteps;
            Archived = false;
        }
    }
}
