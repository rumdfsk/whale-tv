using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TVLive.Core;
using Button = Wpf.Ui.Controls.Button;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
namespace TVLive;

public class MainWindow : Window
{
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVLive");
    readonly Settings settings;
    readonly Grid stage = new(), chrome = new(), videoOverlay = new();
    readonly VideoView video = new();
    readonly WebView2CompositionControl browser = new();
    // Composition control stays in the WPF visual tree: no native HWND can cover the player.
    readonly WebView2CompositionControl resolver = new()
    {
        Width = 1024, Height = 640,
        IsHitTestVisible = false, Focusable = false, IsTabStop = false
    };
    readonly Border drawer = new();
    readonly StackPanel channelRows = new();
    readonly TextBox search = new() { Margin = new Thickness(0, 12, 0, 12), ToolTip = "搜索频道或地区", FontSize = 18 };
    readonly TextBlock status = new() { Foreground = Brushes.LightGray, FontSize = 13, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock heading = new() { Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.SemiBold };
    readonly TextBlock clock = new() { Foreground = Brushes.White, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(30) };
    readonly Border dock = new();
    readonly SemaphoreSlim resolverGate = new(1);
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly DispatcherTimer hideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    readonly List<string> diagnostics = [];
    LibVLC? vlc; MediaPlayer? player;
    CancellationTokenSource? switchCancellation;
    int selected = Catalog.DefaultIndex, generation; bool official, fullscreen, closing, favoritesOnly, officialFullPage;
    WindowState previousState;
    public MainWindow()
    {
        Directory.CreateDirectory(DataDir);
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(DataDir, "settings.json"))) ?? new(); } catch { settings = new(); }
        settings.Favorites ??= []; settings.Volume = Math.Clamp(settings.Volume, 0, 100);
        Title = "鲸鱼电视";
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/whale-tv.ico")); Width = 1280; Height = 800; MinWidth = 900; MinHeight = 600;
        Background = new SolidColorBrush(Color.FromRgb(13, 17, 23)); Foreground = Brushes.White; FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        Content = stage;
        // Apply opacity on the WPF container; composition control exposes a read-only Opacity.
        stage.Children.Add(new Border { Child = resolver, Opacity = 0, IsHitTestVisible = false, Focusable = false });
        stage.Children.Add(browser); stage.Children.Add(video);
        browser.Visibility = Visibility.Hidden;
        browser.NavigationCompleted += async (_, e) =>
        {
            if (e.IsSuccess && official && !officialFullPage && IsCurrentOfficialPage())
                await AdaptOfficial(generation, switchCancellation?.Token ?? CancellationToken.None);
        };
        BuildChrome();
        // VideoView internally resets Content to null; retain one host and move its children.
        video.Content = videoOverlay; videoOverlay.Children.Add(chrome);
        PreviewKeyDown += OnKey; chrome.PreviewKeyDown += OnKey;
        foreach (var surface in new UIElement[] { this, chrome })
        {
            surface.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, _) => Wake()), true);
            surface.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => Wake()), true);
            surface.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((_, _) => Wake()), true);
            surface.AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler((_, _) => Wake()), true);
            surface.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, _) => Wake()), true);
            surface.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler((_, _) => Wake()), true);
        }
        timer.Tick += (_, _) => clock.Text = DateTime.Now.ToString("HH:mm");
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            dock.Visibility = Visibility.Hidden; clock.Visibility = Visibility.Hidden;
        };
        Loaded += async (_, _) => { Wake(); await Initialize(); };
        Closing += (_, _) => { closing = true; generation++; switchCancellation?.Cancel(); timer.Stop(); hideTimer.Stop(); Save(); player?.Stop(); video.Dispose(); player?.Dispose(); vlc?.Dispose(); resolver.Dispose(); browser.Dispose(); };
    }
    Button Btn(string text, Action action, double width = double.NaN)
    {
        var b = new Button { Content = text, Margin = new Thickness(4), Padding = new Thickness(14, 9, 14, 9), FontSize = 16, Width = width, Foreground = Brushes.White };
        b.Click += (_, _) => { Wake(); action(); }; return b;
    }
    void BuildChrome()
    {
        chrome.Background = Brushes.Transparent; chrome.Children.Add(clock);
        drawer.Width = 330; drawer.HorizontalAlignment = HorizontalAlignment.Left; drawer.Margin = new Thickness(20, 20, 0, 120); drawer.Padding = new Thickness(22); drawer.CornerRadius = new CornerRadius(16); drawer.Background = new SolidColorBrush(Color.FromArgb(245, 24, 29, 38)); drawer.Visibility = Visibility.Collapsed;
        var list = new DockPanel();
        var title = new TextBlock { Text = $"频道 · {Catalog.All.Length}", FontSize = 28, Foreground = Brushes.White }; DockPanel.SetDock(title, Dock.Top); list.Children.Add(title);
        DockPanel.SetDock(search, Dock.Top); list.Children.Add(search); search.TextChanged += (_, _) => RenderChannels();
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        tabs.Children.Add(Btn("全部", () => { favoritesOnly = false; RenderChannels(); })); tabs.Children.Add(Btn("收藏", () => { favoritesOnly = true; RenderChannels(); })); DockPanel.SetDock(tabs, Dock.Top); list.Children.Add(tabs);
        list.Children.Add(new ScrollViewer { Content = channelRows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); drawer.Child = list; chrome.Children.Add(drawer);
        dock.VerticalAlignment = VerticalAlignment.Bottom; dock.Margin = new Thickness(24); dock.Padding = new Thickness(16); dock.CornerRadius = new CornerRadius(16); dock.Background = new SolidColorBrush(Color.FromArgb(242, 25, 30, 39));
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition()); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; labels.Children.Add(heading); labels.Children.Add(status); layout.Children.Add(labels);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(controls, 1); layout.Children.Add(controls);
        controls.Children.Add(Btn("‹", () => _ = Switch(Catalog.Wrap(selected - 1))));
        controls.Children.Add(Btn("Ⅱ / ▶", TogglePause));
        controls.Children.Add(Btn("声音", () => { if (settings.Volume == 0) SetVolume(60); if (official) _ = OfficialCommand("start"); else SetVolume(settings.Volume); })); controls.Children.Add(Btn("›", () => _ = Switch(Catalog.Wrap(selected + 1))));
        controls.Children.Add(Btn("频道", ToggleDrawer)); controls.Children.Add(Btn("☆", () => { var id = Catalog.All[selected].Id; if (!settings.Favorites.Remove(id)) settings.Favorites.Add(id); Save(); RenderChannels(); }));
        controls.Children.Add(Btn("⛶", ToggleFullscreen)); controls.Children.Add(Btn("设置", ShowSettings));
        dock.Child = layout; chrome.Children.Add(dock); RenderChannels();
    }
    void RenderChannels()
    {
        channelRows.Children.Clear(); string group = "";
        for (var i = 0; i < Catalog.All.Length; i++)
        {
            var c = Catalog.All[i]; if (!(c.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) || c.Group.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)) || (favoritesOnly && !settings.Favorites.Contains(c.Id))) continue;
            if (group != c.Group) { group = c.Group; channelRows.Children.Add(new TextBlock { Text = group, Margin = new Thickness(5, 18, 0, 8), Foreground = Brushes.Gray }); }
            int index = i;
            var b = Btn($"{i + 1:00}   {c.Name}" + (settings.Favorites.Contains(c.Id) ? "  ★" : ""), () => { drawer.Visibility = Visibility.Collapsed; _ = Switch(index); });
            b.HorizontalContentAlignment = HorizontalAlignment.Left; b.BorderThickness = new Thickness(1); b.BorderBrush = i == selected ? Brushes.LightSkyBlue : Brushes.Transparent;
            channelRows.Children.Add(b);
        }
    }
    async Task Initialize()
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize(); vlc = new LibVLC("--no-video-title-show");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataDir, "WebView2"));
            await resolver.EnsureCoreWebView2Async(env); await browser.EnsureCoreWebView2Async(env);
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ActivityBridge);
            foreach (var core in new[] { resolver.CoreWebView2, browser.CoreWebView2 })
            {
                core.NewWindowRequested += (_, e) => { e.Handled = true; Report("官网请求新窗口，请用“打开官网”继续。"); };
            }
            browser.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (closing || !official || !IsCurrentOfficialPage() || e.Source != browser.Source?.AbsoluteUri) return;
                try
                {
                    using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                        !doc.RootElement.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return;
                    if (type.GetString() == "tvlive-activity") { Wake(); return; }
                    if (officialFullPage || type.GetString() != "tvlive-key" ||
                        !doc.RootElement.TryGetProperty("key", out var pressed) || pressed.ValueKind != JsonValueKind.String) return;
                    Wake();
                    var key = pressed.GetString() switch
                    {
                        "F11" => Key.F11, "Escape" => Key.Escape, "PageUp" => Key.PageUp,
                        "PageDown" => Key.PageDown, "ArrowUp" => Key.Up, "ArrowDown" => Key.Down,
                        "ArrowLeft" => Key.Left, "ArrowRight" => Key.Right,
                        "c" or "C" => Key.C, " " => Key.Space, _ => Key.None
                    };
                    HandleKey(key);
                }
                catch (JsonException) { }
                catch (KeyNotFoundException) { }
            };
            resolver.CoreWebView2.IsMuted = true;
            timer.Start(); if (settings.StartFullscreen) ToggleFullscreen();
            var last = settings.Remember ? Array.FindIndex(Catalog.All, c => c.Id == settings.LastChannel) : Catalog.DefaultIndex;
            await Switch(last >= 0 ? last : Catalog.DefaultIndex);
        }
        catch (Exception ex) { Report("启动失败：" + ex.GetType().Name + "。请查看错误详情。"); App.ShowFailure(ex); }
    }
    void MoveChrome(bool toBrowser)
    {
        videoOverlay.Children.Clear(); stage.Children.Remove(chrome);
        chrome.Background = toBrowser ? null : Brushes.Transparent;
        if (toBrowser) stage.Children.Add(chrome); else videoOverlay.Children.Add(chrome);
        video.Visibility = toBrowser ? Visibility.Hidden : Visibility.Visible;
        browser.Visibility = toBrowser ? Visibility.Visible : Visibility.Hidden;
    }
    async Task Switch(int index, bool useOfficial = false)
    {
        if (closing || browser.CoreWebView2 == null || vlc == null) return;
        int ticket = ++generation; switchCancellation?.Cancel(); switchCancellation = new CancellationTokenSource(); var ct = switchCancellation.Token;
        selected = index; var c = Catalog.All[index]; settings.LastChannel = c.Id; Save(); heading.Text = c.Name; RenderChannels(); Wake();
        player?.Stop(); video.MediaPlayer = null; player?.Dispose(); player = null;
        browser.CoreWebView2.IsMuted = true; browser.CoreWebView2.Navigate("about:blank");
        official = useOfficial || c.Host == null; officialFullPage = false; MoveChrome(official);
        if (official) { browser.CoreWebView2.IsMuted = c.Selection != null; browser.CoreWebView2.Navigate(c.Page); Report("正在适配官网视频区域…"); return; }
        Report("正在从官网刷新直播地址…"); var watch = Stopwatch.StartNew();
        try
        {
            var source = await Resolve(c, ct); ct.ThrowIfCancellationRequested(); if (ticket != generation) return;
            var next = new MediaPlayer(vlc); player = next; video.MediaPlayer = next; next.Volume = settings.Volume;
            next.Playing += (_, _) => Dispatcher.BeginInvoke(() => { if (ticket == generation && !closing) Report($"直播 · 直连模式 · {watch.Elapsed.TotalSeconds:F1} 秒就绪"); });
            next.EncounteredError += (_, _) => Dispatcher.BeginInvoke(() => { if (ticket == generation && !closing) Report("播放失败 · 在设置中重试，或切换官网模式"); });
            using var media = new Media(vlc, new Uri(source.Url));
            media.AddOption(":http-user-agent=" + source.UserAgent); media.AddOption(":http-referrer=" + c.Page); media.AddOption(":network-caching=1200");
            if (!next.Play(media)) Report("播放器未启动，请重试或打开官网。");
            _ = CheckPlayback(ticket, next, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (ticket == generation && !closing) Report("取流失败：" + ex.GetType().Name + " · 可重试或使用官网模式"); }
    }
    static readonly string OfficialAdapter = ReadWebScript("official-player.js");
    static readonly string ActivityBridge = ReadWebScript("activity.js");
    static string ReadWebScript(string name)
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("TVLive.Web." + name)
            ?? throw new InvalidOperationException("Missing official player adapter");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    bool IsCurrentOfficialPage()
    {
        if (!official || browser.Source == null) return false;
        var expected = new Uri(Catalog.All[selected].Page);
        return browser.Source.Host == expected.Host && browser.Source.AbsolutePath == expected.AbsolutePath
            && browser.Source.Query == expected.Query;
    }
    async Task AdaptOfficial(int ticket, CancellationToken ct)
    {
        try
        {
            for (int attempt = 0; attempt < 35; attempt++)
            {
                if (closing || ticket != generation || officialFullPage || !IsCurrentOfficialPage()) return;
                ct.ThrowIfCancellationRequested();
                var json = await browser.ExecuteScriptAsync(OfficialAdapter.Replace("__TVLIVE_CHANNEL__", JsonSerializer.Serialize(Catalog.All[selected].Selection)));
                if (closing || ticket != generation) return;
                using var result = JsonDocument.Parse(json);
                if (result.RootElement.TryGetProperty("supported", out var supported) && !supported.GetBoolean())
                { Report("当前官网尚未适配 · 保留官网播放控件"); return; }
                if (result.RootElement.TryGetProperty("found", out var found) && found.GetBoolean())
                { await OfficialCommand("start"); return; }
                await Task.Delay(1000, ct);
            }
            if (ticket == generation) Report("未识别到官网视频 · 可在设置中显示完整官网");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing && ticket == generation) Report("官网适配失败：" + ex.GetType().Name + " · 可恢复完整官网"); }
    }
    async Task OfficialCommand(string action)
    {
        if (closing || !IsCurrentOfficialPage()) return;
        int ticket = generation;
        try
        {
            var expression = "window.__tvLive ? window.__tvLive.command(" + JsonSerializer.Serialize(action) + ", " +
                (settings.Volume / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ") : ({unadapted:true})";
            // CDP userGesture represents the authorized playback action without moving the desktop mouse.
            var json = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new
            { expression, userGesture = true, awaitPromise = true, returnByValue = true })).WaitAsync(TimeSpan.FromSeconds(12));
            if (closing || ticket != generation) return;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("value", out var value))
            { Report("官网操作未完成 · 请点击页面播放或声音按钮"); return; }
            if (value.TryGetProperty("unadapted", out _)) { Report("完整官网模式 · 请使用页面播放控件"); return; }
            if (value.TryGetProperty("restored", out _)) { browser.CoreWebView2.IsMuted = false; return; }
            if (value.TryGetProperty("found", out var found) && found.GetBoolean()) browser.CoreWebView2.IsMuted = false;
            bool playing = value.TryGetProperty("playing", out var p) && p.GetBoolean();
            bool muted = value.TryGetProperty("muted", out var m) && m.GetBoolean();
            Report("官网视频模式 · " + (action == "volume" ? $"音量 {settings.Volume}% · " : "") + (playing ? "播放中" : "尚未播放，可点击播放按钮") + (muted ? " · 静音，可点击声音" : " · 已取消静音"));
        }
        catch (Exception ex) { if (!closing && ticket == generation) Report("官网控制失败：" + ex.GetType().Name + " · 可使用页面控件"); }
    }
    async Task ToggleOfficialPage()
    {
        if (!official) { Report("当前使用直连播放器"); return; }
        if (!officialFullPage)
        {
            officialFullPage = true;
            await OfficialCommand("restore");
            if (!closing) Report("完整官网模式 · 可再次点击设置恢复视频区域");
        }
        else
        {
            officialFullPage = false;
            await AdaptOfficial(generation, switchCancellation?.Token ?? CancellationToken.None);
        }
    }
    async Task CheckPlayback(int ticket, MediaPlayer p, CancellationToken ct)
    {
        try { await Task.Delay(20000, ct); if (ticket == generation && !p.IsPlaying && !closing) Report("20 秒内尚未播放 · 请重试或使用官网模式"); } catch (OperationCanceledException) { }
    }
    async Task<(string Url, string UserAgent)> Resolve(Channel c, CancellationToken ct)
    {
        await resolverGate.WaitAsync(ct);
        EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs>? handler = null;
        try
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler = (_, e) => { if (e.Response.StatusCode == 200 && c.Accepts(e.Request.Uri)) completion.TrySetResult(e.Request.Uri); };
            resolver.Visibility = Visibility.Visible;
            resolver.CoreWebView2.WebResourceResponseReceived += handler;
            resolver.CoreWebView2.Navigate(c.Page);
            var url = await completion.Task.WaitAsync(TimeSpan.FromSeconds(35), ct);
            return (url, resolver.CoreWebView2.Settings.UserAgent);
        }
        finally
        {
            try
            {
                if (!closing)
                {
                    if (handler != null) resolver.CoreWebView2.WebResourceResponseReceived -= handler;
                    resolver.CoreWebView2.Navigate("about:blank");
                    resolver.Visibility = Visibility.Hidden;
                }
            }
            finally { resolverGate.Release(); }
        }
    }
    void Report(string text) { status.Text = text; diagnostics.Add($"{DateTime.Now:HH:mm:ss} {Catalog.All[selected].Name} {text}"); if (diagnostics.Count > 200) diagnostics.RemoveAt(0); }
    void Wake()
    {
        if (closing) return;
        dock.Visibility = Visibility.Visible; clock.Visibility = Visibility.Visible;
        hideTimer.Stop(); hideTimer.Start();
    }
    void ToggleDrawer() { Wake(); drawer.Visibility = drawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (drawer.Visibility == Visibility.Visible) search.Focus(); }
    void TogglePause() { if (official) { _ = OfficialCommand("toggle"); return; } player?.Pause(); Wake(); }
    void ToggleFullscreen()
    {
        if (!fullscreen) { previousState = WindowState; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
        else { WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = previousState; }
        fullscreen = !fullscreen; Wake();
    }
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.IsRepeat && e.Key == Key.Space) { e.Handled = true; return; }
        if (Keyboard.FocusedElement is TextBox && e.Key != Key.Escape && e.Key != Key.F11) return;
        e.Handled = HandleKey(e.Key);
    }
    bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.F11: ToggleFullscreen(); break;
            case Key.Escape: if (drawer.Visibility == Visibility.Visible) drawer.Visibility = Visibility.Collapsed; else if (fullscreen) ToggleFullscreen(); break;
            case Key.C: ToggleDrawer(); break;
            case Key.Space: TogglePause(); break;
            case Key.Left:
            case Key.PageUp: _ = Switch(Catalog.Wrap(selected - 1)); break;
            case Key.Right:
            case Key.PageDown: _ = Switch(Catalog.Wrap(selected + 1)); break;
            case Key.Up: SetVolume(settings.Volume + 5); break;
            case Key.Down: SetVolume(settings.Volume - 5); break;
            default: return false;
        }
        return true;
    }
    void SetVolume(int value) { settings.Volume = Math.Clamp(value, 0, 100); if (player != null) player.Volume = settings.Volume; if (official) _ = OfficialCommand("volume"); Save(); Wake(); }
    void Save() { try { File.WriteAllText(Path.Combine(DataDir, "settings.json"), JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
    void ShowSettings()
    {
        Wake(); var window = new Window { Title = "设置 · 鲸鱼电视", Width = 570, Height = 650, Owner = this, Icon = Icon, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Brushes.White, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(28) }; window.Content = panel;
        panel.Children.Add(new TextBlock { Text = "播放与验证", FontSize = 28, Margin = new Thickness(0, 0, 0, 22) });
        var remember = new CheckBox { Content = "记住上次频道", IsChecked = settings.Remember, Margin = new Thickness(0, 8, 0, 8) }; remember.Click += (_, _) => { settings.Remember = remember.IsChecked == true; Save(); }; panel.Children.Add(remember);
        var full = new CheckBox { Content = "启动时全屏", IsChecked = settings.StartFullscreen, Margin = new Thickness(0, 8, 0, 8) }; full.Click += (_, _) => { settings.StartFullscreen = full.IsChecked == true; Save(); }; panel.Children.Add(full);
        panel.Children.Add(new TextBlock { Text = "播放器音量（含已适配官网）", Margin = new Thickness(0, 15, 0, 8) });
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = settings.Volume, TickFrequency = 5, IsSnapToTickEnabled = true }; volume.ValueChanged += (_, _) => SetVolume((int)volume.Value); panel.Children.Add(volume);
        panel.Children.Add(Btn("重新取流并播放", () => { window.Close(); _ = Switch(selected); }));
        panel.Children.Add(Btn("改用内置官网播放器", () => { window.Close(); _ = Switch(selected, true); }));
        panel.Children.Add(Btn("显示完整官网 / 恢复视频区域", () => { window.Close(); _ = ToggleOfficialPage(); }));
        panel.Children.Add(Btn("在浏览器打开官网", () => Process.Start(new ProcessStartInfo(Catalog.All[selected].Page) { UseShellExecute = true })));
        panel.Children.Add(Btn("导出诊断日志", () => { var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "鲸鱼电视-诊断日志.txt", Filter = "文本|*.txt" }; if (dialog.ShowDialog(window) == true) File.WriteAllLines(dialog.FileName, diagnostics.Prepend("鲸鱼电视 0.1.6 | " + Environment.OSVersion)); }));
        panel.Children.Add(new TextBlock { Text = "F11 全屏 · Esc 返回 · C 频道\n← → 换台 · ↑ ↓ 音量 · 空格暂停/继续\nPageUp / PageDown 也可换台。\n验证版 0.1.6 · 仅使用官方公开播放页面", Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) });
        window.ShowDialog();
        Wake();
    }
}
