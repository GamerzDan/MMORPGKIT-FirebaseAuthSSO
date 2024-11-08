using UnityEngine.Events;
using UnityEngine.UI;
using LiteNetLibManager;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
//For Firebase
using Google;
using Firebase.Auth;
#endif
//For UniTask
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

namespace MultiplayerARPG.MMO
{
    public partial class UIMmoLogin : UIBase
    {
#if UNITY_ANDROID || UNITY_IOS
        Firebase.Auth.FirebaseAuth auth;
        GoogleSignInConfiguration googleConfig;
#endif

        public async void mmoLoginSSO_Google()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (auth == null)
            {
                Debug.Log("Firebase Auth Init");
                auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            }

            Debug.Log("mmoLoginSSO_Google");
            if (this.googleConfig == null)
            {
                Debug.Log("GoogleSignIn Config Init");
                //Init Google SignIn plugin first
                googleConfig = new GoogleSignInConfiguration
                {
                    RequestIdToken = true,
                    // Copy this value from the google-service.json file.
                    // oauth_client with type == 3
                    WebClientId = MMOClientInstance.Singleton.googleWebClientId
                };
                GoogleSignIn.Configuration = googleConfig;
                GoogleSignIn.Configuration.UseGameSignIn = false;
            }

            Debug.Log("Call Google SignIn");
            UISceneGlobal uiSceneGlobal = UISceneGlobal.Singleton;
            if (LoggingIn)
                return;
            // Clear stored username and password
            PlayerPrefs.SetString(keyUsername, string.Empty);
            PlayerPrefs.Save();
            LoggingIn = true;

            try
            {
                UniTask<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn().AsUniTask();
                await signIn.ContinueWith(task =>
                {
                //task is the GoogleSignInUser object
                Debug.Log("Google SignIn Done: " + task.IdToken + " " + task.UserId + " " + task.Email);
                MMOClientInstance.Singleton.RequestFirebaseAuthSSOLogin(task.IdToken, "", OnFirebaseAuthSSOLogin);
                });
            } catch (Exception e)
            {
                Debug.Log("mmoLoginSSO_Google exception " + e.Message + " - " + e.StackTrace);
                uiSceneGlobal.ShowMessageDialog("SSO Login Error", e.Message);
                LoggingIn = false;
            }
#else 
            Debug.Log("Called Google SSO Login on non-mobile platform !!");
#endif
        }

        public void OnFirebaseAuthSSOLogin(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseFirebaseAuthSSOLoginMessage response)
        {
            LoggingIn = false;
            Debug.Log(responseCode);
            Debug.Log(response.response);
            //If firebaseLogin was not success
            if (responseCode == AckResponseCode.Timeout)
            {
                UISceneGlobal.Singleton.ShowMessageDialog("Timeout Error", "MMO Server did not respond in time");
                return;
            }
            if (responseCode != AckResponseCode.Success)
            {
                //FirebaseErrorRes error = JsonUtility.FromJson<FirebaseErrorRes>(response.response);
                Debug.Log("OnFirebaseAuthSSOLogin Error: " + response.response);
                UISceneGlobal.Singleton.ShowMessageDialog("SSO Login Error", response.response);
                return;
            }
            //If success, try Kit's login
            //MMOClientInstance.Singleton.RequestUserLogin(Username, Password, OnLoginCustom);

            //Save userid/playerid, usefull for things like steamid
            PlayerPrefs.SetString("_PLAYERID_", response.username);
            Debug.Log("_PLAYERID_ " + response.username);
            //APIManager.instance.updatePlayerId(response.userId);

            if (onLoginSuccess != null)
            {
                onLoginSuccess.Invoke();
            }
        }
    }

    public partial class MMOClientInstance
    {
        [Header("FirebaseAuth SSO Config")]
        //For Google Sign in
        //https://console.cloud.google.com/apis/credentials
        //Setup Consent Screen/Branding and create OAuth Token for Android and iOS

        [Tooltip("Get it from your google-services.json file. " +
            "The Web client ID. This is needed for generating a server auth code for your backend server, or for generating an ID token. " +
            "This is the client_id value for the oauth client with client_type == 3.")]
        [SerializeField]
        internal string googleWebClientId;

        [SerializeField] 
        internal string googleIdToken;

        [SerializeField] 
        internal string googleIdSecret;

        /// <summary>
        /// Get it from your google-services.json file. 
        /// The Web client ID. This is needed for generating a server auth code for your backend server, or for generating an ID token. 
        /// This is the client_id value for the oauth client with client_type == 3.
        /// </summary>
        public void test()
        {

        }
    }
}