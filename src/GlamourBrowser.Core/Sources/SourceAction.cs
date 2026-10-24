namespace GlamourBrowser.Core.Sources;

/// <summary>Something the item card can do for a source.</summary>
public abstract record SourceAction;

/// <summary>Map flag in map coordinates (as shown in game, e.g. 11.7 / 13.4).</summary>
public sealed record MapFlag(uint TerritoryId, uint MapId, float X, float Y) : SourceAction;

public sealed record DutyFinder(uint ContentFinderConditionId) : SourceAction;

public sealed record OpenRecipe(uint RecipeId) : SourceAction;
