using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DT_Tools.WebConsole.Api
{
    internal sealed class SteamNewsItem
    {
        public string Title = "";

        public string Url = "";

        public DateTimeOffset Date;

        public string Summary = "";

        public string Image;
    }

    internal static class SteamNewsParser
    {
        public const int MaxItems = 12;

        private const int SummaryMax = 160;
        private const string PubDateFormat = "ddd, dd MMM yyyy HH:mm:ss zzz";

        private static readonly Regex TagRe = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex SpaceRe = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ImgRe = new Regex("<img[^>]*?src=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<SteamNewsItem> Parse(string xml)
        {
            var doc = XDocument.Parse(xml);
            var list = new List<SteamNewsItem>();
            foreach (var item in doc.Descendants("item").Take(MaxItems))
            {
                string html = (string)item.Element("description") ?? "";
                list.Add(new SteamNewsItem
                {
                    Title = (string)item.Element("title") ?? "",
                    Url = (string)item.Element("link") ?? (string)item.Element("guid") ?? "",
                    Date = ParseDate((string)item.Element("pubDate")),
                    Summary = Summarize(html),
                    Image = FirstImage(html) ?? (string)item.Element("enclosure")?.Attribute("url"),
                });
            }
            return list;
        }

        public static string Summarize(string html)
        {
            string text = WebUtility.HtmlDecode(TagRe.Replace(html ?? "", " "));
            text = SpaceRe.Replace(text, " ").Trim();
            if (text.Length > SummaryMax)
                text = text.Substring(0, SummaryMax) + "…";
            return text;
        }

        public static string FirstImage(string html)
        {
            var m = ImgRe.Match(WebUtility.HtmlDecode(html ?? ""));
            return m.Success ? m.Groups[1].Value : null;
        }

        private static DateTimeOffset ParseDate(string s)
        {
            DateTimeOffset d;
            if (DateTimeOffset.TryParseExact(s ?? "", PubDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d;
            if (DateTimeOffset.TryParse(s ?? "", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
                return d;
            return DateTimeOffset.MinValue;
        }
    }
}
