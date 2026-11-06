using System;
using System.Text.RegularExpressions;

namespace LMS.Helpers
{
    public static class VideoHelper
    {
        public static string ToEmbedUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

            rawUrl = rawUrl.Trim();

            if (rawUrl.Contains("youtube.com/embed/") || rawUrl.Contains("player.vimeo.com/video/"))
            {
                return rawUrl;
            }

            var youtuMatch = Regex.Match(rawUrl, @"youtu\.be\/([a-zA-Z0-9_\-]{6,15})", RegexOptions.IgnoreCase);
            if (youtuMatch.Success)
            {
                return "https://www.youtube.com/embed/" + youtuMatch.Groups[1].Value + "?enablejsapi=1&rel=0";
            }

            var ytWatchMatch = Regex.Match(rawUrl, @"(?:youtube\.com\/(?:watch\?.*v=|v\/|embed\/))([a-zA-Z0-9_\-]{6,15})", RegexOptions.IgnoreCase);
            if (ytWatchMatch.Success)
            {
                return "https://www.youtube.com/embed/" + ytWatchMatch.Groups[1].Value + "?enablejsapi=1&rel=0";
            }

            var vimeoMatch = Regex.Match(rawUrl, @"vimeo\.com\/(\d+)", RegexOptions.IgnoreCase);
            if (vimeoMatch.Success)
            {
                return "https://player.vimeo.com/video/" + vimeoMatch.Groups[1].Value;
            }

            return rawUrl;
        }

        public static bool IsDirectVideoFile(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var clean = url.Split('?')[0].ToLowerInvariant();
            return clean.EndsWith(".mp4") || clean.EndsWith(".webm") || clean.EndsWith(".ogg") || clean.EndsWith(".mov");
        }
    }
}
