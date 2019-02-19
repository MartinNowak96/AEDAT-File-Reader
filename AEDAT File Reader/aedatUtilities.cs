using AEDAT_File_Reader.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

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
        public static UInt16[] GetXYCords(byte[] dataEntry, UInt16 height)
		{
            UInt16[] xy = new UInt16[2];
            BitArray bits = new BitArray(dataEntry);

            // Y
            bool[] cord = new bool[] { bits[54], bits[55], bits[56], bits[57], bits[58], bits[59], bits[60], bits[61], bits[62] };
            xy[1] = Convert.ToUInt16((height - BoolArrayToUint(cord)) & 0xffff);

			// X
            cord = new bool[] { bits[44], bits[45], bits[46], bits[47], bits[48], bits[49], bits[50], bits[51], bits[52], bits[53] };
            xy[0] = BoolArrayToUint(cord);

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

		public static  async Task< List<Event>> GetEvents(StorageFile file)
		{
			
			byte[] result  = await readToBytes(file);      // All of the bytes in the AEDAT file loaded into an array
			
			List<Event> tableData = new List<Event>();
			const int dataEntrySize = 8;            // Number of elements in the data entry

			byte[] currentDataEntry = new byte[dataEntrySize];
			

			int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref result);

			int timeStamp = 0;
			for (int i = endOfHeaderIndex; i < result.Count() - 1; i += 8)
			{
				for (int j = 7; j > -1; j--)
				{
					currentDataEntry[j] = result[i + j];

				}
				Array.Reverse(currentDataEntry);
				timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes

				UInt16[] XY = AedatUtilities.GetXYCords(currentDataEntry, 180);

				tableData.Add(new Event { time = timeStamp, onOff = AedatUtilities.GetEventType(currentDataEntry), x = XY[0], y = XY[1] });


			}

			return tableData;


		}

		public static async Task<byte[]> readToBytes(StorageFile file)
		{
			byte[] result;
			using (Stream stream = await file.OpenStreamForReadAsync())
			{
				using (var memoryStream = new MemoryStream())
				{
					stream.CopyTo(memoryStream);
					result = memoryStream.ToArray();
				}
			}

			return result;
		}

		public static void setPixel(ref byte[] pixels, int x, int y, byte[] rgba, int imageWidth)
		{
			y = y - 1;
		
			int startingPoint = (((imageWidth * y) + x)  * 4);

			pixels[startingPoint] = rgba[2];
			pixels[startingPoint + 1] = rgba[1];
			pixels[startingPoint + 2] = rgba[0];
			pixels[startingPoint + 3] = rgba[3];
		}
	}
}
