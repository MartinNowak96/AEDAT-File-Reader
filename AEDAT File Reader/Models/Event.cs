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
        public bool onOff { get; set; }
        public UInt16 x { get; set; }
        public UInt16 y { get; set; }
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
