using BepInEx.Unity.IL2CPP;
using TheArchive;
using TheArchive.Core;

namespace Hikaria.NetworkQualityTracker;

public class EntryPoint : BasePlugin, IArchiveModule
{
    public override void Load()
    {
        Instance = this;

        ArchiveMod.RegisterModule(typeof(EntryPoint));

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

    public bool ApplyHarmonyPatches => throw new NotImplementedException();

    public bool UsesLegacyPatches => throw new NotImplementedException();

    public ArchiveLegacyPatcher Patcher { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
}
