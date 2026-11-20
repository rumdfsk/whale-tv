using TVLive.Core;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
var zj = Catalog.All.Single(c => c.Id == "zhejiang");
Check(zj.Accepts("https://zwebl02.cztv.com/live/channel01720Pnew.m3u8?auth_key=abc"), "Zhejiang signed official stream");
Check(!zj.Accepts("https://evilcztv.com/live/channel01720Pnew.m3u8"), "Reject suffix lookalike");
Check(!zj.Accepts("https://zwebl02.cztv.com/live/channel02720Pnew.m3u8"), "Reject other channel");
Check(!zj.Accepts("http://zwebl02.cztv.com/live/channel01720Pnew.m3u8"), "Reject insecure URL");
Check(!zj.Accepts("https://cztv.com.evil.test/live/channel01720Pnew.m3u8"), "Reject spoofed host");
Check(Catalog.All.Single(c => c.Id == "jiangsu").Accepts("https://litchi-play-encrypted-site.jstv.com/applive/jswspro.m3u8?txSecret=abc"), "Jiangsu stream");
Check(Catalog.All.Single(c => c.Id == "dongfang").Accepts("https://volc-stream.kksmg.com/live/dfws4k/index.m3u8?token=abc"), "Dongfang stream");
Check(!Catalog.All[0].Accepts("https://example.org/index.m3u8"), "Official-only channel cannot resolve arbitrary streams");
Check(Catalog.Wrap(-1) == Catalog.All.Length - 1 && Catalog.Wrap(Catalog.All.Length) == 0 && Catalog.Wrap(-2 * Catalog.All.Length - 1) == Catalog.All.Length - 1, "Channel wrap boundaries");
Check(Catalog.All.Select(c => c.Id).Distinct().Count() == Catalog.All.Length, "Unique stable IDs");

Check(Catalog.All.Length == 87, "Expanded 87-channel catalog");
Check(Catalog.All[Catalog.DefaultIndex].Id == "zhejiang", "Stable default after reordering");
Check(new[]{"cctv1","cctv13","cctv5","hunan","zhejiang","jiangsu","dongfang"}.All(id=>Catalog.All.Any(c=>c.Id==id)), "Original favorites IDs retained");
Check(Catalog.All.Where(c=>c.Host==null).All(c=>new[]{"www.yangshipin.cn","www.cztv.com","live.kankanews.com","www.gdtv.cn","news.hbtv.com.cn","tv.hoolo.tv"}.Contains(new Uri(c.Page).Host)), "New entries use adapted official providers");

Check(Catalog.All.Count(c=>c.Group.StartsWith("地方")) == 35, "35 local channels grouped by area");
Check(Catalog.All.Where(c=>c.Group=="地方 · 上海").All(c=>!string.IsNullOrEmpty(c.Selection)), "Shanghai entries select the requested official channel");
