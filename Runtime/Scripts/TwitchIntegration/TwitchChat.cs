using BeamXR.Streaming.Core;
using BeamXR.Streaming.Core.Models;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace BeamXR.Streaming.Twitch
{
    /// <summary>
    /// Automatically sets up and connects to Twitch Chat using the user's BeamXR connections.
    /// </summary>
    public class TwitchChat : MonoBehaviour
    {
        private const string PLATFORM = "Twitch";
        private const string INTEGRATION_TYPE = "Chat";
        private const string TWITCH_ADDRESS = "irc.chat.twitch.tv";

        private StreamPlatform _platform;

        private TwitchEmoteDownloader _emoteDownloader;
        private BeamManager _beamManager;

        [SerializeField]
        private bool _dontDestroyOnLoad = true;

        [SerializeField, Tooltip("Starts the Twitch chat connection process on start.")]
        private bool _connectOnStart = true;

        [SerializeField, Tooltip("The number of milliseconds between each time the read thread checks for new messages.")]
        private int _readInterval = 200;

        [SerializeField, Tooltip("Download all global and channel emotes, will be significantly slower to start up.")]
        private bool _downloadEmotes = true;

        public UnityEvent<ChatMessage> OnMessageReceived;

        public UnityEvent OnAuthenticated;

        private void Awake()
        {
            _beamManager = FindFirstObjectByType<BeamManager>(FindObjectsInactive.Include);
            if (_dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private IEnumerator Start()
        {
            if (_connectOnStart)
            {
                yield return StartCoroutine(Authenticate());
            }
        }

        private IEnumerator Authenticate()
        {
            while (_beamManager == null || _beamManager.AuthState != AuthenticationState.Authenticated || _beamManager.StreamPlatforms == null)
            {
                if (_beamManager == null)
                {
                    _beamManager = FindFirstObjectByType<BeamManager>(FindObjectsInactive.Include);
                }
                yield return new WaitForSeconds(1);
            }

            _platform = null;

            foreach (var item in _beamManager.StreamPlatforms)
            {
                if (item.PlatformName == "twitch")
                {
                    _platform = item;
                    break;
                }
            }

            if (_platform == null)
            {
                BeamLogger.LogWarning("User does not have Twitch connected to their Beam account. Unable to start chat.", this);
                yield break;
            }

            StartCoroutine(StartIRC());
        }

        private IEnumerator StartIRC()
        {
            if (_platform == null)
            {
                BeamLogger.LogError("User does not have Twitch connected to their Beam account. Unable to start IRC.", this);
                yield break;
            }

            var IRC = gameObject.AddComponent<IRC>();
            IRC.readInterval = _readInterval;
            IRC.Connect(_platform.DisplayName);

            if (_downloadEmotes)
            {
                _emoteDownloader = GetComponent<TwitchEmoteDownloader>();
                yield return StartCoroutine(GetEmotes());
            }

            ConnectToChat();
        }


        private IEnumerator GetEmotes()
        {
            if (_platform == null)
            {
                BeamLogger.LogError("User does not have Twitch connected to their Beam account. Unable to get Emotes.", this);
                yield break;
            }

            var tokenrequest = _beamManager.GetTwitchToken();

            yield return tokenrequest;

            var twitchToken = tokenrequest.Current as string;

            if (string.IsNullOrEmpty(twitchToken))
            {
                BeamLogger.LogError("Unable to fetch token for Twitch.", this);
                yield break;
            }

            var clientrequest = _beamManager.GetClientID("twitch", "chat");

            yield return clientrequest;

            var clientID = clientrequest.Current as string;

            if (string.IsNullOrEmpty(clientID))
            {
                BeamLogger.LogError("Unable to fetch client ID for Twitch.", this);
                yield break;
            }

            yield return StartCoroutine(_emoteDownloader.FetchAndDownloadAllEmotes(twitchToken, clientID, _platform.DisplayName));
        }

        private void ConnectToChat()
        {
            IRC.Instance.OnChatMessage += Instance_OnChatMessage;
            IRC.Instance.Connect(_platform.DisplayName);

            OnAuthenticated?.Invoke();
        }

        private void Instance_OnChatMessage(ChatMessage obj)
        {
            OnMessageReceived.Invoke(obj);
        }

        private void OnDestroy()
        {
            if (IRC.Instance != null)
            {
                IRC.Instance.OnChatMessage -= Instance_OnChatMessage;
            }
        }
    }

}