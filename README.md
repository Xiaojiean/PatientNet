## PatientNet

# To run HoloLens application as EMT:

* Put on the Hololens and ensure that the PatientNet application is installed
  * If it is not installed:
    * From the Hololens, open Settings > Network & Internet > Advanced Options to obtain the IP address of the Hololens
    * From a web browser, enter the IP address of the Hololens and hit enter to open the Windows Device Portal
      * Username: PatientNet
      * Password: Chesney
    * Retrieve the latest version of PatientNet from PatientNet/AppPackages/ on this github page, and install the x86 version of the package (with its 2 dependencies) in the 'Apps' tab of the Windows Device Portal
* Enter the EMT's skype name (e.g. eric.hwang8) into the 'Enter Skype Name' textbox. For more instructions, hover the Hololens pointer over the textbox.
* Enter the phone number and/or email address of the emergency contact into the 'Enter Contact Phone' and 'Enter Contact Email' textboxes, respectively. For more detailed instructions, hover the Hololens pointer over either textbox. 
* Click the 'Notify Parties' button to request a doctor at the hospital and notify the emergency contact via text message, email, or both. Note that this request will be successful only if a skype name is entered. The contact information is not required, but is recommended.
* For general help on using the applcation, click the question mark icon at the top right of the application.
* To increase or decrease the font size, click the 'AA' icon on the top right of the application.

# Run web application as doctor

* Go to [481patientnet.com](https://481patientnet.com)
* Log in with doctor credentials:
   * Email us for the passwords. We don't want them posted on Github.
   
There will be a list of EMT requests. Clicking on the EMT Skype name will automatically generate a meeting for the emergency contact and open Skype to call the EMT.

Note:
  * Only audio calls are supported with the emergency contact
  * You will need a <i>personal</i> Skype account and the classic [Skype Desktop app](https://www.skype.com/en/download-skype/skype-for-computer/) (the built-in Windows 10 Skype app doesn't support Hololens drawing)
  * Both video and audio are supported for the call with the EMT
    * If the EMT's video is blank, please follow instructions [here](https://forums.hololens.com/discussion/2343/hololens-add-in-is-causing-black-screen) to remedy

# To run web application as emergency contact
* Go to the link texted and/or emailed to you
  * The emergency contact's number and/or email will be provided by the EMT on the HoloLens
* Fill out your name to join the meeting
* Call the doctor
  * Note: Only audio calls are supported with the doctor

# Linux Commands to test API calls without using HoloLens

There are two options to test the emergency contact's web page without sending a text from the HoloLens:

1. Send a request for a doctor:
```
curl -i -X POST -H 'Content-Type: application/json' -d '{"skypeid":[id], "email":[example@user.com], "number":[number]}' https://481patientnet.com:3001/api/v1/requestdoctor
```
Note: Only "skypeid" is required.

2. Query the number of available doctors:
```
curl -i -X POST https://481patientnet.com:3001/api/v1/getavailabledoctors
```
