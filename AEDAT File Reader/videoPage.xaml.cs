using AEDAT_File_Reader.Models;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Pickers;
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
			
			MediaComposition composition = new MediaComposition();
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
				int lastTime = -999999;
				int timeStamp = 0;
				int count = 0;
				for (int i = endOfHeaderIndex; i < (result.Length); i += 8)
				{
					for (int j = 7; j > -1; j--)
					{
						currentDataEntry[j] = result[i + j];

					}
					Array.Reverse(currentDataEntry);
					timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes, uS

					UInt16[] XY = AedatUtilities.GetXYCords(currentDataEntry, 180);
					if (AedatUtilities.GetEventType(currentDataEntry))
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], new byte[] { 0, 255, 0, 0 }, 240);
					}
					else
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], new byte[] { 255, 0, 0, 0 }, 240);
					}
					if(lastTime == -999999)
					{
						lastTime = timeStamp;
					}
					else
					{
						if(lastTime+ 33333 <= timeStamp )//30 fps
						{
		
							WriteableBitmap b = new WriteableBitmap(240,180);
							using (Stream stream = b.PixelBuffer.AsStream())
							{
								await stream.WriteAsync(pixels, 0, pixels.Length);
							}

							SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Premultiplied);
							CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);
							MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(.03));
	
							composition.Clips.Add(mediaClip);
							count++;
							if (count > 1000)
							{
								break;
							}
							pixels = new byte[pixelStream.Length];
							lastTime = timeStamp;
						}
					}
					
				}
				var sampleFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("WBVideo.mp4");
				await composition.SaveAsync(sampleFile);
				composition = await MediaComposition.LoadAsync(sampleFile);
				await composition.RenderToFileAsync(sampleFile);


				//display.Source = bitmapImage;



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
