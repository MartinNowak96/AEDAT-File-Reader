using AEDAT_File_Reader.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AEDAT_File_Reader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class eventList : Page
    {
        ObservableCollection<Event> tableData;
        public eventList()
        {
            tableData = EventManager.GetEvent();
            this.InitializeComponent();
        }

        private async void selectFile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".AEDAT");


            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                getEvents(file);
            }
        }

        private async void export_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("Comma-seperated Values", new List<string>() { ".csv" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "New Document";
            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                //await Windows.Storage.FileIO.WriteTextAsync(file, "Name, Starting Time, Ending Time, Number of Events, Avg Events/Sec");
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    //await Windows.Storage.FileIO.WriteTextAsync(file, "Swift as a shadow");
                    var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                        {
                            dataWriter.WriteString("On/Off, X, Y, Timestamp\n");
                            foreach (Event item in tableData)
                            {
                                dataWriter.WriteString(item.onOff + "," + item.x + "," + item.y + "," + item.time + "\n");
                            }

                            await dataWriter.StoreAsync();
                            await outputStream.FlushAsync();
                        }
                    }
                    stream.Dispose();

                }
            }
        }

        private async void getEvents(StorageFile file)
        {
            const int dataEntrySize = 8;            // Number of elements in the data entry

            byte[] currentDataEntry = new byte[dataEntrySize];
            int timeStamp = 0;

            Queue<byte> dataEntryQ = new Queue<byte>();

            byte[] result;      // All of the bytes in the AEDAT file loaded into an array
            using (Stream stream = await file.OpenStreamForReadAsync())
            {
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    result = memoryStream.ToArray();
                }
            }

			int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref result);

            for(int i = endOfHeaderIndex; i < result.Count() - 1; i += 8)
            {
                for(int j = 7; j > -1; j--)
                {
                    currentDataEntry[j] = result[i+j];

                }
                Array.Reverse(currentDataEntry);
                timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes

                string eventType;

				if (AedatUtilities.GetEventType(currentDataEntry) == true)
				{
					eventType = "ON";
				}
				else
				{
					eventType = "OFF";
				}

                int x;
                int y;

                int[] XY = AedatUtilities.GetXYCords(currentDataEntry);

                tableData.Add(new Event {time= timeStamp, onOff = eventType , x = XY[0], y = XY[1]});
                
                
            }
            dataGrid.ItemsSource = tableData;



        }
    }
}
