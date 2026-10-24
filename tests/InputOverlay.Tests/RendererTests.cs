using System.Drawing.Imaging;
using InputOverlay.Input;
using InputOverlay.Overlay;

namespace InputOverlay.Tests;

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
        state.Keyboard(1, 0x21, 0, 100);
        state.Keyboard(1, 0x1d, 0, 100);
        state.Mouse(2, 0x0441, 120, 100);
        renderer.Draw(state.Snapshot, 138);
        surface.Graphics.Flush();
        surface.Bitmap.Save(Path.Combine(directory, "overlay-active.png"), ImageFormat.Png);
    }
}
