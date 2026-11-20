package cn.whaletv.mobile;

import java.net.URI;
import java.util.Locale;

final class Channel {
    final String id, name, page, host, path;
    Channel(String id, String name, String page, String host, String path) {
        this.id=id; this.name=name; this.page=page; this.host=host; this.path=path;
    }
    boolean direct() { return host != null; }
    boolean accepts(String address) {
        if (!direct()) return false;
        try {
            URI uri=URI.create(address);
            String h=uri.getHost(), p=uri.getPath();
            return "https".equalsIgnoreCase(uri.getScheme()) && h != null && p != null
                && (h.equalsIgnoreCase(host) || (host.startsWith(".") && h.toLowerCase(Locale.ROOT).endsWith(host)))
                && p.contains(path) && p.toLowerCase(Locale.ROOT).endsWith(".m3u8");
        } catch (IllegalArgumentException e) { return false; }
    }
    static int wrap(int index) { return Math.floorMod(index, ALL.length); }
    static final Channel[] ALL = {
        new Channel("zhejiang", "浙江卫视", "https://www.cztv.com/liveTV", ".cztv.com", "/live/channel01"),
        new Channel("jiangsu", "江苏卫视", "https://live.jstv.com/?id=9", "litchi-play-encrypted-site.jstv.com", "/applive/jswspro"),
        new Channel("dongfang", "东方卫视", "https://live.kankanews.com/huikan", "volc-stream.kksmg.com", "/live/dfws4k/index"),
        new Channel("cctv1", "CCTV-1 综合", "https://www.yangshipin.cn/tv/home?pid=600001859", null, null),
        new Channel("cctv13", "CCTV-13 新闻", "https://www.yangshipin.cn/tv/home?pid=600001811", null, null),
        new Channel("cctv5", "CCTV-5 体育", "https://www.yangshipin.cn/tv/home?pid=600001818", null, null),
        new Channel("shandong", "山东卫视", "https://www.yangshipin.cn/tv/home?pid=600002513", null, null),
        new Channel("hunan", "湖南卫视", "https://www.yangshipin.cn/tv/home?pid=600002475", null, null)
    };
}
