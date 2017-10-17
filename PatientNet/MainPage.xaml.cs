namespace PatientNet
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Windows.ApplicationModel;
    using Windows.Graphics.Display;
    using Windows.Media.Capture;
    using Windows.System.Display;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using System.Text;
    using Windows.Data.Xml.Dom;
    using Windows.UI.Notifications;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture _mediaCapture;
        bool _isPreviewing;
        DisplayRequest _displayRequest = new DisplayRequest();

        private const int FormattedPhoneLength = 13;

        private HashSet<string> numbersSentTo = new HashSet<string>();
        private string phoneNumber = null;
        private string skypeName = null;

        private delegate void PhoneClickedEventHandler(object sender, PhoneClickEventArgs e);
        private event PhoneClickedEventHandler PhoneClicked;  // Invoke on phone click

        enum MessageType
        {
            Number, Skype
        };

        public MainPage()
        {
            this.InitializeComponent();
            PhoneClicked += OnPhoneClicked;
            Application.Current.Suspending += Application_Suspending;
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
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

        private void PhoneDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                phoneNumber = Phone.Text;
                PhoneClick(sender, e);
            }
        }

        private void SkypeNameDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                skypeName = SkypeName.Text;
                CallDoctorClick(sender, e);
            }
        }

        /* Used to send phone numbers and skype names
         * type: 
         *  - 0: phone number
         *  - 1: skype name
         */
        private async void SendHTTP(string message, string endpoint, MessageType type)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://481patientnet.com:3001");
            string content_type = "application/json";
            string key = null;              // Key to send to API
            string title = null;            // Title of popup
            string success_message = null;  // Message to print on success

            switch (type)
            {
                case MessageType.Number:
                    key = "number";
                    title = "Notify Emergency Contact";
                    success_message = $"Successfully sent SMS to {message}.";
                    break;
                case MessageType.Skype:
                    key = "skypeid";
                    title = "Notify All Doctors";
                    success_message = "Successfully notified doctors! A doctor will initiate a Skype call soon.";
                    break;
                default:
                    throw new ArgumentException("Invalid message type.");
            }

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(content_type));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));

            try
            {
                string info = $"{{ \"{key}\": \"{message}\" }}";
                var serialized = JsonConvert.SerializeObject(info);
                HttpContent content = new StringContent(info, Encoding.UTF8, content_type);
                System.Diagnostics.Debug.WriteLine("Sending " + info + " to " + endpoint);
                HttpResponseMessage response = await httpClient.PostAsync(httpClient.BaseAddress + endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    ShowToast(title, success_message);
                }
                else
                {
                    ShowToast(title, "Unsuccessful");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            }
        }
        
        private void PhoneClick(object sender, RoutedEventArgs e)
        {
            phoneNumber = Phone.Text;

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                System.Diagnostics.Debug.WriteLine($"Got null or empty phone number. Skipping.");
                return;
            }
            else if (phoneNumber.Length != FormattedPhoneLength)
            {
                ShowToast("Invalid Number", phoneNumber);
                return;
            }

            if (numbersSentTo.Contains(phoneNumber))
            {
                System.Diagnostics.Debug.WriteLine($"Recently sent to {phoneNumber}. Skipping.");
                return;  // Don't do anything
            }

            // Add phone number to set of numbers
            System.Diagnostics.Debug.WriteLine($"Adding {phoneNumber} to sent numbers");
            PhoneClicked.Invoke(this, new PhoneClickEventArgs(phoneNumber));

            try
            {
                string parsedPhoneNumber = string.Format("{0}{1}{2}", phoneNumber.Substring(1, 3),
                    phoneNumber.Substring(5, 3), phoneNumber.Substring(9, 4));
                string endpoint = @"api/v1/sendsms";
                SendHTTP(parsedPhoneNumber, endpoint, MessageType.Number);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"When notifying contact, got error: {ex.Message}");
            }
        }

        private async void OnPhoneClicked(object sender, PhoneClickEventArgs e)
        {
            numbersSentTo.Add(e.Number);

            await Task.Delay(5000);

            numbersSentTo.Remove(e.Number);
            System.Diagnostics.Debug.WriteLine($"Removed {phoneNumber} from sent numbers");
        }

        private void CallDoctorClick(object sender, RoutedEventArgs e)
        {
            skypeName = SkypeName.Text;

            if (string.IsNullOrWhiteSpace(skypeName))
            {
                System.Diagnostics.Debug.WriteLine($"Got null or empty phone number. Skipping.");
                return;
            }

            try
            {
                // Append with "sip:"
                if (!skypeName.StartsWith("sip:"))
                {
                    skypeName = "sip:" + skypeName;
                }

                string endpoint = @"api/v1/requestdoctor";
                SendHTTP(skypeName, endpoint, MessageType.Skype);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"When notifying contact, got error: {ex.Message}");
                return;
            }
        }

        private string PhoneNumberFormatter(string value)
        {
            value = new Regex(@"\D").Replace(value, string.Empty);

            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            if (value.Length < 4)
            {
                value = string.Format("({0}", value.Substring(0, value.Length));
            }
            else if (value.Length < 7)
            {
                value = string.Format("({0}){1}", value.Substring(0, 3), value.Substring(3, value.Length - 3));
            }
            else if (value.Length < 11)
            {
                value = string.Format("({0}){1}-{2}", value.Substring(0, 3), value.Substring(3, 3), value.Substring(6));
            }
            else
            {
                value = value.Remove(value.Length - 1, 1);
                value = string.Format("({0}){1}-{2}", value.Substring(0, 3), value.Substring(3, 3), value.Substring(6));
            }

            return value;
        }

        private void PhoneTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            string number = textBox.Text;
            textBox.Text = PhoneNumberFormatter(number);

            // This gets kinda bad when the user tries to insert or delete from the middle
            if (textBox.Text.Length != 0)
            {
                textBox.SelectionStart = textBox.Text.Length;
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
