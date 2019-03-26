using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
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

	public class Colors {

		public Colors(string name, byte[] color)
		{
			this.Name = name;
			this.Color = color;
		}
		public string Name;
		public byte[] Color;
		public static readonly byte[] Green = new byte[] { 0, 255, 0, 0 };
		public static readonly byte[] Red = new byte[] { 255, 0, 0, 0 };
		public static readonly byte[] Blue = new byte[] { 0, 0, 255, 0 };
		public static readonly byte[] Gray = new byte[] { 127, 127, 127, 0 };
		public static readonly byte[] White = new byte[] { 255, 255, 255, 0 };
	}



	public sealed partial class videoPage : Page
	{
		public ObservableCollection<Colors> colors;
		public videoPage()
		{
			
			colors = new ObservableCollection<Colors>();
			colors.Add(new Colors("Green",Colors.Green));
			colors.Add(new Colors("Red", Colors.Red));
			colors.Add(new Colors("Blue", Colors.Blue));
			colors.Add(new Colors("Gray", Colors.Gray));
			colors.Add(new Colors("White", Colors.White));
			InitializeComponent();
		}
		string previousValue = "100";

		private async void SelectFile_Tapped(object sender, TappedRoutedEventArgs e)
		{
			int frameTime;              // The amount of time per frame in uS (30 fps = 33333)
			int maxFrames;             // Max number of frames in the reconstructed video

			// Check for invalid input
		
			try
			{
				frameTime = Int32.Parse(frameTimeTB.Text);
				if (allFrameCheckBox.IsChecked == true)
				{
					maxFrames = 2147483647;
				}
				else
				{
					maxFrames = Int32.Parse(maxFramesTB.Text);
				}
				
			}
			catch (FormatException)
			{
				ContentDialog noWifiDialog = new ContentDialog()
				{
					Title = "Invalid Input",
					Content = "Please enter a valid number.",
					CloseButtonText = "Close"
				};

				await noWifiDialog.ShowAsync();
				return;
			}

			if (maxFrames <= 0 || frameTime <= 0)
			{
				ContentDialog noWifiDialog = new ContentDialog()
				{
					Title = "Invalid Input",
					Content = "Enter a number greater than 0.",
					CloseButtonText = "Close"
				};

				await noWifiDialog.ShowAsync();
				return;
			}

			Colors onColor = onColorCombo.SelectedItem as Colors;
			Colors offColor = offColorCombo.SelectedItem as Colors;

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
						AedatUtilities.setPixel(ref currentFrame, XY[0], XY[1], onColor.Color, cameraX);
					}
					else	// OFF event
					{
						AedatUtilities.setPixel(ref currentFrame, XY[0], XY[1], offColor.Color, cameraX);
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
				var savePicker = new Windows.Storage.Pickers.FileSavePicker();
				savePicker.SuggestedStartLocation =
					Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
				// Dropdown of file types the user can save the file as
				savePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });
				// Default file name if the user does not type one in or select a file to replace
				savePicker.SuggestedFileName = file.DisplayName;
				//var sampleFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("WBVideo.mp4",Windows.Storage.CreationCollisionOption.ReplaceExisting);
				Windows.Storage.StorageFile sampleFile = await savePicker.PickSaveFileAsync();
				await composition.SaveAsync(sampleFile);
				composition = await MediaComposition.LoadAsync(sampleFile);

				MediaEncodingProfile _MediaEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);


				////var encoding = new Windows.Media.MediaProperties.MediaEncodingProfile();
				////encoding.Video.Bitrate = 10;
				_MediaEncodingProfile.Video.Width = cameraX;
				_MediaEncodingProfile.Video.Height = cameraY;
				//_MediaEncodingProfile.Video.Bitrate = 300000000;

				await composition.RenderToFileAsync(sampleFile, MediaTrimmingPreference.Precise, _MediaEncodingProfile);
				//await composition.RenderToFileAsync(sampleFile);
				mediaSimple.Source = new Uri("ms-appx:///WBVideo.mp4");
			}
		}

		private void AllFrameCheckBox_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			this.previousValue = maxFramesTB.Text;
			maxFramesTB.IsReadOnly = true;
			maxFramesTB.IsEnabled = false;
			maxFramesTB.Text = "∞";
		}

		private void AllFrameCheckBox_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			maxFramesTB.Text = this.previousValue;
			maxFramesTB.IsReadOnly = false;
			maxFramesTB.IsEnabled = true;
		}

		private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			onColorCombo.SelectedIndex = 0;
			offColorCombo.SelectedIndex = 1;
		}
	}
}
