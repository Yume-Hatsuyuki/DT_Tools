using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// Steam 新闻 RSS 的一条公告（正文已去除 HTML，供挂件列表显示）。
    /// </summary>
    internal sealed class SteamNewsItem
    {
        /// <summary>标题。</summary>
        public string Title = "";

        /// <summary>原文链接（Steam 新闻页）。</summary>
        public string Url = "";

        /// <summary>发布时间（UTC）；解析失败时为 DateTimeOffset.MinValue。</summary>
        public DateTimeOffset Date;

        /// <summary>正文摘要（纯文本，已截断）。</summary>
        public string Summary = "";

        /// <summary>配图地址：正文中的第一张图；正文没有图时取 enclosure；都没有则为 null。</summary>
        public string Image;
    }

    /// <summary>
    /// Steam 新闻 RSS 解析。纯函数，不依赖 HTTP 与游戏对象，便于独立测试。
    /// </summary>
    internal static class SteamNewsParser
    {
        /// <summary>单次最多返回的条数。</summary>
        public const int MaxItems = 12;

        private const int SummaryMax = 160;
        private const string PubDateFormat = "ddd, dd MMM yyyy HH:mm:ss zzz";

        private static readonly Regex TagRe = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex SpaceRe = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ImgRe = new Regex("<img[^>]*?src=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 解析 RSS 文本。XML 非法时抛出 System.Xml.XmlException，由调用方处理。
        /// </summary>
        /// <param name="xml">Steam 新闻 RSS 的原文。</param>
        /// <returns>至多 MaxItems 条公告，保持 RSS 中的顺序（新的在前）。</returns>
        public static List<SteamNewsItem> Parse(string xml)
        {
            var doc = XDocument.Parse(xml);
            var list = new List<SteamNewsItem>();
            foreach (var item in doc.Descendants("item").Take(MaxItems))
            {
                // XDocument 已解开一层 XML 实体，description 是 Steam 写入的 HTML 片段
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

        /// <summary>
        /// 把正文 HTML 压成一行纯文本并截断。
        /// </summary>
        /// <param name="html">正文 HTML 片段。</param>
        /// <returns>纯文本摘要，超过上限时以省略号结尾。</returns>
        public static string Summarize(string html)
        {
            string text = WebUtility.HtmlDecode(TagRe.Replace(html ?? "", " "));
            text = SpaceRe.Replace(text, " ").Trim();
            if (text.Length > SummaryMax)
                text = text.Substring(0, SummaryMax) + "…";
            return text;
        }

        /// <summary>
        /// 取正文中的第一张图片地址。
        /// </summary>
        /// <param name="html">正文 HTML 片段。</param>
        /// <returns>图片地址；没有图片时为 null。</returns>
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
