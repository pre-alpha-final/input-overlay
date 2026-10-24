using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using InputOverlay.Input;

namespace InputOverlay.Overlay;

internal sealed class OverlayRenderer : IDisposable
{
    public const int LogicalWidth = 476, LogicalHeight = 224;
    private readonly LayeredSurface _surface;
    private readonly float _scale;
    private readonly Bitmap _background;
    private readonly Font _label = new("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _timer = new("Consolas", 15, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly StringFormat _center = new()
    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private static readonly Color Accent = Color.FromArgb(250, 69, 222, 237);

    internal readonly record struct Cap(int Code, string Label, RectangleF Bounds);
    internal static readonly Cap[] Keys = CreateKeys();
    private static readonly Cap[] Mouse =
    [
        new(InputCodes.MouseLeft, "L", new(295, 88, 23, 48)),
        new(InputCodes.MouseRight, "R", new(332, 91, 23, 49)),
        new(InputCodes.MouseMiddle, "", new(319, 96, 11, 25)),
        new(InputCodes.MouseBack, "4", new(291, 144, 10, 23)),
        new(InputCodes.MouseForward, "5", new(291, 116, 10, 23))
    ];

    public OverlayRenderer(LayeredSurface surface, float scale)
    {
        _surface = surface;
        _scale = scale;
        _background = new Bitmap(surface.Width, surface.Height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(_background);
        graphics.Clear(Color.Transparent);
        Configure(graphics);
        foreach (Cap key in Keys) DrawCap(graphics, key, false);
        using GraphicsPath body = MouseBody();
        using var fill = new LinearGradientBrush(new RectangleF(282, 76, 86, 140),
            Color.FromArgb(180, 28, 37, 45), Color.FromArgb(155, 15, 21, 27), LinearGradientMode.Vertical);
        using var outline = new Pen(Color.FromArgb(200, 111, 127, 140), 1);
        graphics.FillPath(fill, body);
        graphics.DrawPath(outline, body);
        using var seam = new GraphicsPath();
        seam.AddBezier(303, 142, 318, 143, 336, 147, 352, 145);
        using var seamPen = new Pen(Color.FromArgb(85, 108, 123, 136), 0.8f);
        graphics.DrawPath(seamPen, seam);
        foreach (Cap button in Mouse)
            if (!IsSideButton(button.Code)) DrawCap(graphics, button, false);
        DrawScroll(graphics, true, false);
        DrawScroll(graphics, false, false);
    }

    private static Cap[] CreateKeys()
    {
        List<Cap> keys = [new(0x01, "Esc", new(0, 0, 32, 32))];
        string[] numbers = ["`", "1", "2", "3", "4", "5"];
        int[] numberCodes = [0x29, 0x02, 0x03, 0x04, 0x05, 0x06];
        for (int i = 0; i < numbers.Length; i++) keys.Add(new(numberCodes[i], numbers[i], new(i * 36, 40, 32, 32)));
        keys.Add(new(0x0f, "Tab", new(0, 76, 48, 32)));
        AddRow(keys, "QWERT", [0x10, 0x11, 0x12, 0x13, 0x14], 52, 76);
        keys.Add(new(0x3a, "Caps", new(0, 112, 56, 32)));
        AddRow(keys, "ASDFG", [0x1e, 0x1f, 0x20, 0x21, 0x22], 60, 112);
        keys.Add(new(0x2a, "Shift", new(0, 148, 72, 32)));
        AddRow(keys, "ZXCVB", [0x2c, 0x2d, 0x2e, 0x2f, 0x30], 76, 148);
        keys.AddRange([
            new(0x1d, "Ctrl", new(0, 184, 40, 32)),
            new(0x15b, "Win", new(44, 184, 40, 32)),
            new(0x38, "Alt", new(88, 184, 40, 32)),
            new(0x39, "Space", new(132, 184, 120, 32))
        ]);
        return keys.ToArray();
    }

    private static void AddRow(List<Cap> keys, string labels, int[] codes, float x, float y)
    {
        for (int i = 0; i < labels.Length; i++) keys.Add(new(codes[i], labels[i].ToString(), new(x + i * 36, y, 32, 32)));
    }

    private void Configure(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.ScaleTransform(_scale, _scale);
        graphics.TranslateTransform(4, 4);
    }

    public void Draw(InputSnapshot snapshot, long now)
    {
        Graphics graphics = _surface.Graphics;
        graphics.ResetTransform();
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImageUnscaled(_background, 0, 0);
        graphics.CompositingMode = CompositingMode.SourceOver;
        Configure(graphics);
        foreach (Cap key in Keys)
            if (snapshot.IsDown(key.Code)) DrawCap(graphics, key, true);
        foreach (Cap button in Mouse)
            if (!IsSideButton(button.Code) && snapshot.IsDown(button.Code)) DrawCap(graphics, button, true);
        // Keep the entire matched pair in the same foreground layer, including released buttons.
        foreach (Cap button in Mouse)
            if (IsSideButton(button.Code)) DrawCap(graphics, button, snapshot.IsDown(button.Code));
        if (snapshot.ScrollUpAt(now)) DrawScroll(graphics, true, true);
        if (snapshot.ScrollDownAt(now)) DrawScroll(graphics, false, true);
        TimerVisual timer = snapshot.TimerAt(now);
        if (timer.Visible)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(240 * timer.Opacity), 232, 240, 244));
            graphics.DrawString($"{timer.Milliseconds,3} ms", _timer, brush, new RectangleF(382, 184, 86, 32), _center);
        }
    }

    private void DrawCap(Graphics graphics, Cap cap, bool pressed)
    {
        using GraphicsPath path = CapShape(cap);
        using var fill = new SolidBrush(pressed ? Accent : Color.FromArgb(185, 23, 29, 35));
        using var border = new Pen(pressed ? Color.FromArgb(255, 139, 242, 250) : Color.FromArgb(200, 108, 123, 136), 1);
        using var text = new SolidBrush(pressed ? Color.FromArgb(255, 8, 30, 35) : Color.FromArgb(245, 224, 233, 239));
        CompositingMode compositing = graphics.CompositingMode;
        // Erase the shell beneath a side button so its outline cannot show through the face.
        if (IsSideButton(cap.Code)) graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.FillPath(fill, path);
        graphics.CompositingMode = compositing;
        graphics.DrawPath(border, path);
        if (cap.Label.Length != 0) graphics.DrawString(cap.Label, _label, text, cap.Bounds, _center);
        if (cap.Code == InputCodes.MouseMiddle)
        {
            using var rib = new Pen(pressed ? Color.FromArgb(255, 8, 30, 35) : Color.FromArgb(210, 150, 168, 180));
            for (int i = 1; i <= 3; i++) graphics.DrawLine(rib, cap.Bounds.Left + 3, cap.Bounds.Top + i * 6,
                cap.Bounds.Right - 3, cap.Bounds.Top + i * 6);
        }
    }

    private static GraphicsPath MouseBody()
    {
        // The left thumb recess is deeper than the right flank; the right palm bulge
        // sits higher, and the heel curls back left of the button axis.
        var path = new GraphicsPath();
        path.AddBeziers([
            new PointF(324.5f, 76),
            new(312, 76), new(299, 80), new(294, 88),
            new(291, 94), new(291.5f, 107), new(293, 118),
            new(294.5f, 129), new(298, 135), new(298, 145),
            new(298, 155), new(291, 165), new(288, 177),
            new(285, 189), new(289, 202), new(298, 209),
            new(304, 214), new(314, 216), new(322, 216),
            new(332, 216), new(342, 211), new(349, 201),
            new(357, 190), new(362, 175), new(361, 157),
            new(360, 139), new(359, 117), new(358, 100),
            new(357.5f, 92), new(354, 85), new(349, 82),
            new(342, 77), new(333, 76), new(324.5f, 76)
        ]);
        path.CloseFigure();
        return path;
    }

    private static bool IsSideButton(int code) => code is InputCodes.MouseBack or InputCodes.MouseForward;

    private static GraphicsPath CapShape(Cap cap)
    {
        if (cap.Code is InputCodes.MouseLeft or InputCodes.MouseRight)
        {
            var button = new GraphicsPath();
            if (cap.Code == InputCodes.MouseLeft)
            {
                button.AddBeziers([
                    new PointF(320, 79),
                    new(309, 79), new(300, 83), new(296, 91),
                    new(292, 98), new(294, 111), new(296, 122),
                    new(298, 130), new(299, 135), new(303, 137),
                    new(309, 139), new(315, 139), new(320, 139)
                ]);
                button.AddLine(320, 139, 320, 124);
                button.AddBezier(320, 124, 318, 124, 317, 122, 317, 118);
                button.AddLine(317, 118, 317, 97);
                button.AddBezier(317, 97, 317, 94, 318, 92, 320, 92);
            }
            else
            {
                button.AddBeziers([
                    new PointF(329, 80),
                    new(341, 80), new(351, 86), new(354, 95),
                    new(358, 103), new(356, 116), new(355, 126),
                    new(355, 133), new(355, 138), new(352, 141),
                    new(345, 143), new(337, 143), new(330, 142)
                ]);
                button.AddLine(330, 142, 329, 124);
                button.AddBezier(329, 124, 331, 124, 332, 122, 332, 118);
                button.AddLine(332, 118, 332, 97);
                button.AddBezier(332, 97, 332, 94, 331, 92, 329, 92);
            }
            button.CloseFigure();
            return button;
        }

        return Rounded(cap.Bounds, cap.Code == InputCodes.MouseMiddle || IsSideButton(cap.Code)
            ? cap.Bounds.Width / 2 : Math.Min(5, cap.Bounds.Width / 3));
    }

    private static void DrawScroll(Graphics graphics, bool up, bool active)
    {
        float y = up ? 84 : 129;
        PointF[] points = up ? [new(321, y + 3), new(324.5f, y), new(328, y + 3)]
            : [new(321, y - 3), new(324.5f, y), new(328, y - 3)];
        using var pen = new Pen(active ? Accent : Color.FromArgb(180, 128, 146, 160), active ? 2 : 1.2f)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawLines(pen, points);
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _background.Dispose();
        _label.Dispose();
        _timer.Dispose();
        _center.Dispose();
    }
}
