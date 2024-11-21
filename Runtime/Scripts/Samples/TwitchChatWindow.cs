using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Networking;
using BeamXR.Streaming.Core;
using BeamXR.Streaming.Core.Auth.Credentials;

namespace BeamXR.Twitch
{
    /// <summary>
    /// This is a simple example chat window script, showing one
    /// way to display a live Twitch chat with emote support.
    /// </summary>
    public class TwitchChatWindow : MonoBehaviour
    {
        [SerializeField] Transform _messageList;
        [SerializeField] MessageDisplay _messagePrefab;
        [SerializeField] AudioSource _newMessageAudio;

        List<MessageDisplay> _messages;

        const int _messageLimit = 100;
        int _messageCount = 0;

        EmoteDownloader _emoteDownloader;

        private void Start()
        {
            _messages = new List<MessageDisplay>();
            _emoteDownloader = FindObjectOfType<EmoteDownloader>();
        }

        public void HandleChatMessage(Chatter chatter)
        {
            AddNewMessage(chatter);
            if (_messageCount >= _messageLimit)
            {
                RemoveOldestMessage();
            }
        }

        public void HandleAuthenticated()
        {
            AddNewMessage(new Chatter("BeamXR", "BeamXR", $"You are now connected to your chat", new IRCTags { displayName = "BeamXR" }));
        }

        public void HandleNoTokenAvailable()
        {
            var connectionHandler = FindObjectOfType<TwitchConnectionHandler>();

            if (connectionHandler != null)
            {
                var url = connectionHandler.GetSettingsUrl();
                AddNewMessage(new Chatter("BeamXR", "BeamXR", $"Please visit the portal and add your Twitch integration at {url}", new IRCTags { displayName = "BeamXR" }));
            }
            else
            {
                AddNewMessage(new Chatter("BeamXR", "BeamXR", "Please visit the portal and add your Twitch integration", new IRCTags { displayName = "BeamXR" }));
            }
        }

        private void AddNewMessage(Chatter chatter)
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
            _newMessageAudio.Play();
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