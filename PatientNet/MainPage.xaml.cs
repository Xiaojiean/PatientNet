using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Contacts;
using Windows.Devices.Sms;
//using Windows.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PatientNet
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture _mediaCapture;
        bool _isPreviewing;
        DisplayRequest _displayRequest = new DisplayRequest();
        string phoneNumber = null;
        private MainPage rootPage;
        string skypeName = null;

        public MainPage()
        {
            this.InitializeComponent();

            Application.Current.Suspending += Application_Suspending;
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                await CleanupCameraAsync();
                deferral.Complete();
            }
        }

        private async Task StartPreviewAsync()
        {
            try
            {

                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync();

                _displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                Debug.WriteLine("The app was denied access to the camera");
                return;
            }

            try
            {
                PreviewControl.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();
                _isPreviewing = true;
            }
            catch (System.IO.FileLoadException)
            {
                _mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }

        }

        private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                Debug.WriteLine("The camera preview can't be displayed because another app has exclusive access");
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !_isPreviewing)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await StartPreviewAsync();
                });
            }
        }

        private async Task CleanupCameraAsync()
        {
            if (_mediaCapture != null)
            {
                if (_isPreviewing)
                {
                    await _mediaCapture.StopPreviewAsync();
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PreviewControl.Source = null;
                    if (_displayRequest != null)
                    {
                        _displayRequest.RequestRelease();
                    }

                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                });
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPreviewAsync();
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await CleanupCameraAsync();
        }

        // Doesn't quite work
        private void PhoneDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                phoneNumber = Phone.Text;
                Call_Click(sender, e);
            }
        }
        private void SkypeNameDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                skypeName = SkypeName.Text;
                CallDoctor_Click(sender, e);
            }
        }

        // Try to send sms - TODO: Don't know if I need this function
        private async void ComposeSms(Windows.ApplicationModel.Contacts.Contact recipient, string messageBody,
            StorageFile attachmentFile, string mimeType)
        {
            var chatMessage = new Windows.ApplicationModel.Chat.ChatMessage();
            chatMessage.Body = messageBody;

            if (attachmentFile != null)
            {
                var stream = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(attachmentFile);

                var attachment = new Windows.ApplicationModel.Chat.ChatMessageAttachment(
                    mimeType,
                    stream);

                chatMessage.Attachments.Add(attachment);
            }

            var phone = recipient.Phones.FirstOrDefault<Windows.ApplicationModel.Contacts.ContactPhone>();
            if (phone != null)
            {
                chatMessage.Recipients.Add(phone.Number);
            }
            await Windows.ApplicationModel.Chat.ChatMessageManager.ShowComposeSmsMessageAsync(chatMessage);
        }

        private async void SendSMS(Contact recipient, string message)
        {
            var chatMessage = new Windows.ApplicationModel.Chat.ChatMessage();
            chatMessage.Body = message;

            var phone = recipient.Phones.FirstOrDefault<Windows.ApplicationModel.Contacts.ContactPhone>();
            if (phone != null)
            {
                chatMessage.Recipients.Add(phone.Number);
                await Windows.ApplicationModel.Chat.ChatMessageManager.ShowComposeSmsMessageAsync(chatMessage);
            }
        }

        /* Used to send phone numbers and skype names
         * type: 
         *  - 0: phone number
         *  - 1: skype name
         */
        private async void SendHTTP(string message, string endpoint, int type)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://481patientnet.com");
            string content_type = null;
            switch (type)
            { 
                case 0:
                    content_type = "application/phonenumber";
                    break;
                case 1:
                    content_type = "application/skypename";
                    break;
                default:
                    break;
            }
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(content_type));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));

            try
            {
                HttpContent content = new StringContent(message, Encoding.UTF8, content_type);
                System.Diagnostics.Debug.WriteLine("Sending " + message + " to " + endpoint);
                HttpResponseMessage response = await httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    ShowToast("SendHTTP()", "Successful");
                }
                else
                {
                    ShowToast("SendHTTP()", "Unsuccessful");
                }
            }
            catch (Exception ex)
            {
                ShowToast("Exception", ex.Message);
            }
        }


        private void Call_Click(object sender, RoutedEventArgs e)
        {
            if (phoneNumber == null)
            {
                phoneNumber = Phone.Text;
            }

            if (phoneNumber.Length < 10)
            {
                ShowToast("title", "Invalid Number");
                phoneNumber = null;
            }
            else
            {
                phoneNumber = "1" + phoneNumber;
                
                try
                {
                    string endpoint = @"/api/v1/sendsms";
                    SendHTTP(phoneNumber, endpoint, 0);
                }
                catch(Exception ex)
                {
                    ShowToast("Call_Click()", ex.Message);
                    return;
                }                
            }
        }

        private void CallDoctor_Click(object sender, RoutedEventArgs e)
        {
            if (skypeName == null)
            {
                skypeName = SkypeName.Text;
            }
            try
            {
                string endpoint = @"/api/v1/requestdoctor";
                SendHTTP(skypeName, endpoint, 1);
            }
            catch (Exception ex)
            {
                ShowToast("CallDoctor_Click()", ex.Message);
                return;
            }
        }

        private static void ShowToast(string title, string content)
        {
            XmlDocument toastXml = new XmlDocument();
            string xml = $@"
                <toast activationType='foreground'>
                <visual>
                <binding template='ToastGeneric'>
                    <text>{title}</text>
                    <text>{content}</text>
                </binding>
                </visual>
                </toast>";
            toastXml.LoadXml(xml);
            ToastNotification toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }
}
