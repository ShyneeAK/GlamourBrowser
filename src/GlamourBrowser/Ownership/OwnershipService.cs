using System;
using System.IO;
using GlamourBrowser.Core.Ownership;

namespace GlamourBrowser.Ownership;

/// <summary>Holds the ownership store of the logged-in character and persists every change.</summary>
public sealed class OwnershipService : IDisposable
{
    private readonly string directory;
    private string? path;

    public OwnershipStore? Current { get; private set; }

    public event Action? Changed;

    public OwnershipService()
    {
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "ownership");
        Plugin.ClientState.Login += OnLogin;
        Plugin.ClientState.Logout += OnLogout;
        if (Plugin.ClientState.IsLoggedIn)
            OnLogin();
    }

    /// <summary>Replaces the container in the current store and saves it. Ignored while logged out.</summary>
    public void Submit(ContainerSnapshot snapshot)
    {
        if (Current == null || path == null)
            return;

        Current.ReplaceContainer(snapshot);
        try
        {
            OwnershipFile.Save(Current, path);
        }
        catch (IOException e)
        {
            Plugin.Log.Warning(e, $"Saving ownership file {path} failed");
        }
        Changed?.Invoke();
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
        Current = null;
        path = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        Plugin.ClientState.Login -= OnLogin;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
