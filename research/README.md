# 电视直播小范围技术验证

这是一组研究脚本和三频道测试页，不是完整 Windows 软件。2026-10-24 在当前 macOS 环境运行；Windows、WebView2 和物理扬声器听验尚未进行。

## 本轮结果

| 样本 | 独立播放结果 | 条件 / 边界 |
|---|---|---|
| 浙江卫视 | H.264 + AAC，1280×720，90 秒媒体解码通过 | 使用官网当前签名地址；未添加特殊请求头 |
| 江苏卫视 | H.264 + AAC，1920×1080，90 秒媒体解码通过 | 必须保留官网 Referer；无 Referer 的对照请求返回 403 |
| 东方卫视 | H.264 + AAC，1920×1080，20 秒媒体解码通过 | 使用官网当前令牌及匹配的会话 User-Agent；通用客户端和仅 Referer 请求返回 403 |
| CCTV1 / CCTV5 / CCTV13 | 央视频网页 1920×1080 播放通过 | 独立流解析未通过验证；不能据此说绝对不可行 |
| 湖南卫视 | 央视频网页 1920×1080 播放通过 | 独立流解析未通过验证；芒果直播首页的当前视频不能当作湖南卫视 |

两轮共 16 次自动换台全部进入播放并保持全屏，每次均测得非零音频信号；本机自动换台到 `playing` 事件耗时 206–938 ms。另有方向键、数字键、PageDown 三次单独换台通过（472–1281 ms）；快速输入 2→3→1→2→3 后，最终正确停在东方卫视。官网页全部关闭后，仍能重新选择频道并播放。

两次初始加载均被拒绝有声自动播放（`NotAllowedError`），一次用户操作后后续切换可有声播放。全屏只出现主动进入和主动退出事件；没有切台导致的退出。测试期间没有 HLS 致命错误或 `stalled` 事件；两频道测试中的一次浙江播放记录有 6 帧丢帧，因此不应描述为“零丢帧”。这是数分钟、三个视频源的短测，尚未证明长期稳定。

机器可读汇总：`results/summary.json`。最终取舍：三个已验证卫视可优先走独立播放器；央视和湖南暂保留官方页面方案，后续重点验证正常会话取流与刷新。

## 文件

- `results/site-observations.json`：央视、湖南及三个卫视的网页/直流验证边界。
- `results/native-initial.json`、`zhejiang-90s.json`、`jiangsu-90s.json`：独立解码、分辨率、音频能量和媒体时间跨度。
- `results/dongfang-initial.json`、`dongfang-context.json`：东方卫视无上下文请求失败，以及正常官网会话请求头下的成功结果。
- `results/dongfang-header-control.json`：后续仅保留匹配 User-Agent 的播放列表请求也返回 200；完整解码测试使用的是已记录的全部会话请求头。
- `results/address-reuse.json`：同一个临时地址在若干分钟内重复请求的结果，不能据此认定永久有效或准确有效期。
- `results/browser-two-channels.json`：10 次自动换台、3 次单独快捷键测试的原始记录。
- `results/browser-three-channels.json`：三频道交叉换台及快速按键记录。
- `scripts/probe_stream.py`：直接通过 PyAV/libav 读取音视频。只在内存中解码，不录像。
- `harness/`：本地 HLS 测试页与仅绑定 `127.0.0.1` 的临时转发服务。
- `private/`：当前会话临时直播地址，已排除出 Git，可能失效；不应发布或当作永久频道库。

## 复现

需要 Python 3.10+，安装 `requirements.txt`。将从正常官方播放会话取得的有效 HLS 地址填入 `private/streams.json`，格式见 `streams.example.json`。不生成签名、不提取 DRM 密钥、不处理付费内容。

从 `research` 目录运行：

```sh
python scripts/probe_stream.py private/streams.json results/native-rerun.json --seconds 20
python harness/server.py
```

打开 `http://127.0.0.1:8765/`。先观察首次有声自动播放是否被拒绝，再点“开始有声播放”。测试全屏、方向键、数字 1/2/3、PageUp/PageDown，最后点“保存测试结果”。它保存到 `results/browser-harness.json`。`Ctrl+C` 关闭本地服务。

测试页使用官方发行的 HLS.js 1.7.3，版本和校验值见 `harness/vendor/source.json`，许可证随附。本地转发仅接受配置中的 CDN 同源子资源，保留已验证的正常请求头，不提供任意 URL 代理。正式 Windows 播放器可以直接配置必要请求头；本地转发仅用于本轮浏览器验证。

## 如何理解结果

1. `90 media seconds` 是解码出的媒体时间跨度；由于可以先读取已有直播缓冲，其耗时可能少于 90 秒，不等于做过 90 秒物理扬声器听验。
2. 换台时间从提交切换到浏览器 `playing` 事件，受本机网络、CDN 缓冲和本地转发影响；不代表 Windows 成品的保证指标，也不代表直播相对电视信号的延迟。
3. 声音验证包括解码后非零 PCM 信号、浏览器未静音、AudioContext 运行及非零音频采样；未使用麦克风或系统音频录制。
4. 全屏测试是在点击一次全屏后切换频道，验证播放器容器持续全屏。尚未验证 Windows 应用从启动即自动全屏。
5. 央视/湖南在央视频网页中的播放成功，不能算独立视频流成功。页面加载了定制 HLS/CMG/WASM 播放组件；共享库出现加密相关代码，也不能证明每个频道都使用 DRM。
6. 本轮保留官网正常取得的临时签名与请求上下文；自动更新地址、跨网络、隔天复测、长时间稳定性和 Windows 实机行为仍需单独验证。

## 后续 Windows 最小验证范围

只需做一个诊断窗口：三路独立播放器 + 央视频网页备用入口。验证首次自动出声、原生全屏、键盘焦点、连续换台、关闭旧音轨、地址正常刷新，以及至少 30 分钟播放。先解决这些，再决定是否开发频道库与完整界面。

WebView2 的官方网络事件支持观察主文档和 worker 请求，可用于进一步检查正常官方会话的媒体请求；API 能力不等于已验证央视流能脱离播放器独立播放：
https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested
