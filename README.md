## PatientNet

# Run web application as doctor

* Go to [481patientnet.com](https://481patientnet.com)
* Log in with doctor credentials:

```
user: tsaoa@patientnet2.onmicrosoft.com
pass: Patientnet123
```
A meeting will be generated so the emergency contact can join.
* Wait for emergency contact to join the meeting 
  * Only audio calls are supported with the emergency contact

* Wait for notification from EMT
  * Note: You will need a <i>personal</i> Skype account and the [Skype Desktop app](https://www.skype.com/en/download-skype/skype-for-computer/)
  * A Skype "Call" button will be created and clicking it will automatically call the EMT.
  * Both video and audio are supported for this call

# To run web application as emergency contact
* Go to the link texted to you
  * The emergency contact's number will be provided by the EMT on the HoloLens
* Fill out your name to join the meeting
* Call the doctor
  * Note: Only audio calls are supported with the doctor

<b> Access emergency contact page without a text</b>

There are two options to test the emergency contact's web page without sending a text from the HoloLens:
1. Send a POST request from the terminal. On Linux:
```
curl -i -X POST -H 'Content-Type: application/json' -d '{"number":[number]}' https://481patientnet.com:3001/api/v1/sendsms
```
2. Find the short url from the doctor's web page.
   * On 481patientnet.com, open Developer's Settings to see the console.
   * Look for the response object in the following format:
   ```
   Resp: {
   	"kind": "urlshortener#url",
 	"id": "https://goo.gl/nKJr8u",
 	"longUrl": "http://481patientnet.com/client.html?org=patientnet2&user=tsaoa&id=CMYNVEIH"
	}
   ```
# To run HoloLens application as EMT:

* Put on the Hololens and ensure that the PatientNet application is installed
  * If it is not installed:
    * From the Hololens, open Settings > Network & Internet > Advanced Options to obtain the IP address of the Hololens
    * From a web browser, enter the IP address of the Hololens and hit enter to open the Windows Device Portal
    * Retrieve the latest version of PatientNet from PatientNet/AppPackages/ 
on this github page, and install the x86 version of the package (with its 2 dependencies) in the 'Apps' tab of the Windows Device Portal
* Enter the phone number of the emergency contact into the 'Enter Phone Number' textbox. For more detailed instructions, hover the Hololens pointer over the textbox.
* Click the 'Notify Emergency Contact' button to send a text message to the emergency contact, which contains a link to talk to the doctor.
* Enter the EMT's skype name into the 'Enter Skype Name' textbox. For more instructions, hover the Hololens pointer over the textbox.
* Click the 'Notify All Doctors' button to notify all available doctors at the hospital from which the EMT services.
* A doctor will respond to the Hololens notification, opening up a Skype call. Accept it to communicate with the doctor.

`
