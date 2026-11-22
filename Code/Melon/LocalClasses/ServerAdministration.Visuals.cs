using Melon.Interface;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration
{
    public VisualsStatus GetVisuals() => VisualsService.Instance.GetStatus();
    public void SetVisualsEnabled(bool enabled) => VisualsService.Instance.SetEnabled(enabled);
    public void SetVisualDefaults(VisualFormat format) => VisualsService.Instance.SetDefaults(format);
    public void SetVisualLimits(VisualLimits limits) => VisualsService.Instance.SetLimits(limits);
    public void SetVisualsCache(int megabytes, int maxConcurrentJobs) => VisualsService.Instance.SetCache(megabytes, maxConcurrentJobs);
    public long ClearVisualsCache() => VisualsService.Instance.ClearCache();
}
