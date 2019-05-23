using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
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
	public sealed partial class TestPage : Page
	{
		public TestPage()
		{
			this.InitializeComponent();
		}


		private async void SearchTest_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var picker = new FileOpenPicker
			{
				ViewMode = PickerViewMode.Thumbnail,
				SuggestedStartLocation = PickerLocationId.PicturesLibrary
			};
			picker.FileTypeFilter.Add(".AEDAT");

			var file = await picker.PickSingleFileAsync();
            if (file == null) return;
			byte[] aedatFile = await AedatUtilities.ReadToBytes(file);

			string result = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref aedatFile);

            CameraParameters cameraParam = AedatUtilities.ParseCameraModel(result);

            if (cameraParam == null)
            {
                ContentDialog AEEE = new ContentDialog()
                {
                    Title = "EEE",
                    Content = "AEEEE",
                    CloseButtonText = "Close"
                };
				await AEEE.ShowAsync();
            }

			ContentDialog invaldInputDialogue = new ContentDialog()
			{
				Title = "Testing...",
				Content = cameraParam.cameraName,
				CloseButtonText = "Close"
			};

			await invaldInputDialogue.ShowAsync();

		}

		private async void ReadTest_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var picker = new Windows.Storage.Pickers.FileOpenPicker
			{
				ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
				SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
			};
			picker.FileTypeFilter.Add(".AEDAT");


			var file = await picker.PickSingleFileAsync();
			if (file == null) return;

			ContentDialog readTestDialog = new ContentDialog()
			{
				Title = "Testing",
				Content = file.Path,
				CloseButtonText = "Close"
			};
			await readTestDialog.ShowAsync();
		}

		private async void ReadToBytesTime_Tapped(object sender, TappedRoutedEventArgs e)
		{
			long readToBytes_Time;
			var picker = new Windows.Storage.Pickers.FileOpenPicker
			{
				ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
				SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
			};
			picker.FileTypeFilter.Add(".AEDAT");


			var file = await picker.PickSingleFileAsync();
			if (file == null) return;

			Stopwatch sw = new Stopwatch();
			sw.Start();
				byte[] result = await AedatUtilities.ReadToBytes(file);    // All of the bytes in the AEDAT file loaded into an array
			sw.Stop();
			readToBytes_Time = sw.ElapsedMilliseconds;

			ContentDialog readTimeDialog = new ContentDialog()
			{
				Title = "Testing",
				Content = "Read to bytes time: " + readToBytes_Time + " ms",
				CloseButtonText = "Close"
			};
			await readTimeDialog.ShowAsync();
		}
	}
}
