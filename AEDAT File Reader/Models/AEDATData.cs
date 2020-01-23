using System.Collections.ObjectModel;

namespace AEDAT_File_Reader.Models
{
    public class AEDATData
    {
        public string name { get; set; }
        public double startingTime { get; set; }
        public double duration { get; set; }
        public double endingTime { get; set; }
        public double eventCount { get; set; }
        public double avgEventsPerSecond { get; set; }
    }

    public class AEDATDataManager
    {
        public static ObservableCollection<AEDATData> GetAEDATData()
        {
            var data = new ObservableCollection<AEDATData>();
          
            return data;
        }
    }
}
