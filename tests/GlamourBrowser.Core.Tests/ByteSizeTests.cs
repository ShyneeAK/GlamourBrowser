using GlamourBrowser.Core.Ui;

namespace GlamourBrowser.Core.Tests;

public class ByteSizeTests
{
    private const long MiB = 1024 * 1024;

    [Theory]
    [InlineData(0, "0 MB")]
    [InlineData(1, "< 1 MB")]
    [InlineData(38 * MiB, "38 MB")]
    [InlineData(38 * MiB + MiB / 2, "39 MB")]
    [InlineData(1023 * MiB, "1023 MB")]
    [InlineData(1024 * MiB, "1,0 GB")]
    [InlineData(3174 * MiB, "3,1 GB")]
    public void Format_MegabytesBelowOneGigabyteElseGigabytesWithADecimalComma(long bytes, string expected)
        => Assert.Equal(expected, ByteSize.Format(bytes));
}
