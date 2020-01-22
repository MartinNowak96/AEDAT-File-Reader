using AEDAT_File_Reader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
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
		enum ExportMode { Single, Bulk };
		ExportMode currentExportMode;
		CameraParameters currentCamera;

		readonly ContentDialog bulkExportComplete = new ContentDialog()
		{
			Title = "Done",
			Content = "Bulk export completed.",
			CloseButtonText = "Close"
		};

		readonly ContentDialog singleExportComplete = new ContentDialog()
		{
			Title = "Done",
			Content = "Single export completed.",
			CloseButtonText = "Close"
		};

		ObservableCollection<AEDATEvent> tableData;
		public eventList()
		{
			tableData = EventManager.GetEvent();
			this.InitializeComponent();
		}

		private async Task<List<AEDATEvent>> UpdateDataGrid(StorageFile file)
		{
			List<AEDATEvent> temp = await AedatUtilities.GetEvents(file);

			// Get camera type
			byte[] headerBytes = await AedatUtilities.ReadHeaderToBytes(file);
			string cameraTypeSearch = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref headerBytes);
			currentCamera = AedatUtilities.ParseCameraModel(cameraTypeSearch);
			return temp;
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
				List<AEDATEvent> events =await UpdateDataGrid(file);
				dataGrid.ItemsSource = events;
			}
		}

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

		private async Task SingleExport()
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

			StorageFile newCSV = await savePicker.PickSaveFileAsync();
			List<AEDATEvent> tempEvents = (List<AEDATEvent>)dataGrid.ItemsSource;
			await SaveAsCSV(tempEvents,newCSV, cordCol.IsOn, onOffCol.IsOn, pixelNumber.IsOn);

			await singleExportComplete.ShowAsync();
		}

		private async Task BulkExport()
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

				List<AEDATEvent> tempEvents = await UpdateDataGrid(file);
				// Create CSV
				await SaveAsCSV(tempEvents, newCSV, cordCol.IsOn, onOffCol.IsOn, pixelNumber.IsOn);
			}

			await bulkExportComplete.ShowAsync();

		}

		private async Task SaveAsCSV(List<AEDATEvent> data,StorageFile saveFile, bool includeCords, bool onOffType, bool pixelNumber)
        {
            if (saveFile == null) return;

            Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(saveFile);
            if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
            {
				var stream = await saveFile.OpenAsync(FileAccessMode.ReadWrite);
				using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new Windows.Storage.Streams.DataWriter(outputStream))
                    {
                        Func<bool, string> formatOnOff;
                        Func<int, int, string> formatCoords;

                        // Determine which columns are included in the CSV
                        if (includeCords)
                        {
							if (pixelNumber)
							{
								dataWriter.WriteString("On/Off,PixelNumber,Timestamp\n");
								formatCoords = (x, y) => ((currentCamera.cameraX * (y - 1)) + (x - 1)).ToString() + ","; 
							}
							else
							{
								dataWriter.WriteString("On/Off,X,Y,Timestamp\n");
								formatCoords = (x, y) => x.ToString() + "," + y.ToString() + ",";
							}
                            
                        }
                        else
                        {
                            dataWriter.WriteString("On/Off,Timestamp\n");
                            formatCoords = (x, y) => "";
                        }

                        // Determine if events are represented by booleans or integers
                        if (!onOffType)
                            formatOnOff = b => b.ToString() + ",";
                        else
                            formatOnOff = b => b == true ? "1," : "-1,";

                        // Write to the CSV file
                        foreach (AEDATEvent item in data)
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

		private void CordCol_Toggled(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			pixelNumber.IsEnabled = cordCol.IsOn;
		}
	}
}
