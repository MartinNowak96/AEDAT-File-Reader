using AEDAT_File_Reader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AEDAT_File_Reader
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class videoPage : Page
	{
		public videoPage()
		{
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

				WriteableBitmap bitmap = new WriteableBitmap(240, 180);

				// Initilize writeable bitmap
				InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryRandomAccessStream);
				Stream pixelStream = bitmap.PixelBuffer.AsStream();
				byte[] pixels = new byte[pixelStream.Length];

				byte[] result = await AedatUtilities.readToBytes(file);
				const int dataEntrySize = 8;
				byte[] currentDataEntry = new byte[dataEntrySize];
				int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref result);

				int timeStamp = 0;
				for (int i = endOfHeaderIndex + 240000; i < (endOfHeaderIndex + 250000); i += 8)
				{
					for (int j = 7; j > -1; j--)
					{
						currentDataEntry[j] = result[i + j];

					}
					Array.Reverse(currentDataEntry);
					timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes

					UInt16[] XY = AedatUtilities.GetXYCords(currentDataEntry, 180);
					if (AedatUtilities.GetEventType(currentDataEntry))
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], new byte[] { 0, 255, 0, 0 }, 240);
					}
					else
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], new byte[] { 255, 0, 0, 0 }, 240);
					}

					
				}
				encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96.0, 96.0, pixels);
				await encoder.FlushAsync();
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.SetSource(inMemoryRandomAccessStream);
				display.Source = bitmapImage;



				//// First pixel (0,0)
				//pixels[0] = 255;
				//pixels[1] = 0;
				//pixels[2] = 0;
				//pixels[3] = 0;
				//// End of first row (240, 0)
				//pixels[956] = 255;
				//pixels[957] = 0;
				//pixels[958] = 0;
				//pixels[959] = 0;

				//AedatUtilities.setPixel(ref pixels, 240, 0, new byte[] { 255, 0, 0, 0 }, 240);

				// Display image from array (strides)




				//getEvents(file);
			}
		}
	}
}
