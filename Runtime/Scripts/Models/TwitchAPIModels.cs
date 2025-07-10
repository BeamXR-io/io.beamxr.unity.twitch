using System;

namespace BeamXR.Streaming.Twitch
{
    [Serializable]
    public class GetUserResponse
    {
        public TwitchUserData[] data;
    }

    [Serializable]
    public class TwitchUserData
    {
        public string id;
        public string login;
        public string display_name;
        public string type;
        public string broadcaster_type;
        public string description;
        public string profile_image_url;
        public string offline_image_url;
        public int view_count;
        public string created_at;
    }

    [Serializable]
    public class GetEmoteListResponse
    {
        public TwitchEmoteData[] data;
        public string template;
    }

    [Serializable]
    public class GetChannelEmotesResponse
    {
        public TwitchEmoteData[] data;
        public string template;
    }

    [Serializable]
    public class TwitchEmoteData
    {
        public string id;
        public string name;
        public TwitchEmoteImageUrls images;
        public string[] format;
        public string[] scale;
        public string[] theme_mode;

        // Channel-only
        public string tier;
        public string emote_type;
        public string emote_set_id;
    }

    [Serializable]
    public class TwitchEmoteImageUrls
    {
        public string url_1x;
        public string url_2x;
        public string url_4x;
    }
}