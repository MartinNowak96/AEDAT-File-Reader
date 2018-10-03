using AEDAT_File_Reader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
                tableData.Add(new AEDATData { name = file.Name, startingTime = 123 });
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
                            dataWriter.WriteString("Name, Starting Time, Ending Time, Number of Events, Avg Events/Sec\n");
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
    }
}
