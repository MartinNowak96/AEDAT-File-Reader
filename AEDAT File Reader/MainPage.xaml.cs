using AEDAT_File_Reader.Models;
using System;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AEDAT_File_Reader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ObservableCollection<AEDATData> tableData;
        AEDATData selectedData;
        public MainPage()
        {
            tableData = AEDATDataManager.GetAEDATData();
            this.InitializeComponent();
        }


        private async void selectFile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".AEDAT");


            var files = await picker.PickMultipleFilesAsync();

            if (files != null)
            {
                foreach(StorageFile file in files)
                getData(file);
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
                            dataWriter.WriteString("Name, Starting Time (s), Ending Time (s), Duration (s), Number of Events, Avg Events/Sec\n");
                            foreach(AEDATData item in tableData)
                            {
                                dataWriter.WriteString(item.name+  ","+ item.startingTime + "," + item.endingTime + "," + item.duration+ ","  + item.eventCount + "," + item.avgEventsPerSecond + "\n");
                            }

                            await dataWriter.StoreAsync();
                            await outputStream.FlushAsync();
                        }
                    }
                    stream.Dispose();

                }
            }
        }

        private void editData_Tapped(object sender, TappedRoutedEventArgs e)
        {
            dataName.Text = selectedData.name;
            editDataPopUp.IsOpen = true;
        }

        private void deleteData_Tapped(object sender, TappedRoutedEventArgs e)
        {
            tableData.Remove(selectedData);
        }

        private void listAEDATItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            var s = (FrameworkElement)sender;
            var d = s.DataContext;
            var data = d as AEDATData;
            selectedData = data;

        }

        private void saveChanges_Tapped(object sender, TappedRoutedEventArgs e)
        {
            int index = tableData.IndexOf(selectedData);
            tableData.Remove(selectedData);
            selectedData.name = dataName.Text;
            tableData.Insert(index, selectedData);
            dataGrid.ItemsSource = null;
            dataGrid.ItemsSource = tableData;
        }


        private async void getData(StorageFile file)
        {
            const int headerCheckSize = 23;         // Number of elements in the header check
            const int dataEntrySize = 8;            // Number of elements in the data entry

            bool endOfHeader = false;               // Has the end of the header been found?
            byte[] currentHeaderBytes = new byte[headerCheckSize];
            byte[] currentDataEntry = new byte[dataEntrySize];
            int timeStamp = 0;

            //Compare current bytes being red to find end of header. (#End Of ASCII)
            byte[] endOfHeaderCheck = new byte[headerCheckSize] { 0x0a, 0x23, 0x45, 0x6e, 0x64, 0x20, 0x4f, 0x66, 0x20, 0x41, 0x53, 0x43, 0x49, 0x49, 0x20, 0x48, 0x65, 0x61, 0x64, 0x65, 0x72, 0x0d, 0x0a };

            Queue<byte> headerCheckQ = new Queue<byte>();
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

            int endOfHeaderIndex = 0;
            foreach (byte byteIn in result)
            {
                if (!endOfHeader)
                {
                    headerCheckQ.Enqueue(byteIn);

                    // Remove oldest element in the queue if it becomes too large. FIFO
                    if (headerCheckQ.Count > headerCheckSize) headerCheckQ.Dequeue();

                    headerCheckQ.CopyTo(currentHeaderBytes, 0);
                    if (Enumerable.SequenceEqual(endOfHeaderCheck, currentHeaderBytes))
                    {
                        endOfHeader = true;
                    }
                    endOfHeaderIndex++;
                }
                else
                {
                    if (dataEntryQ.Count < dataEntrySize)
                        dataEntryQ.Enqueue(byteIn);
                    else
                    {
                        dataEntryQ.CopyTo(currentDataEntry, 0);
                        Array.Reverse(currentDataEntry);
                        timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes
                        break;
                    }
                }
            }

			// Get final data entry
            int endingTime = 0;
            byte[] finalDataEntry = new byte[dataEntrySize];
            int i = 0;
            for (int j = result.Count() -1; j > result.Count() - 9; j--)
            {
                finalDataEntry[i] = result[j];
                i++;
            }
            endingTime = BitConverter.ToInt32(finalDataEntry, 0);   // Timestamp is found in the first four bytes

            // Convert to seconds
            double startingTime = (double)timeStamp / 1000000.000f;
            double endingTime2 = (double)endingTime / 1000000.000f;

            // Total number of events in the file
            double eventCount = (result.Count() - endOfHeaderIndex) / 8;

            // Add data to GUI
            tableData.Add(new AEDATData {
				name = file.Name,
                startingTime = startingTime,
                eventCount = eventCount,
                endingTime = endingTime2 ,
                avgEventsPerSecond = Math.Abs(endingTime2 - startingTime)/eventCount,
                duration =endingTime2 - startingTime
			});
            
        }

        private void removeAll_Tapped(object sender, TappedRoutedEventArgs e)
        {
            tableData.Clear();
        }
    }
}
