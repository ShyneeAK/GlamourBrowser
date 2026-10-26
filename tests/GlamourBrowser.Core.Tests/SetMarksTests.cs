using GlamourBrowser.Core.CustomSets;

namespace GlamourBrowser.Core.Tests;

public class SetMarksTests
{
    private static readonly IReadOnlyList<uint> Shown = [5, 3, 8, 1, 7];

    [Fact]
    public void Toggle_MarksAndTakesTheMarkOff()
    {
        var marks = new SetMarks();
        marks.Toggle(3);
        marks.Toggle(8);
        marks.Toggle(3);

        Assert.Equal([8u], marks.Marked);
    }

    [Fact]
    public void MarkRange_FromTheLastClickedOneInTheShownOrder()
    {
        var marks = new SetMarks();
        marks.Toggle(1);
        marks.MarkRange(Shown, 3);

        Assert.Equal([1u, 3u, 8u], marks.Marked.Order());
    }

    [Fact]
    public void MarkRange_WithoutALastOneOnlyTheSet()
    {
        var marks = new SetMarks();
        marks.MarkRange(Shown, 7);

        Assert.Equal([7u], marks.Marked);
    }

    [Fact]
    public void With_EveryMarkedOneAndTheSetItself()
    {
        var marks = new SetMarks();
        Assert.Equal([1u], marks.With(1));

        marks.Toggle(5);
        marks.Toggle(8);
        Assert.Equal([5u, 8u], marks.With(8).Order());
        Assert.Equal([1u, 5u, 8u], marks.With(1).Order());
    }
}
