using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AEDAT_File_Reader.Models
{
    public class Event
    {
        public string onOff { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int time { get; set; }
    }

    public class EventManager
    {
        public static ObservableCollection<Event> GetEvent()
        {
            var data = new ObservableCollection<Event>();

            return data;
        }
    }
}
