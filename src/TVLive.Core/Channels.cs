namespace TVLive.Core;
public record Channel(string Id, string Name, string Group, string Page, string? Host = null, string? Path = null, string? Selection = null)
{
    public bool Accepts(string address) => Uri.TryCreate(address, UriKind.Absolute, out var u)
        && u.Scheme == "https" && Host != null && (u.Host == Host || (Host.StartsWith('.') && u.Host.EndsWith(Host, StringComparison.OrdinalIgnoreCase)))
        && u.AbsolutePath.Contains(Path!, StringComparison.OrdinalIgnoreCase) && u.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
}
public static class Catalog
{
    // Official page links verified against Yangshipin channel selection on 2026-10-24.
    public static readonly Channel[] All = [
        new("cctv1", "CCTV-1 综合", "央视", "https://www.yangshipin.cn/tv/home?pid=600001859"),
        new("cctv2", "CCTV-2 财经", "央视", "https://www.yangshipin.cn/tv/home?pid=600001800"),
        new("cctv3", "CCTV-3 综艺", "央视", "https://www.yangshipin.cn/tv/home?pid=600001801"),
        new("cctv4", "CCTV-4 中文国际", "央视", "https://www.yangshipin.cn/tv/home?pid=600001814"),
        new("cctv5", "CCTV-5 体育", "央视", "https://www.yangshipin.cn/tv/home?pid=600001818"),
        new("cctv5plus", "CCTV-5+ 体育赛事", "央视", "https://www.yangshipin.cn/tv/home?pid=600001817"),
        new("cctv6", "CCTV-6 电影", "央视", "https://www.yangshipin.cn/tv/home?pid=600108442"),
        new("cctv7", "CCTV-7 国防军事", "央视", "https://www.yangshipin.cn/tv/home?pid=600004092"),
        new("cctv8", "CCTV-8 电视剧", "央视", "https://www.yangshipin.cn/tv/home?pid=600001803"),
        new("cctv9", "CCTV-9 纪录", "央视", "https://www.yangshipin.cn/tv/home?pid=600004078"),
        new("cctv10", "CCTV-10 科教", "央视", "https://www.yangshipin.cn/tv/home?pid=600001805"),
        new("cctv11", "CCTV-11 戏曲", "央视", "https://www.yangshipin.cn/tv/home?pid=600001806"),
        new("cctv12", "CCTV-12 社会与法", "央视", "https://www.yangshipin.cn/tv/home?pid=600001807"),
        new("cctv13", "CCTV-13 新闻", "央视", "https://www.yangshipin.cn/tv/home?pid=600001811"),
        new("cctv14", "CCTV-14 少儿", "央视", "https://www.yangshipin.cn/tv/home?pid=600001809"),
        new("cctv15", "CCTV-15 音乐", "央视", "https://www.yangshipin.cn/tv/home?pid=600001815"),
        new("cctv16", "CCTV-16 奥林匹克", "央视", "https://www.yangshipin.cn/tv/home?pid=600098637"),
        new("cctv17", "CCTV-17 农业农村", "央视", "https://www.yangshipin.cn/tv/home?pid=600001810"),
        new("cgtn", "CGTN 英语", "国际", "https://www.yangshipin.cn/tv/home?pid=600014550"),
        new("zhejiang", "浙江卫视", "卫视", "https://www.cztv.com/liveTV", ".cztv.com", "/live/channel01"),
        new("jiangsu", "江苏卫视", "卫视", "https://live.jstv.com/?id=9", "litchi-play-encrypted-site.jstv.com", "/applive/jswspro"),
        new("dongfang", "东方卫视", "卫视", "https://live.kankanews.com/huikan", "volc-stream.kksmg.com", "/live/dfws4k/index"),
        new("beijing", "北京卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002309"),
        new("hunan", "湖南卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002475"),
        new("hubei", "湖北卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002508"),
        new("guangdong", "广东卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002485"),
        new("guangxi", "广西卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002509"),
        new("heilongjiang", "黑龙江卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002498"),
        new("hainan", "海南卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002506"),
        new("chongqing", "重庆卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002531"),
        new("shenzhen", "深圳卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002481"),
        new("sichuan", "四川卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002516"),
        new("henan", "河南卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002525"),
        new("fujian", "福建东南卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002484"),
        new("guizhou", "贵州卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002490"),
        new("jiangxi", "江西卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002503"),
        new("liaoning", "辽宁卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002505"),
        new("anhui", "安徽卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002532"),
        new("hebei", "河北卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002493"),
        new("shandong", "山东卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600002513"),
        new("tianjin", "天津卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600152137"),
        new("jilin", "吉林卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190405"),
        new("shaanxi", "陕西卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190400"),
        new("gansu", "甘肃卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190408"),
        new("ningxia", "宁夏卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190737"),
        new("neimenggu", "内蒙古卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190401"),
        new("yunnan", "云南卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190402"),
        new("shanxi", "山西卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190407"),
        new("qinghai", "青海卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190406"),
        new("xizang", "西藏卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600190403"),
        new("xinjiang", "新疆卫视", "卫视", "https://www.yangshipin.cn/tv/home?pid=600152138"),
        new("cetv1", "CETV-1 中国教育", "教育", "https://www.yangshipin.cn/tv/home?pid=600171827"),
        new("zj_qianjiang", "钱江都市", "地方 · 浙江", "https://www.cztv.com/liveTV/102"),
        new("zj_jingji", "浙江经济生活", "地方 · 浙江", "https://www.cztv.com/liveTV/103"),
        new("zj_jiaoke", "浙江教科影视", "地方 · 浙江", "https://www.cztv.com/liveTV/104"),
        new("zj_minsheng", "浙江民生休闲", "地方 · 浙江", "https://www.cztv.com/liveTV/106"),
        new("zj_xinwen", "浙江新闻", "地方 · 浙江", "https://www.cztv.com/liveTV/107"),
        new("zj_shaoer", "浙江少儿", "地方 · 浙江", "https://www.cztv.com/liveTV/108"),
        new("zj_guoji", "浙江国际", "地方 · 浙江", "https://www.cztv.com/liveTV/110"),
        new("zj_jilu", "之江纪录", "地方 · 浙江", "https://www.cztv.com/liveTV/112"),
        new("sh_xinwen", "上海新闻综合", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "新闻综合"),
        new("sh_moduyan", "魔都眼", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "魔都眼"),
        new("sh_caijing", "第一财经", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "第一财经"),
        new("sh_jishi", "新纪实", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "新纪实"),
        new("sh_tiyu", "五星体育", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "五星体育"),
        new("sh_dushi", "上海都市频道", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "都市频道"),
        new("sh_haha", "哈哈炫动", "地方 · 上海", "https://live.kankanews.com/huikan/", Selection: "哈哈炫动"),
        new("gd_zhujiang", "广东珠江", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/44"),
        new("gd_xinwen", "广东新闻", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/45"),
        new("gd_minsheng", "广东民生", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/48"),
        new("gd_tiyu", "广东体育", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/47"),
        new("gd_dawanqu", "大湾区卫视", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/51"),
        new("gd_dawanqu_overseas", "大湾区卫视（海外版）", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/46"),
        new("gd_yingshi", "广东影视", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/53"),
        new("gd_shaoer", "广东少儿", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/54"),
        new("gd_jiajia", "嘉佳卡通", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/66"),
        new("gd_xiqu", "岭南戏曲", "地方 · 广东", "https://www.gdtv.cn/tvChannelDetail/15"),
        new("hb_jingshi", "湖北经视", "地方 · 湖北", "https://news.hbtv.com.cn/app/tv/432"),
        new("hb_zonghe", "湖北综合", "地方 · 湖北", "https://news.hbtv.com.cn/app/tv/433"),
        new("hb_yingshi", "湖北影视", "地方 · 湖北", "https://news.hbtv.com.cn/app/tv/435"),
        new("hb_jiaoyu", "湖北教育", "地方 · 湖北", "https://news.hbtv.com.cn/app/tv/437"),
        new("hb_longshang", "湖北垄上", "地方 · 湖北", "https://news.hbtv.com.cn/app/tv/438"),
        new("hz_zonghe", "杭州综合", "地方 · 杭州", "https://tv.hoolo.tv/hzzh/"),
        new("hz_xihu", "西湖明珠", "地方 · 杭州", "https://tv.hoolo.tv/xhmz/"),
        new("hz_shenghuo", "杭州生活", "地方 · 杭州", "https://tv.hoolo.tv/hzsh/"),
        new("hz_yingshi", "杭州影视", "地方 · 杭州", "https://tv.hoolo.tv/utv/"),
        new("hz_qingshao", "杭州青少·体育", "地方 · 杭州", "https://tv.hoolo.tv/hzse/"),
    ];
    public static int DefaultIndex => Array.FindIndex(All, c => c.Id == "zhejiang");
    public static int Wrap(int index) => (index % All.Length + All.Length) % All.Length;
}
public record Settings
{
    public int Volume { get; set; } = 60;
    public string LastChannel { get; set; } = "zhejiang";
    public bool Remember { get; set; } = true;
    public bool StartFullscreen { get; set; }
    public List<string> Favorites { get; set; } = [];
}
