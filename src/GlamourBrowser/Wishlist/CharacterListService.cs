using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin.Services;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Wishlist;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.Wishlist;

/// <summary>
/// Holds a list of the logged-in character (one file per character): the wishlist, the favourites or the items clicked
/// last. Changes are written and announced on the next frame.
/// </summary>
public sealed class CharacterListService : IDisposable
{
    private readonly string directory;
    private readonly string label;
    private readonly IReadOnlyDictionary<uint, uint> variants;
    private string? path;
    // Changed since the last frame: announced on the next one, and saved (see saveInterval).
    private bool dirty;
    // Changed since the last save.
    private bool saveDue;
    // At most one save per interval (the history changes with every click); the last change is saved after it.
    private readonly TimeSpan saveInterval;
    private DateTime lastSave = DateTime.MinValue;
    // The last save failed: tried again with the next change and at logout, not on every frame.
    private bool unsaved;

    public WishlistStore? Current { get; private set; }

    /// <summary>The most items "Zuletzt benutzt" keeps.</summary>
    public const int HistoryLimit = 1000;

    /// <summary>The logged-in character's file could not be read; it is left alone and the browser says so.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>Login, logout and every change of the list.</summary>
    public event Action? Changed;

    /// <param name="folder">The folder of the list's files in the plugin configuration, e.g. "wishlist".</param>
    /// <param name="label">The list's name in the log.</param>
    /// <param name="variants">"+N" variants mapped to their base item: a wish for a variant is a wish for its base.</param>
    /// <param name="saveInterval">The least time between two saves; zero saves every change on the next frame.</param>
    public CharacterListService(string folder, string label, IReadOnlyDictionary<uint, uint> variants, TimeSpan saveInterval = default)
    {
        this.saveInterval = saveInterval;
        this.variants = variants;
        this.label = label;
        directory = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, folder);
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

    /// <summary>Puts the item at the top of the list ("Zuletzt benutzt"), the oldest beyond <see cref="HistoryLimit"/> drop. Ignored while logged out.</summary>
    public void Touch(GlamourItem item)
    {
        lock (RenderSync.Lock)
            Current?.Touch(WishKey.ForItem(item.Id), DateTimeOffset.Now, HistoryLimit);
    }

    private void OnStoreChanged() => dirty = true;

    private void Flush(IFramework framework) => Flush(retry: false);

    /// <param name="retry">
    /// Save now: also a change within the save interval, and when nothing changed but the last save failed (logout,
    /// unload).
    /// </param>
    /// <param name="announce">Raise <see cref="Changed"/> for a change; not at unload, when the browser is gone.</param>
    private void Flush(bool retry, bool announce = true)
    {
        // The UI changes the list while drawing; the lock keeps saving from reading it halfway.
        lock (RenderSync.Lock)
        {
            if (dirty)
                saveDue = true;
            var save = saveDue && (retry || DateTime.UtcNow - lastSave >= saveInterval) || retry && unsaved;
            if (save && Current != null && path != null)
            {
                try
                {
                    WishlistFile.Save(Current, path);
                    unsaved = false;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    unsaved = true;
                    Plugin.Log.Warning(e, $"Saving {label} file {path} failed");
                }
                lastSave = DateTime.UtcNow;
            }
            if (save)
                saveDue = false;

            if (dirty)
            {
                dirty = false;
                if (announce)
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
                Current = WishlistFile.Load(path, contentId, variants);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // No empty list in its place: its first save would overwrite the real file.
                Plugin.Log.Warning(e, $"Loading {label} file {path} failed");
                path = null;
                LoadFailed = true;
                Changed?.Invoke();
                return;
            }
            Current.Changed += OnStoreChanged;
            Plugin.Log.Information($"{label} loaded for {contentId}: {Current.Entries.Count} entries");
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
        saveDue = false;
        LoadFailed = false;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Flush;
        Flush(retry: true, announce: false);
        Plugin.ClientState.Login -= OnLogin;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
