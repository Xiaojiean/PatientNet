namespace PatientNet
{
    /**
     *  TODO:
     *      1) Add summaries
     *      2) Organize member functions
     *      3) Improve UI
     * 
     */


    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Windows.ApplicationModel;
    using Windows.Graphics.Display;
    using Windows.Media.Capture;
    using Windows.System.Display;
    using Windows.UI;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture _mediaCapture;
        DisplayRequest _displayRequest = new DisplayRequest();
        bool _isPreviewing;
        MessageType input;

        private const int FormattedPhoneLength = 13;
        private const int SendSleepTimeInMilliseconds = 5000;
        private const string SendSmsEndpoint = "api/v1/sendsms";
        private const string SendEmailEndpoint = "api/v1/sendemail";
        private const string RequestDoctorsEndpoint = "api/v1/requestdoctor";

        private Logger logger = new Logger();
        private HashSet<string> numbersSentTo = new HashSet<string>();
        private HashSet<string> emailsSentTo = new HashSet<string>();
        private HashSet<string> skypesSentTo = new HashSet<string>();

        private delegate void SendRequestEventHandler(object sender, RequestEventArgs e);
        private event SendRequestEventHandler SentRequest;  // Invoke on phone click

        enum MessageType
        {
            Number, Skype, Email
        };

        public MainPage()
        {
            this.InitializeComponent();
            this.SentRequest += this.OnRequestSent;
            Application.Current.Suspending += Application_Suspending;
            /*
            PhonePic.Visibility = Visibility.Visible;
            EmailPic.Visibility = Visibility.Collapsed;
            */
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
                this.logger.Log("The app was denied access to the camera");
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
                this.logger.Log("The camera preview can't be displayed because another app has exclusive access");
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
                //PhoneClick(sender, e);
            }
        }

        private void SkypeNameDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // CallDoctorClick(sender, e);
            }
        }

        /// <summary>
        /// Used to send phone numbers and skype names
        /// </summary>
        // private async void SendHTTP(string message, string endpoint, MessageType type) // TODO: NEED TO CHANGE SendHTTP INTERFACE
        private async void SendHTTP(string endpoint, Dictionary<MessageType, string> sendTypes)
        {
            /*
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException(nameof(message));
            }
            */

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://481patientnet.com:3001");
            string content_type = "application/json";
            string title = "Request Doctors";
            string success_message = "Successfully Notified Emergency Contact and Doctors";

            /*
            string key = null;              // Key to send to API
            string title = null;            // Title of popup
            string success_message = null;  // Message to print on success

            switch (type)
            {
                case MessageType.Number:
                    key = "number";
                    title = "Notify Emergency Contact";
                    success_message = $"Successfully sent SMS to {PhoneNumberFormatter(message)}.";
                    break;
                case MessageType.Email:
                    key = "email";
                    title = "Notify Emergency Contact";
                    success_message = $"Successfully sent email to {message}.";
                    break;
                case MessageType.Skype:
                    key = "skypeid";
                    title = "Notify All Doctors";
                    success_message = "Successfully notified doctors! A doctor will initiate a Skype call soon.";
                    break;
                default:
                    throw new ArgumentException("Invalid message type.");
            }
            */

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(content_type));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));

            try
            {
                string info = String.Empty;

                if (sendTypes.Count == 2)
                {
                    if (sendTypes.ContainsKey(MessageType.Email))
                    {
                        info = $"{{ \"{"email"}\": \"{sendTypes[MessageType.Email]}\" \"{"skypeid"}\": \"{MessageType.Skype}\" }}";
                    }
                    else
                    {
                        info = $"{{ \"{"number"}\": \"{sendTypes[MessageType.Number]}\" \"{"skypeid"}\": \"{MessageType.Skype}\" }}";
                    }
                }
                else if (sendTypes.Count == 3)
                {
                    info = $"{{ \"{"number"}\": \"{sendTypes[MessageType.Number]}\" \"{"email"}\": \"{sendTypes[MessageType.Email]}\" \"{"skypeid"}\": \"{MessageType.Skype}\" }}";
                }
                else
                {
                    return;
                }

                var serialized = JsonConvert.SerializeObject(info);
                HttpContent content = new StringContent(info, Encoding.UTF8, content_type);
                this.logger.Log("Sending " + info + " to " + endpoint);

                HttpResponseMessage response = await httpClient.PostAsync(httpClient.BaseAddress + endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    NotifyUser($"{title}: {success_message}");
                }
                else
                {
                    NotifyUser($"{title}: Unsuccessful. Please try again.");
                }
            }
            catch (Exception ex)
            {
                this.logger.Log($"Exception: {ex.Message}");
            }
        }

        private async void OnRequestSent(object sender, RequestEventArgs e)
        {
            e.Set.Add(e.Content);

            await Task.Delay(MainPage.SendSleepTimeInMilliseconds);  // Disallow sending again for 5 seconds

            e.Set.Remove(e.Content);
            this.logger.Log($"Removed {e.Content} from {nameof(e.Set)}");
        }

        private void ButtonClick(object sender, RoutedEventArgs e)
        {
            if (SkypeName.Text == String.Empty)
            {
                NotifyUser("Please enter a skype name.");
                return;
            }
            if (Phone.Text == String.Empty && Email.Text == String.Empty)
            {
                NotifyUser($"Please enter a phone number and/or email address.");
                return;
            }

            string phoneNumber = Phone.Text;
            string email = Email.Text;
            string skypeName = SkypeName.Text;
            Dictionary<MessageType, string> sendTypes = new Dictionary<MessageType, string>();

            // Handling Phone Number
            if (phoneNumber != String.Empty)
            {
                if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length != MainPage.FormattedPhoneLength)
                {
                    NotifyUser($"Invalid Number. Please try again.");
                    return;
                }

                if (this.numbersSentTo.Contains(phoneNumber))
                {
                    this.logger.Log($"Recently sent to {phoneNumber}. Skipping.");
                    return;  // Don't do anything
                }

                // Add phone number to set of numbers
                this.logger.Log($"Adding {phoneNumber} to {this.numbersSentTo}");
                SentRequest.Invoke(this, new RequestEventArgs(this.numbersSentTo, phoneNumber));
                sendTypes.Add(MessageType.Number, phoneNumber);
            }

            // Handling Email
            if (email != String.Empty)
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    NotifyUser($"Invalid Email. Please try again.");
                    return;
                }

                // Add email to set of emails
                this.logger.Log($"Adding {email} to {this.emailsSentTo}");
                SentRequest.Invoke(this, new RequestEventArgs(this.emailsSentTo, email));
                sendTypes.Add(MessageType.Email, email);
            }

            // Handling Skype Name
            if (string.IsNullOrWhiteSpace(skypeName))
            {
                this.logger.Log($"Got null or empty skype name. Skipping.");
                NotifyUser("Please enter a skype name.");
                return;
            }

            if (this.skypesSentTo.Contains(skypeName))
            {
                this.logger.Log($"Recently sent to {skypeName}. Skipping.");
                return;  // Don't do anything
            }

            // Add skype name to set of skypes
            this.logger.Log($"Adding {skypeName} to {nameof(this.skypesSentTo)}");
            SentRequest.Invoke(this, new RequestEventArgs(this.skypesSentTo, skypeName));
            sendTypes.Add(MessageType.Skype, skypeName);

            // Sending HTTP Request containing all non-empty parameters
            try
            {
                string endpoint = RequestDoctorsEndpoint;
                SendHTTP(endpoint, sendTypes); // TODO: NEED TO CHANGE SendHTTP INTERFACE
            }
            catch (Exception ex)
            {
                this.logger.Log($"Error: when notifying contact, got exception: {ex.Message}");
                NotifyUser($"Error notifying contact: {ex.Message}");
            }
        }

        /*
        private void CallDoctorClick(object sender, RoutedEventArgs e)
        {
            string skypeName = SkypeName.Text;

            if (string.IsNullOrWhiteSpace(skypeName))
            {
                this.logger.Log($"Got null or empty skype name. Skipping.");
                NotifyUser("Please enter a skype name.");
                return;
            }

            if (this.skypesSentTo.Contains(skypeName))
            {
                this.logger.Log($"Recently sent to {skypeName}. Skipping.");
                return;  // Don't do anything
            }

            // Add skype name to set of skypes
            this.logger.Log($"Adding {skypeName} to {nameof(this.skypesSentTo)}");
            SentRequest.Invoke(this, new RequestEventArgs(this.skypesSentTo, skypeName));

            try
            { 
                string endpoint = RequestDoctorsEndpoint;
                SendHTTP(skypeName, endpoint, MessageType.Skype);
            }
            catch (Exception ex)
            {
                this.logger.Log($"Error: When requesting doctors, got exception: {ex.Message}");
                NotifyUser($"Error requesting doctors: {ex.Message}");
                return;
            }
        }
        */

        private void PhoneSelected(object sender, RoutedEventArgs e)
        {
            input = MessageType.Number;
            Phone.MaxLength = 13;
            Phone.PlaceholderText = "(XXX)XXX-XXXX";
            Phone.Text = string.Empty;
            Phone.IsEnabled = true;

            /*
            PhonePic.Visibility = Visibility.Visible;
            EmailPic.Visibility = Visibility.Collapsed;

            SelectPhoneBackground.Background = new SolidColorBrush(Colors.WhiteSmoke);
            SelectEmailBackground.Background = new SolidColorBrush(Colors.Transparent);
            */

            InputScope scope = new InputScope();
            InputScopeName scopeName = new InputScopeName();
            scopeName.NameValue = InputScopeNameValue.TelephoneNumber;
            scope.Names.Add(scopeName);
            Phone.InputScope = scope;
        }
    
        private void EmailSelected(object sender, RoutedEventArgs e)
        {
            input = MessageType.Email;
            Phone.MaxLength = 100;
            Phone.PlaceholderText = "johndoe@gmail.com";
            Phone.Text = string.Empty;
            Phone.IsEnabled = true;

            /*
            PhonePic.Visibility = Visibility.Collapsed;
            EmailPic.Visibility = Visibility.Visible;

            SelectPhoneBackground.Background = new SolidColorBrush(Colors.Transparent);
            SelectEmailBackground.Background = new SolidColorBrush(Colors.WhiteSmoke);
            */

            InputScope scope = new InputScope();
            InputScopeName scopeName = new InputScopeName();
            scopeName.NameValue = InputScopeNameValue.EmailNameOrAddress;
            scope.Names.Add(scopeName);
            Phone.InputScope = scope;
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
            if (input == MessageType.Number)
            {
                textBox.Text = PhoneNumberFormatter(number);

                // This gets kinda bad when the user tries to insert or delete from the middle
                if (textBox.Text.Length != 0)
                {
                    textBox.SelectionStart = textBox.Text.Length;
                }
            }
        }

        private async void NotifyUser(string content)
        {
            UserNotifications.Text = content;

            await Task.Delay(3000);

            if (UserNotifications.Text == content)
            {
                UserNotifications.Text = string.Empty;
            }
        }

        private void Phone_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // UserHelpText.Text = "Notify Emergency Contact sends a text or email to the specified number containing a link to the emergency contact PatientNet portal.";
            UserHelpText.Text = "Please enter either the emergency contact's phone number or email address (or both). A text or email to the specified number containing a link to the emergency contact PatientNet portal.";
        }

        private void Phone_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            UserHelpText.Text = string.Empty;
        }

        private void SkypeName_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            UserHelpSkype.Text = "Please enter the Skype Name associated with this Hololens.";
        }

        private void SkypeName_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            UserHelpSkype.Text = string.Empty;
        }
    }
}
