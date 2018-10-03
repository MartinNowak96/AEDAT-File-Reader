using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AEDAT_File_Reader.Models
{
    public class AEDATData
    {
        public int id { get; set; }
        public string name { get; set; }
        public double startingTime { get; set; }
        public double endingTime { get; set; }
        public double eventCount { get; set; }
        public double avgEventsPerSecond { get; set; }
    }

    public class AEDATDataManager
    {
        public static ObservableCollection<AEDATData> GetAEDATData()
        {
            var data = new ObservableCollection<AEDATData>();
            // items.Add(new Item { name = "t-shirt", category = "Shirts", price="9.99", minQuantity ="10", TaxID1= "1"});
            return data;
        }
    }
}
