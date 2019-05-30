using AEDAT_File_Reader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AEDAT_File_Reader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class eventList : Page
    {
        ContentDialog exportCompleteDialog = new ContentDialog()
        {
            Title = "Done",
            Content = "Bulk export completed.",
            CloseButtonText = "Close"
        };
        ObservableCollection<Event> tableData;
        public eventList()
        {
            tableData = EventManager.GetEvent();
            this.InitializeComponent();
        }

        private async void SelectFile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".AEDAT");


            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                dataGrid.ItemsSource = await AedatUtilities.GetEvents(file);
            }
        }

        enum ExportMode { Single, Bulk };
        ExportMode currentExportMode;

        private void Export_Tapped(object sender, TappedRoutedEventArgs e)
        {

            switch ((sender as Button).Name)
            {
                case "singleExport":
                    currentExportMode = ExportMode.Single;
                    break;
                case "bulkExport":
                    currentExportMode = ExportMode.Bulk;
                    break;
                default:
                    return;
            }
            exportSettings.IsOpen = true;
        }

        private async void ExportFromPopUp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            switch (currentExportMode)
            {
                case ExportMode.Single:
                    await SingleExport();
                    break;
                case ExportMode.Bulk:
                    await BulkExport();
                    break;
            }

        }

        private async System.Threading.Tasks.Task SingleExport()
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation =
                            Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };

            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("Comma-seperated Values", new List<string>() { ".csv" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "New Document";

            StorageFile file = await savePicker.PickSaveFileAsync();
            await SaveAsCSV(file, cordCol.IsOn, onOffCol.IsOn);

            ContentDialog exportComplete = new ContentDialog()
            {
                Title = "Done",
                Content = "Single export completed.",
                CloseButtonText = "Close"
            };
            await exportComplete.ShowAsync();
        }

        private async System.Threading.Tasks.Task SaveAsCSV(StorageFile file, bool includeCords, bool onOffType)
        {
            if (file == null) return;

            Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
            {
                var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                    {
                        Func<bool, string> formatOnOff;
                        Func<int, int, string> formatCoords;

                        // Determine which columns are included in the CSV
                        if (includeCords)
                        {
                            dataWriter.WriteString("On/Off,X,Y,Timestamp\n");
                            formatCoords = (x, y) => x.ToString() + "," + y.ToString() + ",";
                        }
                        else
                        {
                            dataWriter.WriteString("On/Off,Timestamp\n");
                            formatCoords = (x, y) => "";
                        }

                        // Determine if events are represented by booleans or integers
                        if (onOffType)
                            formatOnOff = b => b.ToString() + ",";
                        else
                            formatOnOff = b => b == true ? "1" : "-1" + ",";

                        // Write to the CSV file
                        foreach (Event item in dataGrid.ItemsSource) // TODO: decouple GetEvents and GUI stuff. Then move SaveAsCSV to AedatUtilities
                        {
                            dataWriter.WriteString(formatOnOff(item.onOff) + formatCoords(item.x, item.y) + item.time + "\n");
                        }

                        await dataWriter.StoreAsync();
                        await outputStream.FlushAsync();
                    }
                }
                stream.Dispose();

            }

        }

        private async System.Threading.Tasks.Task BulkExport()
        {
            // Select CSV save directory
            var saveFolderPicker = new Windows.Storage.Pickers.FolderPicker();
            saveFolderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            saveFolderPicker.FileTypeFilter.Add("*");
            var saveFolder = await saveFolderPicker.PickSingleFolderAsync();
            if (saveFolder == null) return;

            // Select CSV files
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".AEDAT");
            var files = await picker.PickMultipleFilesAsync();
            if (files == null) return;

            foreach (StorageFile file in files)
            {
                string fileName = file.Name;
                // Ignore file if its not an AEDAT file
                if (!fileName.EndsWith(".aedat"))
                    continue;

                string saveName = Path.ChangeExtension(fileName, ".csv");
                StorageFile newCSV;

                try
                {
                    newCSV = await saveFolder.CreateFileAsync(saveName);
                }
                catch
                {
                    // CSV already exists for this file in the save directory. Delete and make a new one
                    StorageFile toDelete = await saveFolder.GetFileAsync(saveName);
                    await toDelete.DeleteAsync();
                    newCSV = await saveFolder.CreateFileAsync(saveName);
                }

                // TODO: decouple GetEvents and GUI stuff. Then move SaveAsCSV to AedatUtilities
                dataGrid.ItemsSource = await AedatUtilities.GetEvents(file);
                // Create CSV
                await SaveAsCSV(newCSV, cordCol.IsOn, onOffCol.IsOn);
            }

            await exportCompleteDialog.ShowAsync();

        }

    }
}
