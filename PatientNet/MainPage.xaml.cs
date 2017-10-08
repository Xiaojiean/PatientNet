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
        // private SmsDevice2 device;

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

        private async void Call_Click(object sender, RoutedEventArgs e)
        {
            if (phoneNumber == null)
            {
                phoneNumber = Phone.Text;
            }
            if (phoneNumber.Length < 10)
            {
                Phone.Text = String.Empty;
                Phone.PlaceholderText = "Invalid Number";
                phoneNumber = null;
            }
            else
            {
                phoneNumber = "1" + phoneNumber;

                /*
                Contact recipient = new Windows.ApplicationModel.Contacts.Contact();
                recipient.Name = "Doctor";
                recipient.Phones.Add(new ContactPhone() { Kind = ContactPhoneKind.Mobile, Number = phoneNumber });

                string message = "TEST MESSAGE";

                SendSMS(recipient, message);
                */
                // TODO: Device capabilities?? Restricted ones

                SmsDevice2 device = SmsDevice2.GetDefault();
               // if (device == null)
               // {
                try
                {
                    device = SmsDevice2.GetDefault();
                }
                catch(Exception ex)
                {
                    Phone.Text = ex.Message;
                    return;
                }
                // }
                if (device != null)
                {
                    SmsTextMessage2 msg = new SmsTextMessage2();
                    msg.To = phoneNumber;
                    msg.Body = "This is a test";
                    SmsSendMessageResult result = await device.SendMessageAndGetResultAsync(msg);
                }
                else
                {
                    Phone.Text = "UH OH";
                }
                
            }
        }
    }
}
