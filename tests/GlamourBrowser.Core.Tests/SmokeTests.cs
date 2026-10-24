namespace GlamourBrowser.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads() => Assert.Equal("GlamourBrowser.Core", typeof(GlamourBrowser.Core.AssemblyMarker).Assembly.GetName().Name);
}
