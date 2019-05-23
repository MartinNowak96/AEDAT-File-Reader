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
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
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
        ObservableCollection<Event> tableData;
        public eventList()
        {
            tableData = EventManager.GetEvent();
            this.InitializeComponent();
        }

        private async void selectFile_Tapped(object sender, TappedRoutedEventArgs e)
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
			switch (currentExportMode) {
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
			await PrepAndSaveCSV(file);
		}

		private async System.Threading.Tasks.Task PrepAndSaveCSV(StorageFile file)
		{
			if (file != null)
			{
				Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
				if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
				{
					var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
					using (var outputStream = stream.GetOutputStreamAt(0))
					{
						using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
						{
							await CreateCSV(outputStream, dataWriter);
						}
					}
					stream.Dispose();

				}
			}
		}

		// TODO: fix crash when csv file alredy exists
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

				// TODO: decouple GetEvents and GUI stuff
				dataGrid.ItemsSource = await AedatUtilities.GetEvents(file);

				string saveName = Path.ChangeExtension(fileName, ".csv");
				StorageFile newCSV = await saveFolder.CreateFileAsync(saveName);

				await PrepAndSaveCSV(newCSV);

			}

			ContentDialog exportComplete = new ContentDialog()
			{
				Title = "Done",
				Content = "Bulk export completed.",
				CloseButtonText = "Close"
			};
			await exportComplete.ShowAsync();

		}

		private async System.Threading.Tasks.Task CreateCSV(Windows.Storage.Streams.IOutputStream outputStream, Windows.Storage.Streams.DataWriter dataWriter)
		{
			Func<bool, string> formatOnOff;
			Func<int, int, string> formatCoords;

			// Determine which columns are included in the CSV
			if (coordCol.IsOn)
			{
				dataWriter.WriteString("On/Off, X, Y, Timestamp\n");
				formatCoords = (x, y) => x.ToString() + "," + y.ToString() + ",";
			}
			else
			{
				dataWriter.WriteString("On/Off, Timestamp\n");
				formatCoords = (x, y) => "";
			}

			// Determine if events are represented by booleans or integers
			if (onOffCol.IsOn)
			formatOnOff = b => b.ToString() + ",";
			else
			formatOnOff = b => b == true ? "1" : "-1" + ",";
			
			// Write to the CSV file
			foreach (Event item in dataGrid.ItemsSource)
			{
				dataWriter.WriteString(formatOnOff(item.onOff) + formatCoords(item.x, item.y) + item.time + "\n");
			}

			await dataWriter.StoreAsync();
			await outputStream.FlushAsync();
		}

	}
}
