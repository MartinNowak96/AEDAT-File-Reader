using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AEDAT_File_Reader
{
	public static class AedatUtilities
	{

		public static int GetEndOfHeaderIndex(ref byte[] fileBytes) {
			bool foundEndOfHeader = false;
			const int headerCheckSize = 23;         // Number of elements in the header check
			byte[] currentHeaderBytes = new byte[headerCheckSize];

			//Compare current bytes being red to find end of header. (#End Of ASCII)
			byte[] endOfHeaderCheck = new byte[headerCheckSize] { 0x0a, 0x23, 0x45, 0x6e, 0x64, 0x20, 0x4f, 0x66, 0x20, 0x41, 0x53, 0x43, 0x49, 0x49, 0x20, 0x48, 0x65, 0x61, 0x64, 0x65, 0x72, 0x0d, 0x0a };

			Queue<byte> headerCheckQ = new Queue<byte>();

			int endOfHeaderIndex = 0;
			foreach (byte byteIn in fileBytes)
			{
				if (!foundEndOfHeader)
				{
					headerCheckQ.Enqueue(byteIn);

					// Remove oldest element in the queue if it becomes too large. FIFO
					if (headerCheckQ.Count > headerCheckSize) headerCheckQ.Dequeue();

					headerCheckQ.CopyTo(currentHeaderBytes, 0);
					if (Enumerable.SequenceEqual(endOfHeaderCheck, currentHeaderBytes))
					{
						foundEndOfHeader = true;
					}
					endOfHeaderIndex++;
				}
				else
				{
					break;
				}
			}

			return endOfHeaderIndex;
		}


		/// <summary>
		/// Extracts the event type from a data entry byte array.
		/// </summary>
		/// <param name="dataEntry"></param>
		/// <returns>Returns true for an ON event, false for an OFF event.</returns>
		public static bool GetEventType(byte[] dataEntry) {

			int eventBit = (dataEntry[5] >> 3) & 1;     //Event type is located in the fourth bit of the sixth byte

			if (eventBit == 0)
			{
				return false;	// OFF event
			}
			else
			{
				return true;	// ON event
			}

		}

        public static int[] getXYCords(byte[] dataEntry)
        {
            int[] XY = new int[2];

            BitArray bits = new BitArray(dataEntry);


            //Select bits using lambda or one liner for loop
            //y
            new BitArray(new bool[] { bits[54], bits[55], bits[56], bits[57], bits[58], bits[59], bits[60], bits[61], bits[62] }).CopyTo(XY, 1);
            //x
            new BitArray(new bool[] { bits[44],bits[45],bits[46], bits[47],bits[48],bits[49],bits[50],bits[51],bits[52], bits[53]}).CopyTo(XY, 0);
            return XY;
        }

	}
}
