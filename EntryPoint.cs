using BepInEx;
using BepInEx.Unity.IL2CPP;
using TheArchive;
using TheArchive.Core;
using TheArchive.Core.Localization;

namespace Hikaria.NetworkQualityTracker;

[BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(ArchiveMod.GUID, BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
public class EntryPoint : BasePlugin, IArchiveModule
{
    public override void Load()
    {
        Instance = this;

        ArchiveMod.RegisterArchiveModule(typeof(EntryPoint));

        Logs.LogMessage("OK");
    }

    public void Init()
    {
    }

    public void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
    }

    public void OnLateUpdate()
    {
    }

    public void OnExit()
    {
    }

    public static EntryPoint Instance { get; private set; }

    public bool ApplyHarmonyPatches => false;

    public bool UsesLegacyPatches => false;

    public ArchiveLegacyPatcher Patcher { get; set; }

    public string ModuleGroup => "Network Quality Tracker";

    public Dictionary<Language, string> ModuleGroupLanguages => new()
    {
        { Language.Chinese, "网络质量跟踪器" },
        { Language.English, "Network Quality Tracker" }
    };
}
