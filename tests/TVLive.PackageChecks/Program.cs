using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
var dir = args[0];
string[] targets = ["鲸鱼电视", "TVLive.Core", "Wpf.Ui", "Wpf.Ui.Abstractions", "LibVLCSharp", "LibVLCSharp.WPF", "Microsoft.Web.WebView2.Wpf", "Microsoft.Web.WebView2.Core", "Microsoft.Windows.SDK.NET", "WinRT.Runtime"];
foreach (var target in targets)
{
    using var stream = File.OpenRead(Path.Combine(dir, target + ".dll"));
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    if (target == "鲸鱼电视")
    {
        var resources = md.ManifestResources.Select(h => md.GetString(md.GetManifestResource(h).Name)).ToHashSet();
        foreach (var name in new[] { "TVLive.Web.official-player.js", "TVLive.Web.activity.js" })
            if (!resources.Contains(name)) throw new Exception("Missing embedded browser script: " + name);
        Console.WriteLine("PASS embedded player and activity scripts present");
    }
    foreach (var handle in md.AssemblyReferences)
    {
        var reference = md.GetAssemblyReference(handle); var name = md.GetString(reference.Name);
        var path = Path.Combine(dir, name + ".dll");
        if (!File.Exists(path)) throw new Exception($"{target} needs missing assembly {name}");
        using var depStream = File.OpenRead(path); using var depPe = new PEReader(depStream);
        var actual = depPe.GetMetadataReader().GetAssemblyDefinition().Version;
        if (actual < reference.Version) throw new Exception($"{target} needs {name} >= {reference.Version}; found {actual}");
    }
    Console.WriteLine($"PASS {target} assembly references present and versions sufficient");
}
