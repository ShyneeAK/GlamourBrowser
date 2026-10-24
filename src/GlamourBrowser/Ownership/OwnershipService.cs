using System;
using System.IO;
using Dalamud.Plugin.Services;
using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Ownership;

/// <summary>
/// Holds the ownership store of the logged-in character. Submissions are collected and written and
/// announced at most once per frame; snapshots that change nothing are not written at all.
/// </summary>
public sealed class OwnershipService : IDisposable
{
    private readonly string directory;
    private string? path;
    private bool dirty;
    private bool changedPending;

    public OwnershipStore? Current { get; private set; }

    public event Action? Changed;

    public OwnershipService()
    {
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "ownership");
        Plugin.ClientState.Login += OnLogin;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.Framework.Update += Flush;
        if (Plugin.ClientState.IsLoggedIn)
            OnLogin();
    }

    /// <summary>Replaces the container in the current store; saved on the next frame. Ignored while logged out.</summary>
    public void Submit(ContainerSnapshot snapshot)
    {
        if (Current == null || path == null)
            return;

        if (Current.ReplaceContainer(snapshot))
        {
            dirty = true;
            changedPending = true;
        }
        else if (snapshot.Id.Kind is ContainerKind.Retainer or ContainerKind.Dresser or ContainerKind.Armoire)
        {
            // Rarely opened containers: keep their "Stand" time current even when nothing changed.
            dirty = true;
        }
    }

    private void Flush(IFramework framework) => Flush();

    private void Flush()
    {
        if (dirty && Current != null && path != null)
        {
            try
            {
                OwnershipFile.Save(Current, path);
            }
            catch (IOException e)
            {
                Plugin.Log.Warning(e, $"Saving ownership file {path} failed");
            }
        }
        dirty = false;

        if (changedPending)
        {
            changedPending = false;
            Changed?.Invoke();
        }
    }

    private void OnLogin()
    {
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0)
            return;

        path = Path.Combine(directory, $"{contentId}.json");
        Current = OwnershipFile.Load(path, contentId);
        Plugin.Log.Information($"Ownership loaded for {contentId}: {Current.Containers.Count} containers");
        Changed?.Invoke();
    }

    private void OnLogout(int type, int code)
    {
        Flush();
        Current = null;
        path = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Flush;
        Flush();
        Plugin.ClientState.Login -= OnLogin;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
