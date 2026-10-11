using System;
using System.Collections.Generic;
using CrucibleSux.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Game;

/// <summary>The installed plugins as <see cref="DependencyCatalog"/> reads them.</summary>
public sealed class PluginInventory(IDalamudPluginInterface pluginInterface, IPluginLog log)
{
    private bool warned;

    public IReadOnlyList<InstalledPlugin> Read()
    {
        var result = new List<InstalledPlugin>();
        try
        {
            foreach (var p in pluginInterface.InstalledPlugins)
            {
                // A fork can keep its upstream's RepoUrl, so the custom repo it was installed from is checked as well.
                var urls = new[] { p.Manifest.RepoUrl, p.Manifest.InstalledFromUrl };
                string repo = string.Join(" | ", Array.FindAll(urls, u => !string.IsNullOrEmpty(u)));
                result.Add(new InstalledPlugin(p.InternalName, p.Version, p.IsLoaded, repo.Length > 0 ? repo : null));
            }
        }
        catch (Exception ex)
        {
            if (!this.warned)
            {
                this.warned = true;
                log.Warning(ex, "The installed plugin list is unavailable; companion plugins read as missing");
            }
        }
        return result;
    }
}
