using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AEDAT_File_Reader
{

	public class EventColor
	{

		public EventColor(string name, byte[] color)
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
		readonly ContentDialog videoExportCompleteDialog = new ContentDialog()
		{
			Title = "Done",
			Content = "Video export complete",
			CloseButtonText = "Close"
		};
		readonly ContentDialog invaldVideoSettingsDialog = new ContentDialog()
		{
			Title = "Invalid Input",
			Content = "One or more video settings are invalid.",
			CloseButtonText = "Close"
		};
		readonly ContentDialog invalidCameraDataDialog = new ContentDialog()
		{
			Title = "Error",
			Content = "Could not parse camera parameters.",
			CloseButtonText = "Close"
		};

		public ObservableCollection<EventColor> colors;
		public videoPage()
		{

			colors = new ObservableCollection<EventColor>
			{
				new EventColor("Green", EventColor.Green),
				new EventColor("Red", EventColor.Red),
				new EventColor("Blue", EventColor.Blue),
				new EventColor("Gray", EventColor.Gray),
				new EventColor("White", EventColor.White)
			};
			InitializeComponent();
		}
		string previousValueMaxFrame = "100";
		string previousValueTimePerFrame = "1000";


		private async void SelectFile_Tapped(object sender, TappedRoutedEventArgs e)
		{
			EventColor onColor;
			EventColor offColor;
			int frameTime;
			int maxFrames;
			float fps;

			try
			{
				// Grab video reconstruction settings from GUI
				// Will throw a FormatException if input is invalid (negative numbers or input has letters)
				(frameTime, maxFrames, onColor, offColor, fps) = ParseVideoSettings();
			}
			catch (FormatException)
			{
				await invaldVideoSettingsDialog.ShowAsync();
				return;
			}

			// Select AEDAT file to be converted to video
			var picker = new FileOpenPicker
			{
				ViewMode = PickerViewMode.Thumbnail,
				SuggestedStartLocation = PickerLocationId.PicturesLibrary
			};
			picker.FileTypeFilter.Add(".AEDAT");


			// Select AEDAT file to be converted
			StorageFile file = await picker.PickSingleFileAsync();
			if (file == null) return;

			byte[] aedatFile = await AedatUtilities.ReadToBytes(file);

			// Determine camera type from AEDAT header
			string cameraTypeSearch = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref aedatFile);
			CameraParameters cam = AedatUtilities.ParseCameraModel(cameraTypeSearch);
			if (cam == null)
			{
				await invalidCameraDataDialog.ShowAsync();
				return;
			}

			float playback_frametime = 1.0f / fps;
			MediaComposition composition;
			if (playbackType.IsOn)
			{
				composition = await TimeBasedReconstruction(aedatFile, cam, onColor, offColor, frameTime, maxFrames, playback_frametime);
			}
			else
			{
				int numOfEvents = Int32.Parse(numOfEventInput.Text);
				composition = await EventBasedReconstruction(aedatFile, cam, onColor, offColor, numOfEvents, maxFrames, playback_frametime);
			}
			SaveCompositionToFile(composition, file.DisplayName, cam.cameraX, cam.cameraY);
		}

		private static Stream InitBitMap(CameraParameters cam)
		{
			// Initilize writeable bitmap
			WriteableBitmap bitmap = new WriteableBitmap(cam.cameraX, cam.cameraY); // init image with camera size
			InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
			Stream pixelStream = bitmap.PixelBuffer.AsStream();
			return pixelStream;
		}

		public async Task<MediaComposition> TimeBasedReconstruction(byte[] aedatFile, CameraParameters cam, EventColor onColor, EventColor offColor, int frameTime, int maxFrames, float playback_frametime)
		{
			showLoading.IsActive = true;
			backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Visible;
			MediaComposition composition = new MediaComposition();
			int lastTime = -999999;
			int timeStamp;
			int frameCount = 0;
			Stream pixelStream = InitBitMap(cam);
			byte[] currentFrame = new byte[pixelStream.Length];
			int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);   // find end of aedat header
			byte[] currentDataEntry = new byte[AedatUtilities.dataEntrySize];

			// Read through AEDAT file
			for (int i = endOfHeaderIndex, length = aedatFile.Length; i < length; i += AedatUtilities.dataEntrySize)    // iterate through file, 8 bytes at a time.
			{
				for (int j = 0; j < 8; j++)    // from i get the next 8 bytes
				{
					currentDataEntry[j] = aedatFile[i + (7-j)];
				}
				timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes, uS

				int[] XY = cam.getXY(currentDataEntry, cam.cameraY, cam.cameraX);
				AedatUtilities.SetPixel(ref currentFrame, XY[0], XY[1], (cam.getEventType(currentDataEntry) ? onColor.Color : offColor.Color), cam.cameraX);


				if (lastTime == -999999)
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

						SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Ignore);
						CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);

						// Set playback framerate
						MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(playback_frametime));

						composition.Clips.Add(mediaClip);
						frameCount++;

						// Stop adding frames to video if max frames has been reached
						if (frameCount >= maxFrames)
						{
							break;
						}
						currentFrame = new byte[pixelStream.Length];
						lastTime = timeStamp;
					}
				}

			}

			return composition;

		}

		public async Task<MediaComposition> EventBasedReconstruction(byte[] aedatFile, CameraParameters cam, EventColor onColor, EventColor offColor, int eventsPerFrame, int maxFrames, float playback_frametime)
		{
			MediaComposition composition = new MediaComposition();
			int frameCount = 0;
			int eventCount = 0;
			Stream pixelStream = InitBitMap(cam);
			byte[] currentFrame = new byte[pixelStream.Length];
			int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);   // find end of aedat header
			byte[] currentDataEntry = new byte[AedatUtilities.dataEntrySize];

			// Read through AEDAT file
			for (int i = endOfHeaderIndex, length = aedatFile.Length; i < length; i += AedatUtilities.dataEntrySize)    // iterate through file, 8 bytes at a time.
			{
				for (int j = 0; j < 8; j++)    // from i get the next 8 bytes
				{
					currentDataEntry[j] = aedatFile[i + (7 - j)];
				}
				int[] XY = cam.getXY(currentDataEntry, cam.cameraY, cam.cameraX);
				AedatUtilities.SetPixel(ref currentFrame, XY[0], XY[1], (cam.getEventType(currentDataEntry) ? onColor.Color : offColor.Color), cam.cameraX);

				eventCount++;
				if (eventCount >= eventsPerFrame) // Collected events within specified timeframe, add frame to video
				{
					eventCount = 0;
					WriteableBitmap b = new WriteableBitmap(cam.cameraX, cam.cameraY);
					using (Stream stream = b.PixelBuffer.AsStream())
					{
						await stream.WriteAsync(currentFrame, 0, currentFrame.Length);
					}

					SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(b.PixelBuffer, BitmapPixelFormat.Bgra8, b.PixelWidth, b.PixelHeight, BitmapAlphaMode.Ignore);
					CanvasBitmap bitmap2 = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice.GetSharedDevice(), outputBitmap);

					// Set playback framerate
					MediaClip mediaClip = MediaClip.CreateFromSurface(bitmap2, TimeSpan.FromSeconds(playback_frametime));
					composition.Clips.Add(mediaClip);
					
					frameCount++;
					// Stop adding frames to video if max frames has been reached
					if (frameCount >= maxFrames)
					{
						return composition;
					}
					currentFrame = new byte[pixelStream.Length];
				}
				

			}

			return composition;

		}


		private async void SaveCompositionToFile(MediaComposition composition, string suggestedFileName, uint vidX, uint vidY)
		{
			var savePicker = new FileSavePicker
			{
				SuggestedStartLocation = PickerLocationId.DocumentsLibrary
			};
			// Dropdown of file types the user can save the file as
			savePicker.FileTypeChoices.Add("MP4", new List<string>() { ".mp4" });
			// Default file name if the user does not type one in or select a file to replace
			savePicker.SuggestedFileName = suggestedFileName;

			// Get name and location for saved video file
			StorageFile sampleFile = await savePicker.PickSaveFileAsync();
			if (sampleFile == null)
			{
				backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				showLoading.IsActive = false;
				return;
			}

			await composition.SaveAsync(sampleFile);
			composition = await MediaComposition.LoadAsync(sampleFile);
			
			// Get a generic encoding profile and match the width and height to the camera's width and height
			MediaEncodingProfile _MediaEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
			_MediaEncodingProfile.Video.Width = vidX;
			_MediaEncodingProfile.Video.Height = vidY;



			var saveOperation =  composition.RenderToFileAsync(sampleFile, MediaTrimmingPreference.Precise, _MediaEncodingProfile);
			//mediaSimple.Source = new Uri("ms-appx:///WBVideo.mp4");
			saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
				{
					Debug.WriteLine(progress);
					//ShowErrorMessage(string.Format("Saving file... Progress: {0:F0}%", progress));
				}));
			});
			saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(async () =>
				{
					backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
					showLoading.IsActive = false;
					await videoExportCompleteDialog.ShowAsync();

				}));
			});
			
		}

		private (int, int, EventColor, EventColor, float) ParseVideoSettings()
		{
			int frameTime = 33333;  // The amount of time per frame in uS (30 fps = 33333)
			int maxFrames;          // Max number of frames in the reconstructed video
			float fps = framerateCombo.SelectedIndex == 1 ? 60.0f : 30.0f; ;
			if (realTimeCheckbox.IsChecked == true)
			{
				frameTime = 33333;
				if(framerateCombo.SelectedIndex == 1)
				{
					frameTime = 33333 / 2;
				}
			}
			else
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


			if (maxFrames <= 0 || frameTime <= 0)
			{
				throw new FormatException();
			}

			// Grab ON and OFF colors from comboBox
			EventColor onColor = onColorCombo.SelectedItem as EventColor;
			EventColor offColor = offColorCombo.SelectedItem as EventColor;

			return (frameTime, maxFrames, onColor, offColor, fps);
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
			frameTimeTB.Text = this.previousValueTimePerFrame;
			frameTimeTB.IsReadOnly = false;
			frameTimeTB.IsEnabled = true;
		}

		private void PlaybackType_Toggled(object sender, Windows.UI.Xaml.RoutedEventArgs e)
		{
			try
			{
				if (playbackType.IsOn)
				{
					numOfEventInput.IsEnabled = false;
					realTimeCheckbox.IsEnabled = true;
					frameTimeTB.IsEnabled = true;
				}
				else
				{
					numOfEventInput.IsEnabled = true;
					realTimeCheckbox.IsEnabled = false;
					frameTimeTB.IsEnabled = false;
				}
			}
			catch
			{

			}
			
		}
	}
}
