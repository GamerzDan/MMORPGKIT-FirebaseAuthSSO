using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLibManager;
using LiteNetLib.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
using System.Text.RegularExpressions;
using System;
#if UNITY_SERVER || UNITY_EDITOR
using Firebase.Auth;
#endif

namespace MultiplayerARPG.MMO
{
    public partial class CentralNetworkManager : LiteNetLibManager.LiteNetLibManager
    {
        [DevExtMethods("RegisterMessages")]
        protected void DevExtRegisterFirebaseAuthSSOMessages()
        {
#if UNITY_STANDALONE || UNITY_SERVER //|| UNITY_EDITOR
            Debug.Log("DevExt RegisterMessages FirebaseAuthSSO + " + MMORequestTypes.RequestFirebaseAuthSSO_Login);
            RegisterRequestToServer<RequestUserLoginMessage, ResponseFirebaseAuthSSOLoginMessage>(MMORequestTypes.RequestFirebaseAuthSSO_Login, HandleRequestFirebaseAuthSSOLogin);
            RegisterRequestToServer<RequestUserRegisterMessage, ResponseFirebaseAuthSSOLoginMessage>(MMORequestTypes.RequestFirebaseAuthSSO_Register, HandleRequestFirebaseAuthSSORegister);
#endif
        }
    }

    public static partial class MMORequestTypes
    {
        public const ushort RequestFirebaseAuthSSO_Login = 3333;
        public const ushort RequestFirebaseAuthSSO_Register = 3334;
    }

    /// <summary>
    /// General Response handler for firebase, we pass string or jsonText as response in it
    /// </summary>
    public struct ResponseFirebaseAuthSSOLoginMessage : INetSerializable
    {
        public string response;
        public UITextKeys message;
        /// <summary>
        /// This is mmorpgkit's internal userid
        /// </summary>
        public string userId;
        public string accessToken;
        public long unbanTime;
        /// <summary>
        /// This is the actual username or steamid in mmorgkit
        /// </summary>
        public string username;
        public void Deserialize(NetDataReader reader)
        {
            response = reader.GetString();
            message = (UITextKeys)reader.GetPackedUShort();
            userId = reader.GetString();
            accessToken = reader.GetString();
            unbanTime = reader.GetPackedLong();
            username = reader.GetString();
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(response);
            writer.PutPackedUShort((ushort)message);
            writer.Put(userId);
            writer.Put(accessToken);
            writer.PutPackedLong(unbanTime);
            writer.Put(username);
        }
    }

    public partial class CentralNetworkManager
    {
        Firebase.Auth.FirebaseAuth auth;
        protected string firebaseAuthSSOPass = @"AIzaSyA4sj5mUuvJIQWp1mdxm5Xbf_ffQLLPqIM";

        /// <summary>
        /// Custom Name validation to be used in delegate of NameValidating class
        /// Currently using it to disable name validation as we will use email for username
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected virtual bool firebaseAuthSSOCustomNameValidation(string name)
        {
            Debug.Log("Using customNameValidation");
            return true;
        }
        public bool RequestFirebaseAuthSSOLogin(string username, string password, ResponseDelegate<ResponseFirebaseAuthSSOLoginMessage> callback)
        {
            Debug.Log("CentralNetworkManager.RequestFirebaseAuthSSOLogin()");
            return ClientSendRequest(MMORequestTypes.RequestFirebaseAuthSSO_Login, new RequestUserLoginMessage()
            {
                username = username,
                password = password,
            }, responseDelegate: callback);
        }
        public bool RequestFirebaseAuthSSORegister(string email, string password, ResponseDelegate<ResponseFirebaseAuthSSOLoginMessage> callback)
        {
            return ClientSendRequest(MMORequestTypes.RequestFirebaseAuthSSO_Register, new RequestUserRegisterMessage()
            {
                username = email,
                password = password,
                email = email
            }, responseDelegate: callback);
        }
        protected async UniTaskVoid HandleRequestFirebaseAuthSSOLogin(
            RequestHandlerData requestHandler,
            RequestUserLoginMessage request,
            RequestProceedResultDelegate<ResponseFirebaseAuthSSOLoginMessage> result)
        {
            Debug.Log("HandleRequestFirebaseAuthSSOLogin");
#if UNITY_EDITOR || UNITY_SERVER
            string message = "";
            string username = request.username;
            string password = request.password;
            NameExtensions.overrideUsernameValidating = firebaseAuthSSOCustomNameValidation;
            //string email = request.email;
            Debug.Log("Pre API call");
            validateFirebaseAuthSSO(username, password, result, requestHandler);
            Debug.Log("Post API call");
#endif
        }

        /// <summary>
        /// Validates the IDToken sent by SSO Provider with FirebaseSSO
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="result"></param>
        public void validateFirebaseAuthSSO(string idtoken, string password, RequestProceedResultDelegate<ResponseFirebaseAuthSSOLoginMessage> result, RequestHandlerData requestHandler)
        {
#if UNITY_EDITOR || UNITY_SERVER
            if (auth == null)
            {
                Debug.Log("Firebase Auth Init");
                auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            }
            try
            {
                Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(idtoken, null);
                auth.SignInWithCredentialAsync(credential).AsUniTask().ContinueWith(authTask =>
                {
                    FirebaseUser firebaseUser = authTask;
                    //Check if passed steamid is same as ticket steamid
                    if (authTask == null || firebaseUser == null)
                    {
                        //TODO: Ban if steamids donot match
                        //This means the steamid client sent is not same as AuthTicket steamid and there is probably a hack attempt
                        //We can use here to ban this user
                        result.Invoke(AckResponseCode.Error,
                            new ResponseFirebaseAuthSSOLoginMessage()
                            {
                                response = "FirebaseSSO Validation Failed, null response",
                            });
                    }
                    else
                    {
                        Debug.Log("firebaseSSO Validate Response: " + firebaseUser.UserId);
                        /*
                        result.Invoke(AckResponseCode.Success,
                        new ResponseSteamAuthLoginMessage()
                        {
                            response = res.Text,
                        }); */

                        //Let's try to login the user from server
                        HandleFirebaseAuthSSOLogin(firebaseUser.UserId, result, requestHandler);
                    }

                }).Forget();
            } catch(Exception e)
            {
                Debug.Log("validateFirebaseAuthSSO Error: " + e.Message + " - " + e.StackTrace);
                result.Invoke(AckResponseCode.Error,
                    new ResponseFirebaseAuthSSOLoginMessage()
                    {
                        response = "validateFirebaseAuthSSO Error: " + e.Message,
                    });
            };
#endif
        }

        protected async UniTaskVoid HandleFirebaseAuthSSOLogin(string userid,
    RequestProceedResultDelegate<ResponseFirebaseAuthSSOLoginMessage> result, RequestHandlerData requestHandler)
        {
#if UNITY_SERVER //|| UNITY_EDITOR
            Debug.Log("HandleRequestSteamUserLogin");
            if (disableDefaultLogin)
            {
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_SERVICE_NOT_AVAILABLE,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_SERVICE_NOT_AVAILABLE.ToString()),
                });
                return;
            }

            long connectionId = requestHandler.ConnectionId;
            DatabaseApiResult<ValidateUserLoginResp> validateUserLoginResp = await DatabaseClient.ValidateUserLoginAsync(new ValidateUserLoginReq()
            {
                Username = userid,
                Password = firebaseAuthSSOPass
            });
            if (!validateUserLoginResp.IsSuccess)
            {
                Debug.Log("HandleFirebaseAuthSSOLogin ValidateUserLogin Failed");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR.ToString()),
                });
                return;
            }
            string userId = validateUserLoginResp.Response.UserId;
            string accessToken = string.Empty;
            long unbanTime = 0;
            if (string.IsNullOrEmpty(userId))
            {
                //// Try registering user using steamID
                HandleFirebaseAuthSSORegister(userid, result, requestHandler);
                return;
            }
            if (_userPeersByUserId.ContainsKey(userId) || MapContainsUser(userId))
            {
                Debug.Log("HandleFirebaseAuthSSOLogin User Already Logged in");
                // Kick the user from game
                if (_userPeersByUserId.ContainsKey(userId))
                {
                    KickClient(_userPeersByUserId[userId].connectionId, UITextKeys.UI_ERROR_ACCOUNT_LOGGED_IN_BY_OTHER);
                    //No longer being used, atleast in 1.85
                    //ServerTransport.ServerDisconnect(_userPeersByUserId[userId].connectionId);
                }
                //TODO: ENABLE WHEN UPDATE TO 1.77
                ClusterServer.KickUser(userId, UITextKeys.UI_ERROR_ACCOUNT_LOGGED_IN_BY_OTHER);
                RemoveUserPeerByUserId(userId, out _);
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_ALREADY_LOGGED_IN,
                });
                return;
            }

            DatabaseApiResult<GetUserUnbanTimeResp> unbanTimeResp = await DatabaseClient.GetUserUnbanTimeAsync(new GetUserUnbanTimeReq()
            {
                UserId = userId
            });
            if (!unbanTimeResp.IsSuccess)
            {
                Debug.Log("HandleFirebaseAuthSSOLogin UserUnbanTime Failed");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR.ToString()),
                });
                return;
            }
            unbanTime = unbanTimeResp.Response.UnbanTime;
            if (unbanTime > System.DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                Debug.Log("HandleFirebaseAuthSSOLogin User is Banned");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_USER_BANNED,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_USER_BANNED.ToString()),
                });
                return;
            }
            CentralUserPeerInfo userPeerInfo = new CentralUserPeerInfo();
            userPeerInfo.connectionId = connectionId;
            userPeerInfo.userId = userId;
            userPeerInfo.accessToken = accessToken = Regex.Replace(System.Convert.ToBase64String(System.Guid.NewGuid().ToByteArray()), "[/+=]", "");
            _userPeersByUserId[userId] = userPeerInfo;
            _userPeers[connectionId] = userPeerInfo;
            Debug.Log("HandleRequestSteamUserLogin: " + userId + " " + connectionId + " " + accessToken + " " + userPeerInfo.accessToken);
            DatabaseApiResult updateAccessTokenResp = await DatabaseClient.UpdateAccessTokenAsync(new UpdateAccessTokenReq()
            {
                UserId = userId,
                AccessToken = accessToken
            });
            if (!updateAccessTokenResp.IsSuccess)
            {
                Debug.Log("HandleFirebaseAuthSSOLogin UpdateAccessToken Failed");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR.ToString()),
                });
                return;
            }
            // Response
            result.InvokeSuccess(new ResponseFirebaseAuthSSOLoginMessage()
            {
                userId = userId,
                accessToken = accessToken,
                unbanTime = unbanTime,
                response = "success",
                username = userid
            });
#endif
        }

        protected async UniTaskVoid HandleRequestFirebaseAuthSSORegister(
            RequestHandlerData requestHandler,
            RequestUserRegisterMessage request,
            RequestProceedResultDelegate<ResponseFirebaseAuthSSOLoginMessage> result)
        {
#if UNITY_EDITOR || UNITY_SERVER
            string message = "";
            string email = request.username;
            string password = request.password;
            NameExtensions.overrideUsernameValidating = firebaseAuthSSOCustomNameValidation;
            //string email = request.email;
            Debug.Log("Pre API call");
            //callSteamRegister(email, password, result);
            Debug.Log("Post API call");
#endif
        }

        protected async UniTaskVoid HandleFirebaseAuthSSORegister(string userid,
            RequestProceedResultDelegate<ResponseFirebaseAuthSSOLoginMessage> result,
            RequestHandlerData requestHandler)
        {
#if UNITY_SERVER //|| UNITY_EDITOR
            Debug.Log("HandleFirebaseAuthSSORegister");
            if (disableDefaultLogin)
            {
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_SERVICE_NOT_AVAILABLE,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_SERVICE_NOT_AVAILABLE.ToString()),
                });
                return;
            }
            string username = userid;
            string password = firebaseAuthSSOPass;
            string email = "";
            if (!NameExtensions.IsValidUsername(username))
            {
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INVALID_USERNAME,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INVALID_USERNAME.ToString()),
                });
                return;
            }
            //
            //RequireEmail code deleted
            //
            DatabaseApiResult<FindUsernameResp> findUsernameResp = await DatabaseClient.FindUsernameAsync(new FindUsernameReq()
            {
                Username = username
            });
            if (!findUsernameResp.IsSuccess)
            {
                Debug.Log("HandleFirebaseAuthSSORegister FindUsernameReq Failed");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR.ToString()),
                });
                return;
            }
            if (findUsernameResp.Response.FoundAmount > 0)
            {
                Debug.Log("HandleFirebaseAuthSSORegister Username Exists");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_USERNAME_EXISTED,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_USERNAME_EXISTED.ToString()),
                });
                return;
            }
            //
            //Removed Username and Password length and validation checks
            //
            DatabaseApiResult createResp = await DatabaseClient.CreateUserLoginAsync(new CreateUserLoginReq()
            {
                Username = username,
                Password = password,
                Email = email,
            });
            if (!createResp.IsSuccess)
            {
                Debug.Log("HandleFirebaseAuthSSORegister RegistrationReq Failed");
                result.InvokeError(new ResponseFirebaseAuthSSOLoginMessage()
                {
                    message = UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR,
                    response = LanguageManager.GetText(UITextKeys.UI_ERROR_INTERNAL_SERVER_ERROR.ToString()),
                });
                return;
            }
            // Success registering, lets retry login now
            //result.InvokeSuccess(new ResponseSteamAuthLoginMessage());
            HandleFirebaseAuthSSOLogin(userid, result, requestHandler);
#endif
        }
    }

    public partial class MMOClientInstance : MonoBehaviour
    {
        public void RequestFirebaseAuthSSOLogin(string steamid, string ticket, ResponseDelegate<ResponseFirebaseAuthSSOLoginMessage> callback)
        {
            //centralNetworkManager.RequestSteamLogin(steamid, ticket, callback);
            CentralNetworkManager.RequestFirebaseAuthSSOLogin(steamid, ticket, (responseHandler, responseCode, response) => OnRequestFirebaseAuthSSOLogin(responseHandler, responseCode, response, callback).Forget());
        }
        public void RequestFirebaseAuthSSORegister(string email, string password, ResponseDelegate<ResponseFirebaseAuthSSOLoginMessage> callback)
        {
            centralNetworkManager.RequestFirebaseAuthSSORegister(email, password, callback);
        }

        private async UniTaskVoid OnRequestFirebaseAuthSSOLogin(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseFirebaseAuthSSOLoginMessage response, ResponseDelegate<ResponseFirebaseAuthSSOLoginMessage> callback)
        {
            await UniTask.Yield();
            Debug.Log("OnRequestFirebaseAuthSSOLogin");
            if (callback != null)
                callback.Invoke(responseHandler, responseCode, response);

            GameInstance.UserId = string.Empty;
            GameInstance.UserToken = string.Empty;
            GameInstance.SelectedCharacterId = string.Empty;
            if (responseCode == AckResponseCode.Success)
            {
                GameInstance.UserId = response.userId;
                GameInstance.UserToken = response.accessToken;
            }
        }

        System.Collections.IEnumerator DelayExitGame(float delay)
        {
            yield return new WaitForSeconds(delay); //wait 5 secconds
            Application.Quit();
        }
    }
}

[System.Serializable]
public class ErrorRes
{
    public ErrorDetailsRes error;
}
[System.Serializable]
public class ErrorDetailsRes
{
    public bool error;
    public int code;
    public string message;
}