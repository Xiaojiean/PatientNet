namespace PatientNet
{
    /**
     *  TODO:
     *      1) Add summaries
     *      2) Organize member functions
     *      3) Improve UI
     *      4) Let EMT send twice but not make two requests? Just update old request...
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
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using System.Text;
    using System.Text.RegularExpressions;

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

        private Logger logger = new Logger();
        private HashSet<string> numbersSentTo = new HashSet<string>();
        private HashSet<string> emailsSentTo = new HashSet<string>();
        private HashSet<string> skypesSentTo = new HashSet<string>();
        private HashSet<MessageType> entersPressed = new HashSet<MessageType>();

        private int oldPhoneLength = 0;  // Used to determine if change was insert or delete
        private bool skypeFocused = false;
        private bool contactFocused = false;
        private bool helpOn = false;

        private delegate void SendRequestEventHandler(object sender, RequestEventArgs e);
        private event SendRequestEventHandler SentRequest;  // Invoke on phone click
        private delegate void EnterEventHandler(object sender, EnterEventArgs e);
        private event EnterEventHandler EnterPressed;  // Invoke on enter

        public MainPage()
        {
            this.InitializeComponent();
            this.SentRequest += this.OnRequestSent;
            this.EnterPressed += this.OnEnterPressed;
            Application.Current.Resources["ToggleButtonBackgroundChecked"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPointerOver"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPressed"] = new SolidColorBrush(Colors.Transparent);
            var task = SkypeNameDataSource.CreateSkypeNameDataAsync();
        }

        /// <summary>
        /// Used to send phone numbers and skype names
        /// </summary>
        private async void SendHTTP(string endpoint, Dictionary<MessageType, string> sendTypes)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://481patientnet.com:3001");
            string content_type = "application/json";
            string title = "Request Doctors";
            string success_message_both = "Successfully Notified Emergency Contact and Doctors";
            string success_message_contact = "Successfully Notified Emergency Contact";
            string success_message_doctor = "Successfully Notified Doctors";
            string success_message = String.Empty;

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(content_type));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("utf-8"));
            
            try
            {
                string info = String.Empty;
                string emailString = "email";
                string skypeString = "skypeid";
                string numberString = "number";

                if (sendTypes.Count == 1)
                {
                    if (sendTypes.ContainsKey(MessageType.Email))
                    {
                        info = $"{{ \"{emailString}\": \"{sendTypes[MessageType.Email]}\" }}";
                        success_message = success_message_contact;
                    }
                    else if (sendTypes.ContainsKey(MessageType.Number))
                    {
                        info = $"{{ \"{numberString}\": \"{sendTypes[MessageType.Number]}\" }}";
                        success_message = success_message_contact;
                    }
                    else // MessageType.Skype
                    {
                        info = $"{{ \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                        success_message = success_message_doctor;
                    }
                }
                else if (sendTypes.Count == 2)
                {
                    if (sendTypes.ContainsKey(MessageType.Email) && sendTypes.ContainsKey(MessageType.Skype))
                    {
                        info = $"{{ \"{emailString}\": \"{sendTypes[MessageType.Email]}\", \"{skypeString}\": \"{sendTypes[MessageType.Skype]}\" }}";
                        success_message = success_message_both;
                    }
                    else if (sendTypes.ContainsKey(MessageType.Number) && sendTypes.ContainsKey(MessageType.Skype))
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

        private void RequestDoctors_Click(object sender, RoutedEventArgs e)
        {
            // I think we made it so that we could just notify an emergency contact?
            /*
            if (string.IsNullOrWhiteSpace(SkypeName.Text))
            {
                NotifyUser("Please enter a skype name.");
                return;
            }
            */

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
            // UserHelpText.Text = "Notify Emergency Contact sends a text or email to the specified number containing a link to the emergency contact PatientNet portal.";
            // UserHelpText.Text = "Please enter either the emergency contact's phone number or email address (or both). A text or email to the specified number containing a link to the emergency contact PatientNet portal.";
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

        // TODO: You have a Toggle Button, need a step for the on click, and a step for off click
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

        // TODO:
        // Working on auto-fill: https://github.com/Microsoft/Windows-universal-samples/blob/master/Samples/XamlAutoSuggestBox/cs/Scenario1.xaml.cs 
        // Nothing works yet

        private void SkypeName_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only get results when it was a user typing, 
            // otherwise assume the value got filled in by TextMemberPath 
            // or the handler for SuggestionChosen.
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var matchingNames = SkypeNameDataSource.GetMatchingNames(sender.Text);
                sender.ItemsSource = matchingNames;
                //Set the ItemsSource to be your filtered dataset
                //sender.ItemsSource = dataset;
            }
        }
    }
}
