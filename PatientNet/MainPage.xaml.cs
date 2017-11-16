namespace PatientNet
{
    /**
     *  TODO:
     *      1) Add summaries
     *      2) Organize member functions
     *      3) Improve UI
     *      4) Add sound button enable / disable for HoloLens clicks
     *      5) Improve user notification textbox (something more like toast)
     *      6) Add headers to fields to better distinguish contact vs emt info
     * 
     */

    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Windows.System.Display;
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.Storage;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.IO;
    using Windows.UI.Xaml.Media.Imaging;

    public enum MessageType
    {
        Number, Skype, Email
    };

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DisplayRequest _displayRequest = new DisplayRequest();

        private const int FormattedPhoneLength = 13;
        private const int SleepTimeInMilliseconds = 1000;
        private const string SendSmsEndpoint = "api/v1/sendsms";
        private const string SendEmailEndpoint = "api/v1/sendemail";
        private const string RequestDoctorsEndpoint = "api/v1/requestdoctor";
        private const string AvailableDoctorsEndpoint = "api/v1/getavailabledoctors";

        private Logger logger;
        private const string LogFolder = ".logs";
        private HashSet<string> numbersSentTo = new HashSet<string>();
        private HashSet<string> emailsSentTo = new HashSet<string>();
        private HashSet<string> skypesSentTo = new HashSet<string>();
        private HashSet<MessageType> entersPressed = new HashSet<MessageType>();

        HttpClient httpClient;
        private const string ContentType = "application/json";

        private StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
        private const string skypeNameFile = ".skype.txt";

        private DispatcherTimer availableDoctorTimer = new DispatcherTimer();
        TimeSpan doctorTimerInterval = TimeSpan.FromSeconds(30);  // Query every 30 seconds

        private int oldPhoneLength = 0;  // Used to determine if change was insert or delete
        private bool skypeFocused = false;
        private bool contactFocused = false;
        private bool helpOn = false;

        private delegate void SendRequestEventHandler(object sender, RequestEventArgs e);
        private event SendRequestEventHandler SentRequest;  // Invoke on phone click
        private delegate void EnterEventHandler(object sender, EnterEventArgs e);
        private event EnterEventHandler EnterPressed;  // Invoke on enter

        /// <summary>
        /// Initializes page state upon opening the application
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            string time = DateTime.Now.ToString("yyyyMMddHHmmss");
            string logFolder = Path.Combine(storageFolder.Path, MainPage.LogFolder);
            var filename = Path.Combine(MainPage.LogFolder, time + ".log");
            if (!Directory.Exists(logFolder)) {
                Directory.CreateDirectory(logFolder);
            }

            this.logger = new Logger(filename);

            this.SentRequest += this.OnRequestSent;
            this.EnterPressed += this.OnEnterPressed;
            Application.Current.Resources["ToggleButtonBackgroundChecked"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPointerOver"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPressed"] = new SolidColorBrush(Colors.Transparent);

            LoadSavedData();  // Load the previous skype name

            InitHttpClient();  // Initialize http client to make api calls

            availableDoctorTimer.Interval = this.doctorTimerInterval;
            availableDoctorTimer.Tick += QueryAvailableDoctors;
            availableDoctorTimer.Start();

            QueryAvailableDoctors(null, null);  // Initial query for available doctors
        }

        /// <summary>
        /// Initializes the base url to connect to the server
        /// </summary>
        private void InitHttpClient()
        {
            this.httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://481patientnet.com:3001")
            };

            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MainPage.ContentType));
            this.httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));
        }

        /// <summary>
        /// Returns the number of doctors available to service an EMT's request
        /// </summary>
        private async void QueryAvailableDoctors(object sender, object e)
        {
            this.logger.Log("Querying available doctors");
            
            // Send empty content
            HttpContent content = new StringContent(string.Empty, Encoding.UTF8, MainPage.ContentType);
            HttpResponseMessage response = await this.httpClient.PostAsync(this.httpClient.BaseAddress + AvailableDoctorsEndpoint, content);

            // API Call
            if (response.IsSuccessStatusCode)
            {
                this.logger.Log(response.Content.ReadAsStringAsync().Result);
                var responseBody = response.Content.ReadAsStringAsync().Result;
                JObject s = JObject.Parse(responseBody);
                int numAvailableDoctors = (int)s["availabledoctors"];
                AvailableDoctors.Text = $"Available Doctors: {numAvailableDoctors}";
            }
            else
            {
                this.logger.Log("QueryAvailableDoctors: When querying for available doctors, did not get a success message.");
            }
        }

        /// <summary>
        /// Auto-populates the skype name box with the last name entered
        /// </summary>
        private async void LoadSavedData()
        {
            try
            {
                StorageFile storageFile = await this.storageFolder.GetFileAsync(MainPage.skypeNameFile);
                SkypeName.Text = await FileIO.ReadTextAsync(storageFile);
                this.logger.Log($"LoadSavedData: Found {skypeNameFile} in path {ApplicationData.Current.LocalFolder.Path}");
            }
            catch (FileNotFoundException)
            {
                this.logger.Log($"LoadSavedData: Did not find {skypeNameFile} in path {ApplicationData.Current.LocalFolder.Path}");
            }
        }

        /// <summary>
        /// Used to send skype names, phone numbers, and email addresses
        /// </summary>
        private async void SendHTTP(string endpoint, Dictionary<MessageType, string> sendTypes)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            string title = "Request Doctors";
            string success_message_both = "Successfully Notified Emergency Contact and Doctors";
            string success_message_contact = "Successfully Notified Emergency Contact";
            string success_message_doctor = "Successfully Notified Doctors";
            string success_message;
            
            try
            {
                string info;
                string emailString = "email";
                string skypeString = "skypeid";
                string numberString = "number";

                bool contains_email = sendTypes.ContainsKey(MessageType.Email);
                bool contains_skype = sendTypes.ContainsKey(MessageType.Skype);
                bool contains_number = sendTypes.ContainsKey(MessageType.Number);

                if (sendTypes.Count == 1 && contains_skype)
                {
                    info = $"{{ \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                    success_message = success_message_doctor;
                }
                else if (sendTypes.Count == 2)
                {
                    if (contains_email && contains_skype)
                    {
                        info = $"{{ \"{emailString}\": \"{sendTypes[MessageType.Email]}\", \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                        success_message = success_message_both;
                    }
                    else if (contains_number && contains_skype)
                    {
                        info = $"{{ \"{numberString}\": \"{sendTypes[MessageType.Number]}\", \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                        success_message = success_message_both;
                    }
                    else // information is only for emergency contact
                    {
                        info = $"{{ \"{numberString}\": \"{sendTypes[MessageType.Number]}\", \"{emailString}\": \"{sendTypes[MessageType.Email]}\" }}";
                        success_message = success_message_contact;
                    }
                }
                else if (sendTypes.Count == 3)
                {
                    info = $"{{ \"{numberString}\": \"{sendTypes[MessageType.Number]}\", \"{emailString}\": \"{sendTypes[MessageType.Email]}\", \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                    success_message = success_message_both;
                }
                else // sanity check
                {
                    return;
                }

                HttpContent content = new StringContent(info, Encoding.UTF8, MainPage.ContentType);
                this.logger.Log("Sending " + info + " to " + endpoint);

                HttpResponseMessage response = await this.httpClient.PostAsync(this.httpClient.BaseAddress + endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    NotifyUser($"{title}: {success_message}");

                    // Save Skype name for future use
                    StorageFile storageFile = await this.storageFolder.CreateFileAsync(MainPage.skypeNameFile, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(storageFile, sendTypes[MessageType.Skype]);
                    this.logger.Log($"LoadSavedData: Wrote to {skypeNameFile} in path {ApplicationData.Current.LocalFolder.Path}");
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

            // Disallow sending again for 1 second (debouncer)
            await Task.Delay(MainPage.SleepTimeInMilliseconds);

            e.Set.Remove(e.Content);
            this.logger.Log($"OnRequestSent: Removed {e.Content} from {nameof(e.Set)}");
        }

        private async void OnEnterPressed(object sender, EnterEventArgs e)
        {
            this.entersPressed.Add(e.Type);

            // Disallow pressing enter again for 1 second (debouncer)
            await Task.Delay(MainPage.SleepTimeInMilliseconds);

            this.entersPressed.Remove(e.Type);
            this.logger.Log($"OnEnterPressed: Removed {e.Type} from entersPressed set");
        }

        /// <summary>
        /// Handles when the EMT clicks the submit button
        /// </summary>
        private void RequestDoctors_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SkypeName.Text))
            {
                NotifyUser("Please enter a skype name.");
                return;
            }

            string phoneNumber = Phone.Text;
            string email = Email.Text;
            string skypeName = SkypeName.Text;
            Dictionary<MessageType, string> sendTypes = new Dictionary<MessageType, string>();

            // Handling Phone Number
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                if (phoneNumber.Length != MainPage.FormattedPhoneLength)
                {
                    NotifyUser($"Invalid Number. Please try again.");
                    return;
                }
                else if (this.numbersSentTo.Contains(phoneNumber))
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
            if (!string.IsNullOrWhiteSpace(email))
            {
                if (this.emailsSentTo.Contains(email))
                {
                    this.logger.Log($"Recently sent to {email}. Skipping.");
                    return;  // Don't do anything
                }

                // Add email to set of emails
                this.logger.Log($"Adding {email} to {this.emailsSentTo}");
                SentRequest.Invoke(this, new RequestEventArgs(this.emailsSentTo, email));
                sendTypes.Add(MessageType.Email, email);
            }

            // Handle sending skype name twice
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

        private void Skype_KeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Protect against enter registering twice
                if (this.entersPressed.Contains(MessageType.Skype))
                {
                    return;
                }

                Phone.Focus(FocusState.Pointer);
                this.logger.Log("Focusing on Phone!");
                this.EnterPressed.Invoke(this, new EnterEventArgs(MessageType.Number));
            }
        }

        private void Phone_KeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Protect against enter registering twice
                if (this.entersPressed.Contains(MessageType.Number))
                {
                    return;
                }

                Email.Focus(FocusState.Pointer);
                this.logger.Log("Focusing on Email!");
                this.EnterPressed.Invoke(this, new EnterEventArgs(MessageType.Email));
            }
        }

        private void Email_KeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Protect against enter registering twice
                if (this.entersPressed.Contains(MessageType.Email))
                {
                    return;
                }

                RequestDoctors_Click(sender, e);
            }
        }

        private void PhoneNumberFormatter(TextBox textBox, bool insert)
        {
            string value = textBox.Text;
            var oldSelectionStart = textBox.SelectionStart;

            value = new Regex(@"\D").Replace(value, string.Empty);

            if (string.IsNullOrEmpty(value))
            {
                // Do nothing
            }
            else if (value.Length < 4)
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

            textBox.Text = value;
            if (insert)
            {
                // Move cursor over to account for phone format character
                if (oldSelectionStart == 1 || oldSelectionStart == 5 || oldSelectionStart == 9)
                {
                    ++oldSelectionStart;
                }
            }
            else
            {
                // Move cursor behind phone format character
                if (oldSelectionStart == 5 || oldSelectionStart == 9)
                {
                    --oldSelectionStart;
                }
            }

            textBox.SelectionStart = oldSelectionStart;
        }

        private void Phone_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            bool insert = textBox.Text.Length > oldPhoneLength;
            PhoneNumberFormatter(textBox, insert);
            oldPhoneLength = textBox.Text.Length;
        }

        private void SkypeName_GotFocus(object sender, RoutedEventArgs e)
        {
            this.skypeFocused = true;
            UserHelpSkype.Visibility = Visibility.Visible;
        }

        private void SkypeName_LostFocus(object sender, RoutedEventArgs e)
        {
            this.skypeFocused = false;
            if (!this.helpOn)
            {
                UserHelpSkype.Visibility = Visibility.Collapsed;
            }
        }

        private void Contact_GotFocus(object sender, RoutedEventArgs e)
        {
            this.contactFocused = true;
            UserHelpContact.Visibility = Visibility.Visible;
        }

        private void Contact_LostFocus(object sender, RoutedEventArgs e)
        {
            this.contactFocused = false;
            if (!this.helpOn)
            {
                UserHelpContact.Visibility = Visibility.Collapsed;
            }
        }

        private void Contact_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            UserHelpContact.Visibility = Visibility.Visible;
        }

        private void Contact_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!(this.helpOn || contactFocused))
            {
                UserHelpContact.Visibility = Visibility.Collapsed;
            }
        }

        private void SkypeName_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            UserHelpSkype.Visibility = Visibility.Visible;
        }

        private void SkypeName_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!(this.helpOn || skypeFocused))
            {
                UserHelpSkype.Visibility = Visibility.Collapsed;
            }
        }

        private void HelpButtonClicked(object sender, RoutedEventArgs e)
        {
            if ((bool)this.HelpButton.IsChecked)
            {
                StepOne.Visibility = Visibility.Visible;
                StepTwo.Visibility = Visibility.Visible;
                StepThree.Visibility = Visibility.Visible;
                UserHelpSkype.Visibility = Visibility.Visible;
                UserHelpContact.Visibility = Visibility.Visible;
                this.helpOn = true;
            }
            else
            {
                StepOne.Visibility = Visibility.Collapsed;
                StepTwo.Visibility = Visibility.Collapsed;
                StepThree.Visibility = Visibility.Collapsed;
                UserHelpSkype.Visibility = Visibility.Collapsed;
                UserHelpContact.Visibility = Visibility.Collapsed;
                this.helpOn = false;
            }
        }

        // TODO: Need to actually disable sounds of clicks
        private void SoundButtonClicked(object sender, RoutedEventArgs e)
        {
            if ((bool)this.SoundButton.IsChecked)
            {
                SoundButtonPic.Source = new BitmapImage(new Uri("ms-appx:///Assets/No_Sound.png", UriKind.Absolute)); 
            }
            else
            {
                SoundButtonPic.Source = new BitmapImage(new Uri("ms-appx:///Assets/Sound.png", UriKind.Absolute));
            }
        }

        private async void NotifyUser(string content)
        {
            UserNotifications.Text = content;

            await Task.Delay(3000);

            // Clear text if notification did not change in the past three seconds
            if (UserNotifications.Text == content)
            {
                UserNotifications.Text = string.Empty;
            }
        }

    }
}
