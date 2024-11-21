using BeamXR.Streaming;
using BeamXR.Streaming.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace BeamXR.Twitch
{
    /// <summary>
    /// A component which fetches the necessary credentials and
    /// uses them to connect to a twitch chat. It also optionally
    /// passes through the OnChatMessage event. You can disable
    /// this and handle subscription yourself.
    /// </summary>
    public class TwitchConnectionHandler : MonoBehaviour
    {
        private string _twitchClientId = "5ianxo6s2up5sr6pbh8gemopu3b8n3";
        private string _portalUrl;
        private const string PLATFORM = "Twitch";
        private const string INTEGRATION_TYPE = "Chat";
        private const string TWITCH_ADDRESS = "irc.chat.twitch.tv";
        private string _channelName;

        EmoteDownloader _emoteDownloader;
        BeamStreamingManager _beamStreamingManager;

        string _twitchToken;

        [SerializeField, Tooltip("Download all global and channel emotes?")] bool _downloadEmotes = true;
        [SerializeField, Tooltip("Automatically subscribe to OnChatMessage event?")] bool _subscribeToChatAutomatically = true;

        public UnityEvent<Chatter> OnMessageReceived;

        public UnityEvent OnTokenNotSet;

        public UnityEvent OnAuthenticated;

        private bool _noTokenWarningSet;
        const int _port = 6667;

        private void Awake()
        {
            _beamStreamingManager = FindObjectOfType<BeamStreamingManager>();

            if (_beamStreamingManager != null)
            {
                switch (_beamStreamingManager.GetEnvironment())
                {
                    case BeamEnvironment.Production:
                        _twitchClientId = "f76ce1kl9puf6n0de5cf1lcjpgftj0";
                        _portalUrl = "https://portal-uat.beamxr.io";
                        break;
                    case BeamEnvironment.Staging:
                    case BeamEnvironment.Development:
                    default:
                        _twitchClientId = "5ianxo6s2up5sr6pbh8gemopu3b8n3";
                        _portalUrl = "https://portal-uat.beamxr.io";
                        break;
                }
            }
        }

        public string GetSettingsUrl()
        {
            return $"{_portalUrl}/settings";
        }

        private IEnumerator Start()
        {
            yield return StartCoroutine(Authenticate());
        }

        IEnumerator Authenticate()
        {
            while (_beamStreamingManager == null || _beamStreamingManager.AuthState != Streaming.AuthenticationState.Authenticated)
            {
                yield return new WaitForSeconds(1);
            }

            while (string.IsNullOrEmpty(_twitchToken))
            {
                var token = _beamStreamingManager.GetTwitchToken();

                yield return token;

                _twitchToken = token.Current as string;

                if (string.IsNullOrEmpty(_twitchToken))
                {
                    if (!_noTokenWarningSet)
                    {
                        _noTokenWarningSet = true;

                        OnTokenNotSet?.Invoke();
                    }

                    yield return new WaitForSeconds(3);
                }
            }

            StartCoroutine(StartIRC());
        }

        IEnumerator StartIRC()
        {
            // Get the channel name using the token.
            var channelName = GetChannelName();

            yield return channelName;

            if (channelName.Current == null)
            {
                Debug.LogError("Failed to get channel name.");
                yield break;
            }

            var IRC = gameObject.AddComponent<IRC>();
            IRC.connectIRCOnStart = false;
            IRC.channel = channelName.Current as string;
            IRC.address = TWITCH_ADDRESS;
            IRC.port = _port;

            IRC.useAnonymousLogin = true;
            // IRC.oauth = _twitchToken;
            // IRC.username = channelName.Current as string;

            if (_downloadEmotes)
            {
                _emoteDownloader = GetComponent<EmoteDownloader>();
                yield return StartCoroutine(GetEmotes());
            }

            ConnectToChat();
        }

        private IEnumerator GetChannelName()
        {
            // URL for the Twitch API to get the user's channel name
            var url = "https://api.twitch.tv/helix/users";

            // Set up the UnityWebRequest with authorization
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Add the Authorization and Client-ID headers
                request.SetRequestHeader("Authorization", $"Bearer {_twitchToken}");
                request.SetRequestHeader("Client-ID", _twitchClientId);

                // Send the request and wait for it to complete
                yield return request.SendWebRequest();

                // Check for errors
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Error fetching channel name: {request.error}");
                    yield break;
                }

                // Parse the JSON response
                var jsonResponse = request.downloadHandler.text;
                var userInfo = JsonUtility.FromJson<GetUserResponse>(jsonResponse);

                if (userInfo.data.Length > 0)
                {
                    var channelName = userInfo.data[0].display_name; // Assuming display_name is the field you want
                    _channelName = channelName;
                    yield return channelName;
                }
                else
                {
                    Debug.LogError("No user data found in Twitch API response.");
                    yield break;
                }
            }
        }

        IEnumerator GetEmotes()
        {
            yield return StartCoroutine(_emoteDownloader.FetchAndDownloadAllEmotes(_twitchToken, _twitchClientId, _channelName));
        }

        private void ConnectToChat()
        {
            IRC.Instance.channel = _channelName;
            if (_subscribeToChatAutomatically)
            {
                IRC.Instance.OnChatMessage += Instance_OnChatMessage;
                IRC.Instance.Connect();

                OnAuthenticated?.Invoke();
            }
        }

        private void Instance_OnChatMessage(Chatter obj)
        {
            OnMessageReceived.Invoke(obj);
        }

        private void OnDestroy()
        {
            IRC.Instance.OnChatMessage -= Instance_OnChatMessage;
        }
    }

}