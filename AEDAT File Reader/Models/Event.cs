using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AEDAT_File_Reader.Models
{
    public class AEDATEvent
    {
        public bool onOff { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int time { get; set; }

        public AEDATEvent(byte[] bytes, int i, CameraParameters cam)
        {
            byte[] currentDataEntry = new byte[AedatUtilities.dataEntrySize];
            for (int j = 0; j < 8; j++)    // from i get the next 8 bytes
            {
                currentDataEntry[j] = bytes[i + (7 - j)];
            }
            time = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes, uS
            onOff = cam.getEventType(currentDataEntry);
            int[] XY = cam.getXY(currentDataEntry, cam.cameraY, cam.cameraX);
            x = XY[0];
            y = XY[1];
        }
    }

    public class EventManager
    {
        public static ObservableCollection<AEDATEvent> GetEvent()
        {
            var data = new ObservableCollection<AEDATEvent>();

            return data;
        }
    }
}
