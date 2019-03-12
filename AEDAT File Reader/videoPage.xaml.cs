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

		private async void SelectFile_Tapped(object sender, TappedRoutedEventArgs e)
		{
			int frameTime;              // The amount of time per frame in uS (30 fps = 33333)
			int maxFrames;             // Max number of frames in the reconstructed video

			// Check for invalid input
			try
			{
				frameTime = Int32.Parse(frameTimeTB.Text);
				maxFrames = Int32.Parse(maxFramesTB.Text);
				if (maxFrames <= 0 || frameTime <= 0) {
					throw new FormatException("Parameters must be greater than 0");
				}
			}
			catch (FormatException)
			{
				// TODO: Popup error message
				return;
			}

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

				ushort cameraX = 240;		// Camera X resolution in pixels
				ushort cameraY = 180;       // Camera Y resolution in pixels

				// Initilize writeable bitmap
				WriteableBitmap bitmap = new WriteableBitmap(cameraX, cameraY);
				InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryRandomAccessStream);
				Stream pixelStream = bitmap.PixelBuffer.AsStream();
				byte[] currentFrame = new byte[pixelStream.Length];

				byte[] aedatFile = await AedatUtilities.readToBytes(file);
				byte[] currentDataEntry = new byte[AedatUtilities.dataEntrySize];
				int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);
				int lastTime = -999999;
				int timeStamp = 0;
				int frameCount = 0;

				// Read through AEDAT file
				for (int i = endOfHeaderIndex; i < (aedatFile.Length); i += AedatUtilities.dataEntrySize)
				{
					for (int j = 7; j > -1; j--)
					{
						currentDataEntry[j] = aedatFile[i + j];
					}
					Array.Reverse(currentDataEntry);
					timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes, uS

					UInt16[] XY = AedatUtilities.GetXYCords(currentDataEntry, cameraY);
					if (AedatUtilities.GetEventType(currentDataEntry)) // ON event
					{
						AedatUtilities.setPixel(ref currentFrame, XY[0], XY[1], Colors.Green, cameraX);
					}
					else	// OFF event
					{
						AedatUtilities.setPixel(ref currentFrame, XY[0], XY[1], Colors.Red, cameraX);
					}
					if(lastTime == -999999)
					{
						lastTime = timeStamp;
					}
					else
					{
						if (lastTime + frameTime <= timeStamp) // Collected events within specified timeframe, add frame to video
						{
							WriteableBitmap b = new WriteableBitmap(cameraX, cameraY);
							using (Stream stream = b.PixelBuffer.AsStream())
							{
								await stream.WriteAsync(currentFrame, 0, currentFrame.Length);
							}

							SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Premultiplied);
							CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);
							MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(0.0333f));
	
							composition.Clips.Add(mediaClip);
							frameCount++;
							if (frameCount > maxFrames)
							{
								break;
							}
							currentFrame = new byte[pixelStream.Length];
							lastTime = timeStamp;
						}
					}
					
				}

				// Create video file
				var sampleFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("WBVideo.mp4");
				await composition.SaveAsync(sampleFile);
				composition = await MediaComposition.LoadAsync(sampleFile);
				await composition.RenderToFileAsync(sampleFile);
			}
		}
	}
}
