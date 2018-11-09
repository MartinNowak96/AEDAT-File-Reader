using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AEDAT_File_Reader
{
	public static class AedatUtilities
	{

		/// <summary>
		/// Iterates through an AEDAT file to find the end of the header.
		/// </summary>
		/// <param name="fileBytes"></param>
		/// <returns>Returns an int which signifies the position in the file where the header ends.</returns>
		public static int GetEndOfHeaderIndex(ref byte[] fileBytes)
		{
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
		public static bool GetEventType(byte[] dataEntry)
		{
			return ((dataEntry[5] >> 3) & 1) == 1;     //Event type is located in the fourth bit of the sixth byte
		}

		/// <summary>
		/// Gets the XY coordinates from the provided data entry.
		/// </summary>
		/// <param name="dataEntry"></param>
		/// <returns>Returns a uint16 array containing the XY coordinates.</returns>
        public static UInt16[] GetXYCords(byte[] dataEntry)
		{
            UInt16[] xy = new UInt16[2];
            BitArray bits = new BitArray(dataEntry);

            // X
            bool[] cord = new bool[] { bits[54], bits[55], bits[56], bits[57], bits[58], bits[59], bits[60], bits[61], bits[62] };
            xy[0] = BoolArrayToUint(cord);

			// Y
            cord = new bool[] { bits[44], bits[45], bits[46], bits[47], bits[48], bits[49], bits[50], bits[51], bits[52], bits[53] };
            xy[1] = BoolArrayToUint(cord);

            return xy;
        }

		private static UInt16 BoolArrayToUint(bool[] bools) {
			UInt16 word = 0;

			for (UInt16 i = 0; i < bools.Length; i++) {
				if (bools[i])
				{
					int twoToPower = (1 << i);
					word = (UInt16)(word + twoToPower);
				}
			}
			return word;
		}
    

	}
}
