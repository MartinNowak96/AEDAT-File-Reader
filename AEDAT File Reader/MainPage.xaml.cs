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


            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();

            if (file != null)
            {
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
                            dataWriter.WriteString("Name, Starting Time (s), Ending Time (s), Number of Events, Avg Events/Sec\n");
                            foreach(AEDATData item in tableData)
                            {
                                dataWriter.WriteString(item.name+  ","+ item.startingTime + "," + item.endingTime + "," + item.eventCount + "," + item.avgEventsPerSecond + "\n");
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

            //int byteIn;
            bool endOfHeader = false;               // Has the end of the header been found?
            string currentByte;
            string[] currentHeaderBytes = new string[headerCheckSize];
            byte[] currentDataEntry = new byte[dataEntrySize];
            int timeStamp =0;

            //Compare current bytes being red to find end of header. (#End Of ASCII)
            string[] endOfHeaderCheck = new string[headerCheckSize] { "0A", "23", "45", "6E", "64", "20", "4F", "66", "20", "41", "53", "43", "49", "49", "20", "48", "65", "61", "64", "65", "72", "0D", "0A" };


            Queue<string> headerCheckQ = new Queue<string>();
            Queue<byte> dataEntryQ = new Queue<byte>();


            byte[] result;
            using (Stream stream = await file.OpenStreamForReadAsync())
            {
                using (var memoryStream = new MemoryStream())
                {

                    stream.CopyTo(memoryStream);
                    result = memoryStream.ToArray();
                }
            }
            int i = 0;
            foreach (int byteIn in result)
            {

            


                if (!endOfHeader)
                {
                    currentByte = string.Format("{0:X2}", byteIn);
                    headerCheckQ.Enqueue(currentByte);

                    // Remove oldest element in the queue if it becomes too large. FIFO
                    if (headerCheckQ.Count > headerCheckSize) headerCheckQ.Dequeue();

                    headerCheckQ.CopyTo(currentHeaderBytes, 0);
                    if (Enumerable.SequenceEqual(endOfHeaderCheck, currentHeaderBytes))
                    {
                        Console.WriteLine("End of header");
                        endOfHeader = true;
                        
                    }
                    i++;
                }
                else
                {
                    if (dataEntryQ.Count < dataEntrySize)
                        dataEntryQ.Enqueue((byte)byteIn);
                    else
                    {
                        dataEntryQ.CopyTo(currentDataEntry, 0);
                        Array.Reverse(currentDataEntry);
                        timeStamp = BitConverter.ToInt32(currentDataEntry, 0);
                        break;
                    }
                }
                

            }

            int endingTime = 0;
            byte[] finalDataEntry = new byte[dataEntrySize];
            int k = 0;
            for (int j = result.Count() -1; j > result.Count() -9; j--)
            {
                finalDataEntry[k] = result[j];
                k++;
            }
            endingTime = BitConverter.ToInt32(finalDataEntry, 0);

            double startingTime = (double)timeStamp / 1000000.000f;
            double endingTime2 = (double)endingTime / 1000000.000f;
            double eventCount = (result.Count() - i) / 8;

            tableData.Add(new AEDATData { name = file.Name,
                startingTime = startingTime,
                eventCount = eventCount,
                endingTime = endingTime2 ,
                avgEventsPerSecond = Math.Abs(endingTime2 - startingTime)/eventCount,
                duration =endingTime2 - startingTime });
            
        }

    }
}
