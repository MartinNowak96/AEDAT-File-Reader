using AEDAT_File_Reader.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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

            }
        }


        
    }
}
