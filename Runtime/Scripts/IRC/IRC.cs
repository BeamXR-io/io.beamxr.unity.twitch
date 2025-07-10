using System;
using System.Collections;
using UnityEngine;
using System.Collections.Concurrent;
using BeamXR.Streaming.Core;

namespace BeamXR.Streaming.Twitch
{
    public partial class IRC : MonoBehaviour
    {
        public const string TWITCH_ADDRESS = "irc.chat.twitch.tv";
        public const int TWITCH_PORT = 6667;

        private bool _setup = false;

        private string _channel = "";
        public string Channel => _channel;

        public bool dontDestroyOnLoad = true;

        public int readInterval = 150;
        public ReadBufferSize readBufferSize = ReadBufferSize._256;
        public int writeInterval = 50;


        // If the game is paused for a significant amount of time and then unpaused,
        // there could be a lot of data to handle, which could cause lag spikes.
        // To prevent this, we limit the amount of data handled per frame.
        private static readonly int maxDataPerFrame = 100;
        private int connectionFailCount = 0;
        private TwitchConnection connection;
        public static IRC Instance { get; private set; }

        #region Events

        public event Action<ChatMessage> OnChatMessage;
        public event Action<IRCReply> OnConnectionAlert;

        #endregion

        // Queues
        internal readonly ConcurrentQueue<IRCReply> alertQueue = new ConcurrentQueue<IRCReply>();
        internal readonly ConcurrentQueue<ChatMessage> chatterQueue = new ConcurrentQueue<ChatMessage>();

        public IRCTags ClientUserTags => connection?.ClientUserTags;

        #region Unity methods

        private void Awake()
        {
            Setup();
        }

        private void Setup()
        {
            if (_setup)
                return;

            if (Instance)
            {
                if (dontDestroyOnLoad)
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
            }
            else
            {
                Instance = this;
                _setup = true;

                if (dontDestroyOnLoad)
                    DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            HandlePendingInformation();
        }

        private void OnDestroy()
        {
            if (dontDestroyOnLoad && Instance == this)
                Instance = null;

            BlockingDisconnect();
        }

        private void OnDisable()
        {
            BlockingDisconnect();
        }

        #endregion

        private void HandlePendingInformation()
        {
            int dataHandledThisFrame = 0;

            // Handle pending connection alerts
            while (!alertQueue.IsEmpty)
            {
                if (dataHandledThisFrame >= maxDataPerFrame)
                    break;

                if (alertQueue.TryDequeue(out var alert))
                {
                    HandleConnectionAlert(alert);
                    dataHandledThisFrame++;
                }
            }

            // Handle pending chat messages
            while (!chatterQueue.IsEmpty)
            {
                if (dataHandledThisFrame >= maxDataPerFrame)
                    break;

                if (chatterQueue.TryDequeue(out var chatter))
                {
                    OnChatMessage?.Invoke(chatter);
                    dataHandledThisFrame++;
                }
            }
        }

        private void HandleConnectionAlert(IRCReply alert)
        {
            BeamLogger.LogDev($"{IRCMessageTag.Alert()} {alert.GetDescription()}");

            switch (alert)
            {
                case IRCReply.NO_CONNECTION:
                case IRCReply.BAD_LOGIN:
                case IRCReply.MISSING_LOGIN_INFO:
                    connectionFailCount = 0;
                    Disconnect();
                    break;

                case IRCReply.CONNECTED_TO_SERVER:
                    break;

                case IRCReply.CONNECTION_INTERRUPTED:
                    // Increment fail count and try reconnecting
                    connectionFailCount++;
                    Connect(_channel);
                    break;

                case IRCReply.JOINED_CHANNEL:
                    connectionFailCount = 0;
                    break;
            }

            OnConnectionAlert?.Invoke(alert);
        }

        public void Connect(string channel)
        {
            _channel = channel;
            Setup();
            StartCoroutine(StartConnection());
            IEnumerator StartConnection()
            {
                if (connection != null) // End current connection if it exists
                    yield return StartCoroutine(NonBlockingDisconnect());

                connection = new TwitchConnection(this);

                if (connection.tcpClient == null || !connection.tcpClient.Connected)
                {
                    alertQueue.Enqueue(IRCReply.NO_CONNECTION);
                    yield break;
                }

                // Reconnect interval based on failed attempt count
                if (connectionFailCount >= 2)
                {
                    int delay = 1 << (connectionFailCount - 2); // -> 0s, 1s, 2s, 4s, 8s, 16s, ...

                    BeamLogger.LogDev($"{IRCMessageTag.Alert()} Reconnecting in {delay} seconds");

                    yield return new WaitForSecondsRealtime(delay);
                }

                // Start connection and threads
                connection.Begin();
            }
        }

        public void Disconnect()
        {
            if (connection == null || connection.disconnectCalled)
                return;

            StartCoroutine(NonBlockingDisconnect());
        }

        private IEnumerator NonBlockingDisconnect()
        {
            yield return StartCoroutine(connection.End());

            // Reset connection variable
            connection = null;

            BeamLogger.LogDev($"{IRCMessageTag.Alert()} Disconnected from Twitch IRC");
        }

        private void BlockingDisconnect()
        {
            if (connection == null)
                return;

            connection.BlockingEnd();

            // Reset connection variable
            connection = null;

            BeamLogger.LogDev($"{IRCMessageTag.Alert()} Disconnected from Twitch IRC");
        }

        /// <summary>
        /// Join a new channel
        /// </summary>
        /// <param name="channel">The channel to join</param>
        public void JoinChannel(string channel)
        {
            if (channel == "")
            {
                BeamLogger.LogError("Failed joining channel. Channel name is empty");
                return;
            }

            connection.SendCommand("JOIN #" + channel.ToLower(), true);
        }

        /// <summary>
        /// Leaves a channel
        /// </summary>
        /// <param name="channel">The channel to leave</param>
        public void LeaveChannel(string channel)
        {
            if (channel == "")
            {
                BeamLogger.LogError("Failed leaving channel. Channel name is empty");
                return;
            }

            connection.SendCommand("PART #" + channel.ToLower(), true);
        }
    }
}