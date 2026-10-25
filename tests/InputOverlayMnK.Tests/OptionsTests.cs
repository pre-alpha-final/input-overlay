namespace InputOverlayMnK.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void DefaultsMatchTheOverlaySpecification() => Assert.Equal(new Options(null, 1, 24), Options.Parse([]));

    [Fact]
    public void ParsesAllPlacementOptions() => Assert.Equal(new Options(2, 1.25f, 40),
        Options.Parse(["--monitor", "2", "--scale", "1.25", "--bottom-margin", "40"]));

    [Theory]
    [InlineData("--monitor", "0")]
    [InlineData("--monitor", "x")]
    [InlineData("--scale", "NaN")]
    [InlineData("--scale", "Infinity")]
    [InlineData("--scale", "0.49")]
    [InlineData("--scale", "3.1")]
    [InlineData("--bottom-margin", "-1")]
    [InlineData("--bottom-margin", "1001")]
    public void RejectsInvalidValues(string option, string value) =>
        Assert.Throws<ArgumentException>(() => Options.Parse([option, value]));

    [Fact]
    public void RejectsUnknownAndMissingArguments()
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(["--unknown"]));
        Assert.Throws<ArgumentException>(() => Options.Parse(["--scale"]));
    }
}
