using System.Globalization;

namespace GlamourBrowser.Core.Ui;

/// <summary>Memory sizes for the debug line: whole megabytes below one gigabyte, else gigabytes with one decimal (German comma).</summary>
public static class ByteSize
{
    private const long MiB = 1024 * 1024;
    private const long GiB = 1024 * MiB;

    public static string Format(long bytes)
    {
        if (bytes >= GiB)
            return ((double)bytes / GiB).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " GB";
        if (bytes is > 0 and < MiB)
            return "< 1 MB";
        return $"{Math.Round((double)bytes / MiB, MidpointRounding.AwayFromZero):0} MB";
    }
}
