namespace GlamourBrowser.Core.Sources;

/// <summary>The tile order; new values go at the end, the configuration stores the number.</summary>
public enum SortOrder
{
    /// <summary>Highest level first.</summary>
    Level,
    /// <summary>Newest patch first, unknown patches last.</summary>
    Newest,
    LevelAscending,
    /// <summary>Oldest patch first, unknown patches last.</summary>
    Oldest,
    NameAscending,
    NameDescending,
}
