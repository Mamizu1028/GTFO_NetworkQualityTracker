using Hikaria.Core;
using TheArchive.Core;
using TheArchive.Core.Attributes;
using TheArchive.Core.Localization;
using TheArchive.Interfaces;

namespace Hikaria.NetworkQualityTracker;

[ArchiveDependency(CoreGlobal.GUID)]
[ArchiveModule(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
public class EntryPoint : IArchiveModule
{
    public ILocalizationService LocalizationService { get; set; }
    public IArchiveLogger Logger { get; set; }

    public void Init()
    {
        Logs.Setup(Logger);
    }
}
