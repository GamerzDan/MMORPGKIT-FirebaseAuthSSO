WILL ONLY RUN ON MOBILE DEVICES and NOT UNITY EDITOR

1. Register your App Id (bundle code)
https://developer.apple.com/account/resources/identifiers/add/bundleId

2. In App Capabilities, enable sign in with apple and register your App Id

3. Again go to Identifiers and add new Identifier for Services Ids
https://developer.apple.com/account/resources/identifiers/add/bundleId

4. Open the newly registered Service Id, select Sign In With Apple in options below and click configure.
Make sure your primary app id is selected from step #1

5. Enter your Firebase Auth callback url and project sub-domain in configuration screen.
You will get your Callback Url when you Add Apple as a Sign In provider in firebase Auth.
Normally it follows pattern
https://**FIREBASE_PROJECT_NAME**.firebaseapp.com/__/auth/handler

6. Save the configuration

7. Next go to Keys->Add a key
https://developer.apple.com/account/resources/authkeys/add

8. Again select Sign in with apple, configure it to select your app id and give a name to your key.
Save the key.

9. Download the key. IT CAN ONLY BE DOWNLOADED ONCE, SO MAKE SURE TO DOWNLOAD IT AND KEEP SAFE.

10. Now go back to Firebase Auth, add new provider, select Apple and enable it

11. Enter your services id from #3
Enter your Key Id from #9
Enter your Private Key Content from #9 (edit that downloaded file in notepad to see content)
Enter your Apple Team ID

12. Make sure you have Firebase (Auth) SDK 12.5.0 installed if targetting Android 14, 12.4.0 has bugs for Android 14

13. Update ANdroidManifest.xml if necessary

14. Call mmoLoginSSO_Apple() to initiate Apple Sign In