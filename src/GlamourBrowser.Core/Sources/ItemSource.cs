namespace GlamourBrowser.Core.Sources;

/// <param name="CostItemId">Currency item of the first cost, 0 when there is none.</param>
public sealed record ItemSource(
    SourceCategory Category,
    string Title,
    string Detail,
    SourceAction? Action = null,
    uint CostItemId = 0,
    uint CostAmount = 0);
