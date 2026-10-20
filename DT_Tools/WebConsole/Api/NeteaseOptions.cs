using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// 网易云歌单一键导入（随身MP3）：把网易云歌单批量导入本地歌单并开始播放。
    /// 播放链接受网易风控（2026-10 实测），未登录时接口返回 url=null，必须填 Cookie。
    /// </summary>
    /// <remarks>
    /// 获取方式：电脑浏览器登录 music.163.com → F12 → 网络（Network）→ 刷新页面 →
    /// 点任意 music.163.com 请求 → 请求头（Headers）→ 复制 Cookie 整串粘贴到下方。
    /// Cookie 会过期（通常数天~数周），失效后重新登录复制即可。
    /// </remarks>
    [ConfigSection("网易云歌单一键导入（随身MP3）：导入歌单前请先填写下方 Cookie。")]
    internal sealed class NeteaseOptions
    {
        [Config("网易云网页版 Cookie（登录 music.163.com 后 F12 复制整串；留空时只能拉到歌名列表、播放链接被网易风控拦截）。")]
        public static string Cookie = "";
    }
}
