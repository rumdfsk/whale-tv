# 鲸鱼电视 Android 手机体验版

版本 0.1.0，Android 8.0（API 26）及以上。独立测试应用包名 `cn.whaletv.mobile.test`。

8 个频道：浙江、江苏、东方、山东、湖南卫视，CCTV-1、CCTV-5、CCTV-13。前三个频道从公开官网加载时识别匹配的 HTTPS HLS 地址，并交给 Media3 播放；其余使用 Android WebView 加载官网。流地址不固化、不落盘。官网的地区、登录与版权限制仍然适用。

竖屏点选频道；全屏按钮切换横屏；原生播放器支持双击画面切换全屏。任意触摸或按键唤醒控制栏，4 秒无操作自动隐藏。音量条调整手机媒体音量。进入后台暂停播放。更多菜单可重新连接、切换官网模式、显示完整官网或复制不含流地址的诊断信息。

首次安装需要在手机上为下载 APK 的浏览器开启“允许安装未知应用”。这是本地测试签名，不是应用商店发布版。仅请求网络相关权限，不请求摄像头、麦克风、位置或存储权限。需手机实测播放、音频、旋转与不同系统 WebView 的兼容性。

## 构建

JDK 17 或更新版本，Android SDK Platform 35 / Build Tools 35.0.0，Gradle 8.11.1，Android Gradle Plugin 8.9.2。将 `local.properties` 中的 `sdk.dir` 改为自己的 SDK 路径，或设置 `ANDROID_HOME`。

```sh
./gradlew assembleDebug testDebugUnitTest lintDebug
```

产物：`app/build/outputs/apk/debug/app-debug.apk`。

## 手机验收

1. 分别播放 8 个频道，观察连接结果；官方页面提示限制时保留提示。
2. 反复换台，确认没有旧频道声音；失败后用“更多 → 重新连接”。
3. 调整音量、暂停/继续，横竖屏切换；检查画面比例和控制栏 4 秒隐藏。
4. 按 Home、锁屏，确认暂停；回到应用点播放继续。
5. 遇到问题通过“更多 → 复制诊断信息”提供频道、系统版本和状态。
