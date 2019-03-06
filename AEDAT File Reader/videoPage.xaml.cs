using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AEDAT_File_Reader
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	/// 

	public static class Colors {
		public static readonly byte[] Green = new byte[] { 0, 255, 0, 0 };
		public static readonly byte[] Red = new byte[] { 255, 0, 0, 0 };
	}


	public sealed partial class videoPage : Page
	{
		public videoPage()
		{
			InitializeComponent();
		}

		private async void selectFile_Tapped(object sender, TappedRoutedEventArgs e)
		{
			var picker = new FileOpenPicker
			{
				ViewMode = PickerViewMode.Thumbnail,
				SuggestedStartLocation = PickerLocationId.PicturesLibrary
			};
			picker.FileTypeFilter.Add(".AEDAT");
			
			MediaComposition composition = new MediaComposition();
			var file = await picker.PickSingleFileAsync();

			if (file != null)
			{

				ushort cameraX = 240;
				ushort cameraY = 180;
				int maxFrames = 1000;

				WriteableBitmap bitmap = new WriteableBitmap(cameraX, cameraY);

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

					UInt16[] XY = AedatUtilities.GetXYCords(currentDataEntry, cameraY);
					if (AedatUtilities.GetEventType(currentDataEntry))
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], Colors.Green, cameraX);
					}
					else
					{
						AedatUtilities.setPixel(ref pixels, XY[0], XY[1], Colors.Red, cameraX);
					}
					if(lastTime == -999999)
					{
						lastTime = timeStamp;
					}
					else
					{
						if(lastTime+ 33333 <= timeStamp )//30 fps
						{
		
							WriteableBitmap b = new WriteableBitmap(cameraX,cameraY);
							using (Stream stream = b.PixelBuffer.AsStream())
							{
								await stream.WriteAsync(pixels, 0, pixels.Length);
							}

							SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Premultiplied);
							CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);
							MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(.03));
	
							composition.Clips.Add(mediaClip);
							count++;
							if (count > maxFrames)
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
			}
		}
	}
}
