using AEDAT_File_Reader.Models;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GenerateFrames : Page
    {
        public ObservableCollection<EventColor> colors;
        public GenerateFrames()
        {

            colors = new ObservableCollection<EventColor>
            {
                new EventColor("Green", EventColor.Green),
                new EventColor("Red", EventColor.Red),
                new EventColor("Blue", EventColor.Blue),
                new EventColor("Gray", EventColor.Gray),
                new EventColor("White", EventColor.White)
            };
            this.InitializeComponent();
        }

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
			IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
            if (files == null)
            {
                showLoading.IsActive = false;
                backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

			var picker2 = new FolderPicker
			{
				ViewMode = PickerViewMode.Thumbnail,
				SuggestedStartLocation = PickerLocationId.PicturesLibrary
			};
			picker2.FileTypeFilter.Add("*");




			// Select AEDAT file to be converted
			StorageFolder folder = await picker2.PickSingleFolderAsync();
			if (folder == null)
			{
				showLoading.IsActive = false;
				backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				return;
			}


			foreach (var file in files)
			{

				byte[] aedatFile = await AedatUtilities.ReadToBytes(file);

				// Determine camera type from AEDAT header
				string cameraTypeSearch = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref aedatFile);
				CameraParameters cam = AedatUtilities.ParseCameraModel(cameraTypeSearch);
				if (cam == null)
				{
					await invalidCameraDataDialog.ShowAsync();
					return;
				}
				showLoading.IsActive = true;
				backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Visible;
				float playback_frametime = 1.0f / fps;

				StorageFolder folder2 = await folder.CreateFolderAsync(file.Name.Replace(".aedat", "") + (playbackType.IsOn ? " time based" : " event based") + " Frames");

				if (playbackType.IsOn)
				{
					await TimeBasedReconstruction(aedatFile, cam, onColor, offColor, frameTime, maxFrames, folder2, file.Name.Replace(".aedat", ""));
				}
				else
				{
					int numOfEvents = Int32.Parse(numOfEventInput.Text);
					await EventBasedReconstruction(aedatFile, cam, onColor, offColor, numOfEvents, maxFrames, folder2, file.Name.Replace(".aedat", ""));
				}
			}
            showLoading.IsActive = false;
            backgroundTint.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

        }

        private static Stream InitBitMap(CameraParameters cam)
        {
            // Initilize writeable bitmap
            WriteableBitmap bitmap = new WriteableBitmap(cam.cameraX, cam.cameraY); // init image with camera size
            InMemoryRandomAccessStream inMemoryRandomAccessStream = new InMemoryRandomAccessStream();
            Stream pixelStream = bitmap.PixelBuffer.AsStream();
            return pixelStream;
        }

        public async Task TimeBasedReconstruction(byte[] aedatFile, CameraParameters cam, EventColor onColor, EventColor offColor, int frameTime, int maxFrames, StorageFolder folder,string fileName)
        {

            int lastTime = -999999;
            int timeStamp;
            int frameCount = 0;
            Stream pixelStream = InitBitMap(cam);
            byte[] currentFrame = new byte[pixelStream.Length];
            int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);   // find end of aedat header

            // Read through AEDAT file
            for (int i = endOfHeaderIndex, length = aedatFile.Length; i < length; i += AedatUtilities.dataEntrySize)    // iterate through file, 8 bytes at a time.
            {
                AEDATEvent currentEvent = new AEDATEvent(aedatFile, i, cam);
            
                timeStamp = currentEvent.time;
                AedatUtilities.SetPixel(ref currentFrame, currentEvent.x, currentEvent.y, (currentEvent.onOff ? onColor.Color : offColor.Color), cam.cameraX);


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
                        try
                        {
                            var file = await folder.CreateFileAsync(fileName +frameCount+".png",CreationCollisionOption.GenerateUniqueName);
                            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                                Stream pixelStream2 = b.PixelBuffer.AsStream();
                                byte[] pixels = new byte[pixelStream2.Length];
                                await pixelStream2.ReadAsync(pixels, 0, pixels.Length);

                                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                                    (uint)b.PixelWidth,
                                                    (uint)b.PixelHeight,
                                                    96.0,
                                                    96.0,
                                                    pixels);
                                await encoder.FlushAsync();
                            }
                        }
                        catch { }
                        
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

        }

        public async Task EventBasedReconstruction(byte[] aedatFile, CameraParameters cam, EventColor onColor, EventColor offColor, int eventsPerFrame, int maxFrames, StorageFolder folder, string fileName)
        {
            int frameCount = 0;
            int eventCount = 0;
            Stream pixelStream = InitBitMap(cam);
            byte[] currentFrame = new byte[pixelStream.Length];
            int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref aedatFile);   // find end of aedat header

            // Read through AEDAT file
            for (int i = endOfHeaderIndex, length = aedatFile.Length; i < length; i += AedatUtilities.dataEntrySize)    // iterate through file, 8 bytes at a time.
            {
                AEDATEvent currentEvent = new AEDATEvent(aedatFile, i, cam);

                AedatUtilities.SetPixel(ref currentFrame, currentEvent.x, currentEvent.y, (currentEvent.onOff ? onColor.Color : offColor.Color), cam.cameraX);

                eventCount++;
                if (eventCount >= eventsPerFrame) // Collected events within specified timeframe, add frame to video
                {
                    eventCount = 0;
                    WriteableBitmap b = new WriteableBitmap(cam.cameraX, cam.cameraY);
                    using (Stream stream = b.PixelBuffer.AsStream())
                    {
                        await stream.WriteAsync(currentFrame, 0, currentFrame.Length);
                    }
                    var file = await folder.CreateFileAsync(fileName + frameCount + ".png");
                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                        Stream pixelStream2 = b.PixelBuffer.AsStream();
                        byte[] pixels = new byte[pixelStream2.Length];
                        await pixelStream2.ReadAsync(pixels, 0, pixels.Length);

                        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                            (uint)b.PixelWidth,
                                            (uint)b.PixelHeight,
                                            96.0,
                                            96.0,
                                            pixels);
                        await encoder.FlushAsync();
                    }


                    frameCount++;
                    // Stop adding frames to video if max frames has been reached
                    if (frameCount >= maxFrames)
                    {
                        return;
                    }
                    currentFrame = new byte[pixelStream.Length];
                }


            }

            return;

        }

        private (int, int, EventColor, EventColor, float) ParseVideoSettings()
        {
            int frameTime = 33333;  // The amount of time per frame in uS (30 fps = 33333)
            int maxFrames;          // Max number of frames in the reconstructed video
            float fps = framerateCombo.SelectedIndex == 1 ? 60.0f : 30.0f; ;
            if (realTimeCheckbox.IsChecked == true)
            {
                frameTime = 33333;
                if (framerateCombo.SelectedIndex == 1)
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
