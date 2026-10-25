using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin.Services;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.CustomSets;

/// <summary>
/// Holds the custom sets of the logged-in character (one file per character). Changes are written and announced on
/// the next frame; a failed save is tried again with the next change and at logout.
/// </summary>
public sealed class CustomSetService : IDisposable
{
    private readonly string directory;
    private readonly IReadOnlyDictionary<uint, uint> variants;
    private string? path;
    // Changed since the last frame: saved and announced on the next one.
    private bool dirty;
    // The last save failed: tried again with the next change and at logout, not on every frame.
    private bool unsaved;

    public CustomSetStore? Current { get; private set; }

    /// <summary>The logged-in character's file could not be read; it is left alone and the browser says so.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>Login, logout and every change of the sets.</summary>
    public event Action? Changed;

    /// <param name="variants">"+N" variants mapped to their base item: a set stores the base.</param>
    public CustomSetService(IReadOnlyDictionary<uint, uint> variants)
    {
        this.variants = variants;
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "customsets");
        Plugin.ClientState.Login += OnLogin;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.Framework.Update += Flush;
        if (Plugin.ClientState.IsLoggedIn)
            OnLogin();
    }

    private void OnStoreChanged() => dirty = true;

    private void Flush(IFramework framework) => Flush(retry: false);

    /// <param name="retry">Also save when nothing changed but the last save failed (logout, unload).</param>
    private void Flush(bool retry)
    {
        // The UI changes the sets while drawing; the lock keeps saving from reading them halfway.
        lock (RenderSync.Lock)
        {
            if (!dirty && !(retry && unsaved))
                return;

            if (Current != null && path != null)
            {
                try
                {
                    CustomSetFile.Save(Current, path);
                    unsaved = false;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    unsaved = true;
                    Plugin.Log.Warning(e, $"Saving custom sets file {path} failed");
                }
            }

            if (dirty)
            {
                dirty = false;
                Changed?.Invoke();
            }
        }
    }

    private void OnLogin()
    {
        var contentId = Plugin.PlayerState.ContentId;
        if (contentId == 0)
            return;

        lock (RenderSync.Lock)
        {
            // A login without a logout in between: the previous character's changes are saved first.
            Flush(retry: true);
            Release();
            path = Path.Combine(directory, $"{contentId}.json");
            try
            {
                Current = CustomSetFile.Load(path, contentId, variants);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // No empty list in its place: its first save would overwrite the real file.
                Plugin.Log.Warning(e, $"Loading custom sets file {path} failed");
                path = null;
                LoadFailed = true;
                Changed?.Invoke();
                return;
            }
            Current.Changed += OnStoreChanged;
            Plugin.Log.Information($"Custom sets loaded for {contentId}: {Current.Sets.Count} sets");
            Changed?.Invoke();
        }
    }

    private void OnLogout(int type, int code)
    {
        lock (RenderSync.Lock)
        {
            Flush(retry: true);
            Release();
            Changed?.Invoke();
        }
    }

    private void Release()
    {
        if (Current != null)
            Current.Changed -= OnStoreChanged;
        Current = null;
        path = null;
        unsaved = false;
        LoadFailed = false;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Flush;
        Flush(retry: true);
        Plugin.ClientState.Login -= OnLogin;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
