# 0.1.4 地方频道来源与验证

核对日期：2026-10-24。用户指定从电视眼继续发现来源；只使用它链接的电视台官网，未导入来路不明的 IPTV 列表。频道名称以当前官网为准。

|电视眼入口|当前官方入口|适配方法|
|---|---|---|
|https://www.tvyan.com/tai/zjqjpd/|https://www.cztv.com/liveTV/102|旧 `/live/?cid=...` 改为当前 `/liveTV/{id}`，保留快手播放器容器 `.videoKS`|
|https://www.tvyan.com/tai/stvxw/|https://live.kankanews.com/huikan/|先点击对应频道条目并核对 active 状态，再聚焦 `.live-box`，保留其版权/不可播图片|
|https://www.tvyan.com/tai/gdzjpd/|https://www.gdtv.cn/tvChannelDetail/44|旧 `zj.gdtv.cn` 跳转到当前页面；保留 `#J_prismPlayer`，必要时触发官网播放按钮|
|https://www.tvyan.com/tai/hbjs/|https://news.hbtv.com.cn/app/tv/432|电视眼旧链接 `/161` 无播放器，官网导航已更新到 `/432`，保留 `#player-con`|
|https://www.tvyan.com/tai/hzzhpd/|https://tv.hoolo.tv/hzzh/|保留 `#m2o_liveplayer`，必要时触发官网播放按钮|

随后读取各官网当前频道导航，取得同站其他频道链接。完整名单及具体地址在 `src/TVLive.Core/Channels.cs`。所有地方频道均使用官网视频模式，不缓存签名播放地址。

## 本机浏览器抽查

- 浙江当前频道（之江纪录）、上海第一财经、广东珠江、湖北经视、杭州综合均观察到 `videoWidth=1280`、`videoHeight=720`（湖北仅记录宽度）、`paused=false`、`readyState=4`。广东、湖北、杭州经官网播放按钮启动。
- 上海新闻综合在抽查时段出现官网图片提示，没有 video；换到第一财经后正常播放。应用保留 `.live-box` 中的图片与提示，不能把该时段标记为播放成功。
- 新增央视频频道的页面 PID 通过逐个点击官方频道列表核对。该步骤是地址确认，不等同于逐台播放验收。广东卫视的央视频入口一次抽查出现暂停/无画面，需关注官网状态或使用广东官方页面后续排查。
- 本机浏览器媒体状态不等同于 Windows WebView2 实机声音或长期稳定性测试。

## 机制回归

用 DOM 模拟覆盖 6 类官方播放器、延迟加载、取消静音、零音量、暂停保持、左右上下及空格按键转发、空格连按抑制、输入框保护、恢复完整页面、上海频道选择确认，以及无 video 时版权图片保留。未绕过付费、登录、地域或浏览器安全限制。

Windows 验收建议：先测杭州综合、广东珠江、湖北经视、钱江都市、第一财经，再检查频道左右循环、音量上下调节、空格暂停/继续和 F11 全屏。
