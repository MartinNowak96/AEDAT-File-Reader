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
    public sealed partial class SummaryView : Page
    {
        ObservableCollection<AEDATData> tableData;
        AEDATData selectedData;
        public SummaryView()
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

        private  void export_Tapped(object sender, TappedRoutedEventArgs e)
        {
            exportSettings.IsOpen = true;
            
        }

        private async void exportFromPopUp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            exportSettings.IsOpen = false;
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
                            foreach (AEDATData item in tableData)
                            {
                                dataWriter.WriteString(item.name + "," + item.startingTime + "," + item.endingTime + "," + item.duration + "," + item.eventCount + "," + item.avgEventsPerSecond + "\n");
                            }

                            if(showAverages.IsOn == true)
                            {
                                dataWriter.WriteString("Averages:\n");
                                double startTimeAverage = 0;
                                double endTimeAverage = 0;
                                double durationAverage = 0;
                                double eventCountAvgerage = 0;
                                double avgEventPerSecondAverage = 0;

                                foreach (AEDATData item in tableData)
                                {
                                  
                                    startTimeAverage += item.startingTime;
                                    endTimeAverage += item.endingTime;
                                    durationAverage += item.duration;
                                    eventCountAvgerage += item.eventCount;
                                    avgEventPerSecondAverage += item.avgEventsPerSecond;
                                }
                                dataWriter.WriteString(" ," + startTimeAverage/ tableData.Count() + "," + endTimeAverage / tableData.Count() + "," + durationAverage / tableData.Count() + "," + eventCountAvgerage / tableData.Count() + "," + avgEventPerSecondAverage / tableData.Count() + "\n");

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

			foreach (byte byteIn in result.Skip(endOfHeaderIndex))
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
                name = file.Name.Replace(".aedat", "").Replace(".AEDAT", ""),
                startingTime = startingTime,
                eventCount = eventCount,
                endingTime = endingTime2 ,
                avgEventsPerSecond = eventCount / Math.Abs(endingTime2 - startingTime),
                duration =endingTime2 - startingTime
			});
            
        }

        private void removeAll_Tapped(object sender, TappedRoutedEventArgs e)
        {
            tableData.Clear();
        }

        
    }
}
