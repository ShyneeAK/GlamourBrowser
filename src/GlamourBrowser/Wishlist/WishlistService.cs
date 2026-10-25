using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin.Services;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.Wishlist;

/// <summary>
/// Holds the wishlist of the logged-in character (one file per character). Changes are written and announced
/// on the next frame.
/// </summary>
public sealed class WishlistService : IDisposable
{
    private readonly string directory;
    private readonly IReadOnlyDictionary<uint, uint> variants;
    private string? path;
    private bool dirty;

    public WishlistStore? Current { get; private set; }

    /// <summary>Login, logout and every change of the list.</summary>
    public event Action? Changed;

    /// <param name="variants">"+N" variants mapped to their base item: a wish for a variant is a wish for its base.</param>
    public WishlistService(IReadOnlyDictionary<uint, uint> variants)
    {
        this.variants = variants;
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "wishlist");
        Plugin.ClientState.Login += OnLogin;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.Framework.Update += Flush;
        if (Plugin.ClientState.IsLoggedIn)
            OnLogin();
    }

    public bool IsListed(TileSubject subject) => Current?.Contains(WishKey.Of(subject)) ?? false;

    /// <summary>Puts the item or set on the list, or takes it off. Ignored while logged out.</summary>
    public void Toggle(TileSubject subject)
    {
        lock (RenderSync.Lock)
        {
            if (Current == null)
                return;

            var key = WishKey.Of(subject);
            if (!Current.Remove(key))
                Current.Add(key, DateTimeOffset.Now);
        }
    }

    private void OnStoreChanged() => dirty = true;

    private void Flush(IFramework framework) => Flush();

    private void Flush()
    {
        // The UI changes the list while drawing; the lock keeps saving from reading it halfway.
        lock (RenderSync.Lock)
        {
            if (!dirty)
                return;

            dirty = false;
            if (Current != null && path != null)
            {
                try
                {
                    WishlistFile.Save(Current, path);
                }
                catch (IOException e)
                {
                    Plugin.Log.Warning(e, $"Saving wishlist file {path} failed");
                }
            }
            Changed?.Invoke();
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
            Flush();
            Release();
            path = Path.Combine(directory, $"{contentId}.json");
            Current = WishlistFile.Load(path, contentId, variants);
            Current.Changed += OnStoreChanged;
            Plugin.Log.Information($"Wishlist loaded for {contentId}: {Current.Entries.Count} entries");
            Changed?.Invoke();
        }
    }

    private void OnLogout(int type, int code)
    {
        lock (RenderSync.Lock)
        {
            Flush();
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
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Flush;
        Flush();
        Plugin.ClientState.Login -= OnLogin;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
