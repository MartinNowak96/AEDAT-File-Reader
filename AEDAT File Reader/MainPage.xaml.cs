using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AEDAT_File_Reader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        
        public MainPage()
        {
            
            this.InitializeComponent();
        }
        FileActivatedEventArgs FileArgs;

        private void nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                //ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                // Getting the Tag from Content (args.InvokedItem is the content of NavigationViewItem)
                
                if((string)args.InvokedItem == "Summary")
                {
                    ContentFrame.Navigate(typeof(SummaryView));
                }
                else if((string)args.InvokedItem == "Events")
                {
                    ContentFrame.Navigate(typeof(eventList));
                }
                else if ((string)args.InvokedItem == "Event Summaries")
                {
                    ContentFrame.Navigate(typeof(EventSummary));
                }
				else if ((string)args.InvokedItem == "Video")
				{
					ContentFrame.Navigate(typeof(videoPage));
				}
				else if ((string)args.InvokedItem == "Testing")
				{
					ContentFrame.Navigate(typeof(TestPage));
				}
				else if ((string)args.InvokedItem == "Event Chunks")
				{
					ContentFrame.Navigate(typeof(EventChunks));
				}
                else if ((string)args.InvokedItem == "Generate Frames")
                {
                    ContentFrame.Navigate(typeof(GenerateFrames));
                }
            }
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if(e.Parameter.GetType() == typeof(FileActivatedEventArgs))
            {
                FileArgs = (FileActivatedEventArgs)e.Parameter;
                var params2 = (FileActivatedEventArgs)e.Parameter;
                filePopup.IsOpen = true;
            }
        }

        private void ApplyFile_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Convert.ToBoolean(FileSummaryRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(SummaryView),FileArgs);
            }
			else if (Convert.ToBoolean(FileEventRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(eventList), FileArgs);
            }
            else if (Convert.ToBoolean(FileVideoRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(videoPage), FileArgs);
            }
            else if (Convert.ToBoolean(FileEventSummariesRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(EventSummary), FileArgs);
            }
            else if(Convert.ToBoolean(FileGenerateFramesRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(GenerateFrames), FileArgs);
            }
			else if (Convert.ToBoolean(FileEventChunksRadio.IsChecked))
            {
                ContentFrame.Navigate(typeof(EventChunks), FileArgs);
            }
        }
    }
}
