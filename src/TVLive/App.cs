using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
namespace TVLive;
public class App : Application
{
    [STAThread] public static void Main()
    {
        try { RunApplication(); }
        catch (Exception ex) { ShowFailure(ex); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunApplication()
    {
        var app = new App();
        app.DispatcherUnhandledException += (_, e) => { ShowFailure(e.Exception); e.Handled = true; app.Shutdown(1); };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        app.Run(new MainWindow());
    }
    public static void ShowFailure(Exception ex)
    {
        var detail = Regex.Replace(ex.ToString(), @"(https?://[^\s?]+)\?[^\s]+", "$1?[redacted]");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVLive", "last-error.txt");
        var saved = false;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, $"鲸鱼电视 0.1.6 | {DateTime.Now:O}\n{Environment.OSVersion}\n{detail}"); saved = true; } catch { }
        var cause = ex.GetBaseException();
        var file = cause is FileNotFoundException missing ? "\n缺失文件：" + missing.FileName : "";
        MessageBox.Show("启动或运行失败：" + cause.GetType().Name + file + "\n" + cause.Message + (saved ? "\n\n详细日志：" + path : "\n\n日志无法写入。") + "\n请将错误信息发给我进一步定位。", "鲸鱼电视 · 错误详情");
    }
}
