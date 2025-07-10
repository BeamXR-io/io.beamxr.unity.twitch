using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BeamXR.Streaming.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace BeamXR.Streaming.Twitch
{
    /// <summary>
    /// This component can fetch the listings of twitch emotes
    /// both globally and for a specific channel, then download
    /// them and insert them into a TMP-compatible spritesheet
    /// for use in TextMeshPro objects.
    /// </summary>
    public class TwitchEmoteDownloader : MonoBehaviour
    {
        private Texture2D _sheetTexture;
        private Sprite _spriteSheet;
        [SerializeField] Material _spriteMat;

        public TMP_SpriteAsset EmoteSpriteSheet => _tmpSpriteAsset;
        [SerializeField] TMP_SpriteAsset _tmpSpriteAsset;

        public Dictionary<string, string> EmoteUrls => _emoteUrls;
        Dictionary<string, string> _emoteUrls;

        public bool EmotesAvailable { get; private set; }

        string _userEndpoint = "https://api.twitch.tv/helix/users";
        string _emoteEndpointGlobal = "https://api.twitch.tv/helix/chat/emotes/global";
        string _emoteEndpointChannel = "https://api.twitch.tv/helix/chat/emotes";

        public IEnumerator FetchAndDownloadAllEmotes(string accessToken, string clientID, string channelName = null)
        {
            EmotesAvailable = false;
            yield return StartCoroutine(FetchEmoteData(accessToken, clientID, channelName));
            if(_emoteUrls != null)
            {
				yield return StartCoroutine(DownloadAllEmotes(_emoteUrls));
			}
        }

        /// <summary>
        /// Query the global emote list (and optionally, a channel's emote list)
        /// and assemble a dictionary of IDs and download URLs which we can use
        /// to download the associated images.
        /// </summary>
        IEnumerator FetchEmoteData(string accessToken, string clientID, string channelName = null)
        {
            UnityWebRequest webRequest = GetRequest(_emoteEndpointGlobal, accessToken, clientID);

            yield return new WaitUntil(() => webRequest.isDone);

            if (webRequest.responseCode != 200)
            {
                BeamLogger.LogError($"Error {webRequest.responseCode} when getting global emote data");
                yield break;
            }

            string json = webRequest.downloadHandler.text;

            GetEmoteListResponse response = JsonUtility.FromJson<GetEmoteListResponse>(json);
            if (response == null)
            {
                BeamLogger.LogError($"Unexpected response from global emote list GET request: {json}");
                yield break;
            }

            _emoteUrls = new Dictionary<string, string>();

            AddEmoteUrlsToDictionary(response);

            if (channelName != null)
            {
                bool gotUser = false;
                GetUserResponse userResponse = null;
                string broadcasterId = null;
                yield return GetUserData(channelName, accessToken, clientID, userJson =>
                {
                    gotUser = true;
                    userResponse = JsonUtility.FromJson<GetUserResponse>(userJson);
                    if (userResponse == null)
                    {
                        BeamLogger.LogError($"Unexpected response from channel emote list GET request: {json}");
                    }
                    else
                    {
                        broadcasterId = userResponse.data[0].id;
                    }
                });

                if (!gotUser)
                {
                    yield break;
                }

                yield return GetChannelEmoteList(channelName, accessToken, clientID, broadcasterId, emoteJson =>
                {
                    response = JsonUtility.FromJson<GetEmoteListResponse>(emoteJson);
                    if (response == null)
                    {
                        BeamLogger.LogError($"Unexpected response from channel emote list GET request: {json}");
                    }
                    else
                    {
                        AddEmoteUrlsToDictionary(response);
                    }
                });
            }

        }

        IEnumerator GetUserData(string channelName, string accessToken, string clientID, Action<string> callback)
        {
            UnityWebRequest webRequest = GetRequest($"{_userEndpoint}?login={channelName}", accessToken, clientID);

            yield return new WaitUntil(() => webRequest.isDone);
            if (webRequest.responseCode != 200)
            {
                BeamLogger.LogError($"Error {webRequest.responseCode} when fetching user data for {channelName}.");
                yield break;
            }

            callback.Invoke(webRequest.downloadHandler.text);
        }

        IEnumerator GetChannelEmoteList(string channelName, string accessToken, string clientID, string broadcasterId, Action<string> callback)
        {
            UnityWebRequest webRequest = GetRequest($"{_emoteEndpointChannel}?broadcaster_id={broadcasterId}", accessToken, clientID);

            yield return new WaitUntil(() => webRequest.isDone);

            if (webRequest.responseCode != 200)
            {
                BeamLogger.LogError($"Error {webRequest.responseCode} when fetching emote list for channel {channelName}.");
                yield break;
            }

            callback.Invoke(webRequest.downloadHandler.text);
        }

        private UnityWebRequest GetRequest(string url, string accessToken, string clientID)
        {
            UnityWebRequest webRequest = new UnityWebRequest(url, "GET");
            webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            webRequest.SetRequestHeader("Client-Id", clientID);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SendWebRequest();
            return webRequest;
        }

        void AddEmoteUrlsToDictionary(GetEmoteListResponse response)
        {
            foreach (var emote in response.data)
            {
                string key = $"{emote.id}@{emote.name}";
                string emoteUrl = FormatEmoteUrl(emote, response.template);
                _emoteUrls.Add(key, emoteUrl);
            }
        }

        private string FormatEmoteUrl(TwitchEmoteData emote, string template)
        {
            // The URLS follow this template: $"https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}";
            string emoteUrl = template;
            emoteUrl = emoteUrl.Replace("{{id}}", emote.id);
            emoteUrl = emoteUrl.Replace("{{format}}", emote.format[0]);
            emoteUrl = emoteUrl.Replace("{{theme_mode}}", emote.theme_mode[0]);
            emoteUrl = emoteUrl.Replace("{{scale}}", emote.scale[0]);
            return emoteUrl;
        }

        /// <summary>
        /// Download each of the global emotes and store them in a spritesheet.
        /// </summary>
        public IEnumerator DownloadAllEmotes(Dictionary<string, string> emoteUrls)
        {
            // This is quite slow - it takes a couple seconds to get all the emotes and
            // emotes won't get parsed until it's finished. The bottleneck is in the
            // twitch API requirement that you download each icon individually. Ideally
            // we would fetch them per-message as needed, but TextMeshPro requires that
            // you submit a full spritesheet for use with inline images, so this is much
            // less work (and maybe less GC allocation) than rebuilding and re-submitting
            // it each time a new emote is requested.

            int smallestEmoteSize = 28;
            int emotesPerRow = Mathf.CeilToInt(Mathf.Sqrt(emoteUrls.Count));
            int texWidth = Mathf.NextPowerOfTwo(emotesPerRow * smallestEmoteSize);
            _sheetTexture = new Texture2D(texWidth, texWidth, TextureFormat.RGBA32, false, false);
            _sheetTexture.name = "EmoteSheetTexture";
            _sheetTexture.filterMode = FilterMode.Point;

            int i = 0;

            foreach (var emote in emoteUrls)
            {
                // Create a handler to handle the response.
                UnityWebRequest webRequest = new UnityWebRequest(emote.Value, "GET");
                webRequest.downloadHandler = new DownloadHandlerTexture(true);
                webRequest.SendWebRequest();
                while (!webRequest.isDone)
                {
                    yield return null;
                }
                if (webRequest.responseCode != 200)
                {
                    BeamLogger.LogError($"Couldn't download image from {emote.Value}");
                    continue;
                }
                Texture2D tex = new Texture2D(smallestEmoteSize, smallestEmoteSize);
                tex.LoadImage(webRequest.downloadHandler.data);
                int bufferX = smallestEmoteSize - tex.width;
                int bufferY = smallestEmoteSize - tex.height;
                int x = (i % emotesPerRow) * smallestEmoteSize;
                int y = (i / emotesPerRow) * smallestEmoteSize;

                try
                {
                    var pixels = tex.GetPixels32();
                    if (pixels.Length < smallestEmoteSize * smallestEmoteSize)
                    {
                        int width = tex.width;
                        int height = tex.height;
                        var newPixels = new Color32[width * height];
                        for (int p = 0; p < pixels.Length; p++)
                        {
                            newPixels[p] = pixels[p];
                        }
                        pixels = newPixels;
                        _sheetTexture.SetPixels32(x, y, width, height, pixels);
                    }
                    else if (pixels.Length > smallestEmoteSize * smallestEmoteSize)
                    {
                        int width = smallestEmoteSize;
                        int height = smallestEmoteSize;
                        var newPixels = new Color32[width * height];
                        for (int p = 0; p < newPixels.Length; p++)
                        {
                            newPixels[p] = pixels[p];
                        }
                        pixels = newPixels;
                        _sheetTexture.SetPixels32(x, y, width, height, pixels);
                    }
                    else
                    {
                        _sheetTexture.SetPixels32(x, y, smallestEmoteSize, smallestEmoteSize, pixels);
                    }
                }
                catch (ArgumentException e)
                {
                    BeamLogger.LogError($"Sprite size problem at {emote.Key} {e.Message}");
                }
                _sheetTexture.Apply();
                i++;
            }
            _sheetTexture.Apply();
            StartCoroutine(InsertEmotesIntoTMP(emoteUrls, emotesPerRow));
        }

        private IEnumerator InsertEmotesIntoTMP(Dictionary<string, string> emoteUrls, int emotesPerRow)
        {
            _spriteSheet = Sprite.Create(_sheetTexture, new Rect(0f, 0f, _sheetTexture.width, _sheetTexture.height), Vector2.zero);
            _spriteSheet.name = "TwitchEmoteSpriteSheet";
            int spriteSize = 28;
            var sprites = new List<TMP_Sprite>();
            var emoteIds = emoteUrls.Keys.ToArray();

            _tmpSpriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            _tmpSpriteAsset.name = "TwitchEmoteSprites";
            int index = 0;
            for (int y = 0; y < emotesPerRow; y++)
            {
                for (int x = 0; x < emotesPerRow; x++)
                {
                    TMP_Sprite tmpSprite = new TMP_Sprite();
                    tmpSprite.x = x * spriteSize;
                    tmpSprite.y = y * spriteSize;
                    tmpSprite.id = index;
                    string idString = emoteIds[index];
                    string actualName = idString.Substring(idString.LastIndexOf("@") + 1);
                    tmpSprite.name = actualName;
                    tmpSprite.width = spriteSize;
                    tmpSprite.height = spriteSize;
                    tmpSprite.pivot = new Vector2(0, 0);
                    tmpSprite.unicode = (128516 + index); //Random number that's vaguely in the right ballpark so that TMP doesn't complain.
                    tmpSprite.yOffset = +28;
                    tmpSprite.xAdvance = 28;
                    tmpSprite.scale = 1;
                    tmpSprite.hashCode = tmpSprite.GetHashCode();

                    tmpSprite.sprite = _spriteSheet;
                    sprites.Add(tmpSprite);
                    index++;

                    if (index >= emoteIds.Length)
                    {
                        // We got all the emotes.
                        break;
                    }
                }

                if (index >= emoteIds.Length)
                {
                    // We got all the emotes.
                    break;
                }
            }
            yield return null;

            _tmpSpriteAsset.spriteSheet = _sheetTexture as Texture;
            _tmpSpriteAsset.spriteInfoList = sprites;

            var matInstance = Instantiate(_spriteMat);
            matInstance.mainTexture = _sheetTexture;
            matInstance.SetTexture("_MainTex", _sheetTexture);
            _tmpSpriteAsset.material = matInstance;
            _tmpSpriteAsset.UpdateLookupTables();

            for (uint i = 0; i < _tmpSpriteAsset.spriteCharacterTable.Count; i++)
            {
                var spriteCharacter = _tmpSpriteAsset.spriteCharacterTable[(int)i];
                spriteCharacter.glyphIndex = i;
                var glyph = _tmpSpriteAsset.spriteGlyphTable[(int)i];
                glyph.atlasIndex = (int)i;
            }

            RefreshSpriteAsset();
            EmotesAvailable = true;
        }

        /// <summary>
        /// Force TMP to update its understanding of what sprites to use
        /// and how to use them.
        /// </summary>
        private void RefreshSpriteAsset()
        {
            _tmpSpriteAsset.UpdateLookupTables();
            _tmpSpriteAsset.SortGlyphTable();
            TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, _tmpSpriteAsset);
        }
    }
}