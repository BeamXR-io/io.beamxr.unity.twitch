using System.Collections.Generic;
using UnityEngine;
using System;
using BeamXR.Streaming.Core;

namespace BeamXR.Streaming.Twitch
{
    /// <summary>
    /// This is a simple example chat window script, showing one
    /// way to display a live Twitch chat with emote support.
    /// </summary>
    public class TwitchChatWindow : MonoBehaviour
    {
        private TwitchChat _twitchChat;

        [SerializeField]
        private Transform _messageList;
        [SerializeField]
        private MessageDisplay _messagePrefab;
        [SerializeField, Tooltip("If left empty, no sounds will play on message received")]
        private AudioSource _newMessageAudio;

        List<MessageDisplay> _messages;

        private const string _colorHex = "#68a1f7";

        [SerializeField, Tooltip("The number of messages to display before they are culled.")]
        private int _messageLimit = 50;
        private int _messageCount = 0;

        TwitchEmoteDownloader _emoteDownloader;

        private void Start()
        {
            _twitchChat = FindFirstObjectByType<TwitchChat>(FindObjectsInactive.Include);

            if(_twitchChat == null)
            {
                BeamLogger.LogError("Unable to find the Beam Twitch Chat object. Have you added it to your scene?", this);
                return;
            }

            _twitchChat.OnAuthenticated.AddListener(HandleAuthenticated);
            _twitchChat.OnMessageReceived.AddListener(HandleChatMessage);

            _messages = new List<MessageDisplay>();
            _emoteDownloader = FindFirstObjectByType<TwitchEmoteDownloader>(FindObjectsInactive.Include);
        }

        public void HandleChatMessage(ChatMessage chatter)
        {
            AddNewMessage(chatter);
            if (_messageCount >= _messageLimit)
            {
                RemoveOldestMessage();
            }
        }

        public void HandleAuthenticated()
        {
            AddNewMessage(new ChatMessage("BeamXR", "BeamXR", $"Connected to chat", new IRCTags { displayName = "BeamXR", colorHex = _colorHex }));
        }

        private void AddNewMessage(ChatMessage chatter)
        {
            MessageDisplay result = Instantiate(_messagePrefab, _messageList, false);
            string timeStamp = DateTime.Now.ToShortTimeString();
            result.transform.SetAsLastSibling();
            if (_emoteDownloader != null && _emoteDownloader.EmotesAvailable)
            {
                result.Text.spriteAsset = _emoteDownloader.EmoteSpriteSheet;
                chatter.message = InsertEmotes(chatter.message);
            }
            result.SetMessage(timeStamp, chatter.tags.displayName, chatter.message, chatter.GetNameColor());

            _messages.Add(result);
            _messageCount++;
            _newMessageAudio?.Play();
        }

        /// <summary>
        /// Use the existing store of emotes to convert every known
        /// label in the string into a TMP-compatible sprite tag.
        /// </summary>
        public string InsertEmotes(string message)
        {
            foreach (var emoteId in _emoteDownloader.EmoteUrls.Keys)
            {
                string target = emoteId.Substring(emoteId.LastIndexOf("@") + 1);
                if (message.Contains(target))
                {
                    string spriteCode = $"<sprite name={target}> ";
                    message = message.Replace(target, spriteCode);
                }
            }
            return message;
        }

        private void RemoveOldestMessage()
        {
            var target = _messageList.GetChild(0);
            if (target == null)
            {
                return;
            }

            Destroy(target.gameObject);
            _messageCount--;
        }
    }
}