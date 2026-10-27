using System;
using System.Collections.Generic;
using System.Linq;
using GlamourBrowser.Core.CustomSets;
using GlamourBrowser.Core.Items;
using GlamourBrowser.Core.Rendering;
using GlamourBrowser.Rendering;

namespace GlamourBrowser.Browser;

/// <summary>
/// Keeps the live try-on (<see cref="Core.CustomSets.TryOn"/>) on its basis: the worn outfit or nothing, after the tile
/// mode. A changed worn outfit moves the basis under the clicks; login and logout start afresh. Not saved.
/// </summary>
public sealed class TryOnService : IDisposable
{
    private readonly AppearanceTracker appearance;
    private readonly Configuration config;

    /// <param name="blocksOf">The slots an item blocks besides its own (<see cref="GlamourItem.Blocks"/>).</param>
    public TryOnService(AppearanceTracker appearance, Configuration config, Func<uint, ushort> blocksOf)
    {
        this.appearance = appearance;
        this.config = config;
        TryOn = new TryOn(Basis(), blocksOf) { Keep = config.TryOnKeep };
        appearance.Changed += OnAppearanceChanged;
        Plugin.ClientState.Login += Reset;
        Plugin.ClientState.Logout += OnLogout;
    }

    public TryOn TryOn { get; }

    /// <summary>"Aktuelles Outfit": the worn items with their dyes; "Unterwäsche": nothing.</summary>
    public IReadOnlyList<CustomPiece> Basis()
        => config.TileMode == TileMode.CurrentOutfit
            ? appearance.Outfit.Entries.Select(e => new CustomPiece(e.Slot, e.ItemId, e.Stain0, e.Stain1)).ToList()
            : [];

    /// <summary>The basis of the current tile mode under the clicked pieces, which stay (everything on or off).</summary>
    public void Rebase()
    {
        lock (RenderSync.Lock)
            TryOn.SetBasis(Basis());
    }

    /// <summary>"Anprobierte Teile behalten" on or off (off leaves only the last click on); kept in the configuration.</summary>
    public void SetKeep(bool keep)
    {
        lock (RenderSync.Lock)
            TryOn.SetKeep(keep);
        config.TryOnKeep = keep;
        config.Save();
    }

    /// <summary>Back to the basis of the current tile mode.</summary>
    public void Reset()
    {
        lock (RenderSync.Lock)
            TryOn.Reset(Basis());
    }

    /// <summary>Puts a clicked item or set on, dyed like its tile; a ring on the hand in <paramref name="ring"/> when given.</summary>
    public void Put(TileSubject subject, DyePair dye, EquipSlot? ring = null)
    {
        lock (RenderSync.Lock)
        {
            switch (subject)
            {
                case ItemSubject { Item: var item }:
                    TryOn.Put(item, dye, ring);
                    break;
                case SetSubject set:
                    TryOn.PutAll(set, dye);
                    break;
            }
        }
    }

    private void OnAppearanceChanged()
    {
        lock (RenderSync.Lock)
            TryOn.SetBasis(Basis());
    }

    /// <summary>Takes back the last change of the try-on (<see cref="TryOn.Undo"/>).</summary>
    public void Undo()
    {
        lock (RenderSync.Lock)
            TryOn.Undo();
    }

    /// <summary>Does the last undone change of the try-on again (<see cref="TryOn.Redo"/>).</summary>
    public void Redo()
    {
        lock (RenderSync.Lock)
            TryOn.Redo();
    }

    // The next character starts without the changes of this one.
    private void OnLogout(int type, int code)
    {
        Reset();
        lock (RenderSync.Lock)
            TryOn.ClearHistory();
    }

    public void Dispose()
    {
        appearance.Changed -= OnAppearanceChanged;
        Plugin.ClientState.Login -= Reset;
        Plugin.ClientState.Logout -= OnLogout;
    }
}
