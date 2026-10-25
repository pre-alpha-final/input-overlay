using System.Drawing.Imaging;
using InputOverlayPS5.Input;
using InputOverlayPS5.Overlay;

namespace InputOverlayPS5.Tests;

public sealed class RendererTests
{
    [Fact]
    public void EveryStandardButtonHasOneLiveVisual()
    {
        var buttons = OverlayRenderer.Buttons.Select(button => button.Code)
            .Append(ControllerButtons.L3).Append(ControllerButtons.R3).Order().ToArray();
        Assert.Equal(InputCodes.All.Order(), buttons);
        Assert.Equal(19, buttons.Distinct().Count());
    }

    [Theory]
    [InlineData(0.5f)] [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)] [InlineData(3f)]
    public void SurfaceIsTransparentAndButtonsAndStickClicksHighlight(float scale)
    {
        using var surface = new LayeredSurface((int)(680 * scale), (int)(224 * scale));
        using var renderer = new OverlayRenderer(surface, scale);
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral, 0);
        renderer.Draw(state.Snapshot, 0);
        surface.Graphics.Flush();
        Color released = Pixel(surface, scale, 283, 130);
        Color stick = Pixel(surface, scale, 129, 132);
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross | ControllerButtons.L3 }, 10);
        renderer.Draw(state.Snapshot, 10);
        surface.Graphics.Flush();
        Assert.True(Pixel(surface, scale, 283, 130).G > released.G + 80);
        Assert.True(Pixel(surface, scale, 129, 132).G > stick.G + 80);
        Assert.Equal(0, surface.Bitmap.GetPixel(0, 0).A);
        Assert.Equal(0, surface.Bitmap.GetPixel(surface.Width - 1, surface.Height - 1).A);
    }

    [Fact]
    public void SticksMoveAndTriggersFillWithoutCreatingHistory()
    {
        using var surface = new LayeredSurface(1360, 448);
        using var renderer = new OverlayRenderer(surface, 2);
        var state = new InputState(1000);
        state.Apply(ControllerReport.Neutral, 0);
        renderer.Draw(state.Snapshot, 0);
        surface.Graphics.Flush();
        using var neutral = (Bitmap)surface.Bitmap.Clone();
        state.Apply(ControllerReport.Neutral with { LeftX = 255, LeftY = 0, RightX = 0, RightY = 255,
            LeftTrigger = 255, RightTrigger = 128 }, 10);
        renderer.Draw(state.Snapshot, 10);
        surface.Graphics.Flush();
        Assert.NotEqual(neutral.GetPixel(276, 246), surface.Bitmap.GetPixel(276, 246));
        Assert.True(Pixel(surface, 2, 85, 21).G > neutral.GetPixel(170, 42).G + 50);
        Assert.Empty(state.Snapshot.History);
    }

    [Theory]
    [InlineData(0.5f)] [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)] [InlineData(3f)]
    public void TenRowsStayVisibleAfterTimersFinishAndResetClearsThem(float scale)
    {
        using var surface = new LayeredSurface((int)(680 * scale), (int)(224 * scale));
        using var renderer = new OverlayRenderer(surface, scale);
        var state = new InputState(1000);
        for (int i = 0; i < 12; i++)
        {
            state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross }, i * 2);
            state.Apply(ControllerReport.Neutral, i * 2 + 1);
        }
        renderer.Draw(state.Snapshot, 1000);
        surface.Graphics.Flush();
        for (int i = 0; i < 10; i++) Assert.True(Pixel(surface, scale, 400, i * 22 + 14).A > 0);
        state.Reset(1001);
        renderer.Draw(state.Snapshot, 1001);
        surface.Graphics.Flush();
        Assert.Equal(0, Pixel(surface, scale, 400, 14).A);
    }

    [Fact]
    public void WritesFramesForVisualReview()
    {
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/visual-review-ps5"));
        Directory.CreateDirectory(directory);
        using var surface = new LayeredSurface(1360, 448);
        using var renderer = new OverlayRenderer(surface, 2);
        var state = new InputState(1000);
        Save("overlay-disconnected.png", 0);
        state.Apply(ControllerReport.Neutral, 0);
        Save("overlay-idle.png", 0);
        ControllerButtons[] inputs = [ControllerButtons.Cross, ControllerButtons.Up, ControllerButtons.R1,
            ControllerButtons.L2, ControllerButtons.Touchpad, ControllerButtons.Mic, ControllerButtons.Create,
            ControllerButtons.PS, ControllerButtons.L3, ControllerButtons.R2];
        for (int i = 0; i < inputs.Length; i++)
        {
            state.Apply(ControllerReport.Neutral, 100 + i * 80);
            state.Apply(ControllerReport.Neutral with { Buttons = inputs[i] }, 100 + i * 80);
        }
        state.Apply(ControllerReport.Neutral with { Buttons = ControllerButtons.Cross | ControllerButtons.Up
            | ControllerButtons.Right | ControllerButtons.L1 | ControllerButtons.R2 | ControllerButtons.L3 | ControllerButtons.Mic,
            LeftX = 230, LeftY = 20, RightX = 60, RightY = 215, LeftTrigger = 80, RightTrigger = 230 }, 850);
        Save("overlay-active.png", 888);
        void Save(string name, long now)
        {
            renderer.Draw(state.Snapshot, now);
            surface.Graphics.Flush();
            surface.Bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
        }
    }

    private static Color Pixel(LayeredSurface surface, float scale, float x, float y) =>
        surface.Bitmap.GetPixel((int)(x * scale), (int)(y * scale));
}
