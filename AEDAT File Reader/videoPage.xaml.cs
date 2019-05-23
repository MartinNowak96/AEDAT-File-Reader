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
			Name = name;
			Color = color;
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

            colors = new ObservableCollection<Colors>
            {
                new Colors("Green", Colors.Green),
                new Colors("Red", Colors.Red),
                new Colors("Blue", Colors.Blue),
                new Colors("Gray", Colors.Gray),
                new Colors("White", Colors.White)
            };
            InitializeComponent();
		}
		string previousValueMaxFrame = "100";
        string previousValueTimePerFrame = "1000";

        private async void SelectFile_Tapped(object sender, TappedRoutedEventArgs e)
		{
			int frameTime = 33333;              // The amount of time per frame in uS (30 fps = 33333)
			int maxFrames;             // Max number of frames in the reconstructed video
            if (realTimeCheckbox.IsChecked == true)
            {
                frameTime = 33333;
            }
            // Check for invalid input
            try
			{
                if (realTimeCheckbox.IsChecked == false)
                {
                    frameTime = Int32.Parse(frameTimeTB.Text);
                }
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
				ContentDialog invaldInputDialogue = new ContentDialog()
				{
					Title = "Invalid Input",
					Content = "Please enter a valid number.",
					CloseButtonText = "Close"
				};

				await invaldInputDialogue.ShowAsync();
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

            


			// Grab ON and OFF colors from comboBox
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
                byte[] aedatFile = await AedatUtilities.ReadToBytes(file);
                string cameraTypeSearch = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref aedatFile);
                CameraParameters cam = AedatUtilities.ParseCameraModel(cameraTypeSearch);
                if (cam is null) {
                    ContentDialog invalidData = new ContentDialog()
                    {
                        Title = "Error",
                        Content = "Could not parse camera parameters.",
                        CloseButtonText = "Close"
                    };

                    return;
                }

                Func<byte[], int, int, int[]> getXY = AedatUtilities.GetXY_Cam(cam.cameraName);

                // Initilize writeable bitmap
                WriteableBitmap bitmap = new WriteableBitmap(cam.cameraX, cam.cameraY);
				InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryRandomAccessStream);
				Stream pixelStream = bitmap.PixelBuffer.AsStream();
				byte[] currentFrame = new byte[pixelStream.Length];

				byte[] currentDataEntry = new byte[AedatUtilities.dataEntrySize];
				int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);
				int lastTime = -999999;
				float playback_frametime = 1.0f / 30.0f;
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

					int[] XY = getXY(currentDataEntry, cam.cameraY, cam.cameraX);
					if (AedatUtilities.GetEventType(currentDataEntry)) 
					{
						AedatUtilities.SetPixel(ref currentFrame, XY[0], XY[1], onColor.Color, cam.cameraX); // ON event
					}
					else
					{
						AedatUtilities.SetPixel(ref currentFrame, XY[0], XY[1], offColor.Color, cam.cameraX); // OFF event
					}

					if(lastTime == -999999)
					{
						lastTime = timeStamp;
					}
					else
					{
						if (lastTime + frameTime <= timeStamp) // Collected events within specified timeframe, add frame to video
						{
							WriteableBitmap b = new WriteableBitmap(cam.cameraX, cam.cameraY);
							using (Stream stream = b.PixelBuffer.AsStream())
							{
								await stream.WriteAsync(currentFrame, 0, currentFrame.Length);
							}

							SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Premultiplied);
							CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);
							
							// Set playback framerate
							MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(playback_frametime));
	
							composition.Clips.Add(mediaClip);
							frameCount++;

							// Stop adding frames to video if max frames has been hit
							if (frameCount >= maxFrames)
							{
								break;
							}
							currentFrame = new byte[pixelStream.Length];
							lastTime = timeStamp;
						}
					}
					
				}

				// Create video file
				var savePicker = new FileSavePicker
				{
					SuggestedStartLocation = PickerLocationId.DocumentsLibrary
				};

				// Dropdown of file types the user can save the file as
				savePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });

				// Default file name if the user does not type one in or select a file to replace
				savePicker.SuggestedFileName = file.DisplayName;

				StorageFile sampleFile = await savePicker.PickSaveFileAsync();
				await composition.SaveAsync(sampleFile);
				composition = await MediaComposition.LoadAsync(sampleFile);

				// Get a generic encoding profile and set the width and height to the camera's width and height
				MediaEncodingProfile _MediaEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
				_MediaEncodingProfile.Video.Width = cam.cameraX;
                _MediaEncodingProfile.Video.Height = cam.cameraY;

				await composition.RenderToFileAsync(sampleFile, MediaTrimmingPreference.Precise, _MediaEncodingProfile);
				mediaSimple.Source = new Uri("ms-appx:///WBVideo.mp4");
			}
		}

		private void AllFrameCheckBox_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			this.previousValueMaxFrame = maxFramesTB.Text;
			maxFramesTB.IsReadOnly = true;
			maxFramesTB.IsEnabled = false;
			maxFramesTB.Text = "∞";
		}

		private void AllFrameCheckBox_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			maxFramesTB.Text = this.previousValueMaxFrame;
			maxFramesTB.IsReadOnly = false;
			maxFramesTB.IsEnabled = true;
		}

		private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			onColorCombo.SelectedIndex = 0;
			offColorCombo.SelectedIndex = 1;
		}

        private void RealTimeCheckbox_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.previousValueTimePerFrame = frameTimeTB.Text;
            frameTimeTB.Text = "Real Time";
            frameTimeTB.IsReadOnly = true;
            frameTimeTB.IsEnabled = false;
        }

        private void RealTimeCheckbox_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            frameTimeTB.Text= this.previousValueTimePerFrame ;
            frameTimeTB.IsReadOnly = false;
            frameTimeTB.IsEnabled = true;
        }
    }
}
