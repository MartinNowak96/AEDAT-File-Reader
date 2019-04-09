using System;
using System.Collections.Generic;
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
            if (file is null) return;
			byte[] aedatFile = await AedatUtilities.readToBytes(file);

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
            }

			ContentDialog invaldInputDialogue = new ContentDialog()
			{
				Title = "Testing...",
				Content = cameraParam.cameraX,
				CloseButtonText = "Close"
			};

			await invaldInputDialogue.ShowAsync();

		}

	}
}
