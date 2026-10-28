using System.Drawing.Imaging;
using InputOverlayMnK.Input;
using InputOverlayMnK.Overlay;

namespace InputOverlayMnK.Tests;

public sealed class RendererTests
{
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    [InlineData(3f)]
    public void SurfaceRetainsTransparencyAndRendersActiveInputs(float scale)
    {
        using var surface = new LayeredSurface((int)(OverlayRenderer.LogicalWidth * scale),
            (int)(OverlayRenderer.LogicalHeight * scale));
        using var renderer = new OverlayRenderer(surface, scale);
        var state = new InputState(1000);
        renderer.Draw(state.Snapshot, 0);
        Assert.Equal(0, surface.Bitmap.GetPixel(0, 0).A);
        Color released = surface.Bitmap.GetPixel((int)(62 * scale), (int)(85 * scale));
        state.Keyboard(1, 0x10, 0, 0); // Q
        renderer.Draw(state.Snapshot, 0);
        Color pressed = surface.Bitmap.GetPixel((int)(62 * scale), (int)(85 * scale));
        Assert.True(pressed.G > released.G + 80);
        Assert.True(pressed.B > released.B + 80);
        Assert.Equal(0, surface.Bitmap.GetPixel(0, 0).A);
    }

    [Fact]
    public void VisualKeyboardMatchesAllVisiblePhysicalKeysExactlyOnce()
    {
        int[] keys = OverlayRenderer.Keys.Select(key => key.Code).Order().ToArray();
        Assert.Equal(InputCodes.Visible.Where(code => code < InputCodes.MouseLeft).Order(), keys);
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    [InlineData(3f)]
    public void AllTenHistoryRowsRemainVisibleAfterTimersCompleteAndResetClearsThem(float scale)
    {
        using var surface = new LayeredSurface((int)(OverlayRenderer.LogicalWidth * scale),
            (int)(OverlayRenderer.LogicalHeight * scale));
        using var renderer = new OverlayRenderer(surface, scale);
        var state = new InputState(1000);
        for (int index = 0; index < 12; index++) state.Mouse(1, 0x0400, 120, index);
        renderer.Draw(state.Snapshot, 1000);
        surface.Graphics.Flush();
        for (int index = 0; index < 10; index++)
        {
            int x = (int)(400 * scale), y = (int)((index * 22 + 14) * scale);
            Assert.True(surface.Bitmap.GetPixel(x, y).A > 0, $"History row {index} is missing at scale {scale}.");
        }
        Assert.Equal(0, surface.Bitmap.GetPixel(surface.Width - 1, surface.Height - 1).A);
        state.Reset(1001);
        renderer.Draw(state.Snapshot, 1001);
        surface.Graphics.Flush();
        Assert.Equal(0, surface.Bitmap.GetPixel((int)(400 * scale), (int)(14 * scale)).A);
    }

    [Fact]
    public void EarlierRowKeepsItsOwnTimeAndFramesWhenANewInputIsAdded()
    {
        using var surface = new LayeredSurface(OverlayRenderer.LogicalWidth, OverlayRenderer.LogicalHeight);
        using var renderer = new OverlayRenderer(surface, 1);
        var state = new InputState(1000);
        state.Keyboard(1, 0x21, 0, 0);
        renderer.Draw(state.Snapshot, 20);
        surface.Graphics.Flush();
        using var before = (Bitmap)surface.Bitmap.Clone();
        state.Mouse(2, 1, 0, 20);
        renderer.Draw(state.Snapshot, 20);
        surface.Graphics.Flush();
        bool newestDiffers = false;
        for (int y = 4; y < 24; y++)
        for (int x = 478; x < 666; x++)
        {
            int previous = before.GetPixel(x, y).ToArgb();
            Assert.Equal(previous, surface.Bitmap.GetPixel(x, y + 22).ToArgb());
            newestDiffers |= previous != surface.Bitmap.GetPixel(x, y).ToArgb();
        }
        Assert.True(newestDiffers, "The new row should read 0 ms and ~1 f@60 while the previous row reads 20 ms and ~2.2 f@60.");
    }

    [Fact]
    public void WritesNeutralAndActiveFramesForVisualReview()
    {
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/visual-review"));
        Directory.CreateDirectory(directory);
        using var surface = new LayeredSurface(OverlayRenderer.LogicalWidth * 2, OverlayRenderer.LogicalHeight * 2);
        using var renderer = new OverlayRenderer(surface, 2);
        var state = new InputState(1000);
        renderer.Draw(state.Snapshot, 0);
        surface.Graphics.Flush();
        surface.Bitmap.Save(Path.Combine(directory, "overlay-idle.png"), ImageFormat.Png);
        state.Keyboard(1, 0x11, 0, 100);
        state.Keyboard(1, 0x21, 0, 180);
        state.Keyboard(1, 0x1d, 0, 260);
        state.Mouse(2, 0x0040, 0, 340);
        state.Mouse(2, 0x0001, 0, 420);
        state.Mouse(2, 0x0400, 120, 500);
        state.Keyboard(1, 0x39, 0, 580);
        state.Keyboard(1, 0x1d, 2, 660);
        state.Mouse(2, 0x0004, 0, 740);
        state.Mouse(2, 0x0400, -120, 820);
        renderer.Draw(state.Snapshot, 858);
        surface.Graphics.Flush();
        surface.Bitmap.Save(Path.Combine(directory, "overlay-active.png"), ImageFormat.Png);
    }
}
