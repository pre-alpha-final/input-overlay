using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using InputOverlayPS5.Input;

namespace InputOverlayPS5.Overlay;

internal sealed class OverlayRenderer : IDisposable
{
    public const int LogicalWidth = 680, LogicalHeight = 224;
    private readonly LayeredSurface _surface;
    private readonly float _scale;
    private readonly Bitmap _background;
    private readonly Font _label = new("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _small = new("Segoe UI", 8, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font _timer = new("Consolas", 15, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly StringFormat _center = new()
    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private readonly StringFormat _historyLabel = new()
    {
        Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center,
        FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter
    };
    private readonly StringFormat _historyTimer = new()
    { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
    private static readonly Color Accent = Color.FromArgb(250, 69, 222, 237);
    private static readonly Color Foreground = Color.FromArgb(245, 224, 233, 239);

    internal readonly record struct Cap(ControllerButtons Code, string Label, RectangleF Bounds, bool Circle = false);
    internal static readonly Cap[] Buttons =
    [
        new(ControllerButtons.L2, "L2", new(54, 0, 58, 22)),
        new(ControllerButtons.R2, "R2", new(252, 0, 58, 22)),
        new(ControllerButtons.L1, "L1", new(48, 26, 66, 17)),
        new(ControllerButtons.R1, "R1", new(250, 26, 66, 17)),
        new(ControllerButtons.Up, "", new(68, 61, 21, 21)),
        new(ControllerButtons.Right, "", new(92, 85, 21, 21)),
        new(ControllerButtons.Down, "", new(68, 109, 21, 21)),
        new(ControllerButtons.Left, "", new(44, 85, 21, 21)),
        new(ControllerButtons.Triangle, "", new(274, 61, 24, 24), true),
        new(ControllerButtons.Circle, "", new(299, 86, 24, 24), true),
        new(ControllerButtons.Cross, "", new(274, 111, 24, 24), true),
        new(ControllerButtons.Square, "", new(249, 86, 24, 24), true),
        new(ControllerButtons.Create, "", new(112, 55, 10, 22)),
        new(ControllerButtons.Options, "", new(242, 55, 10, 22)),
        new(ControllerButtons.Touchpad, "", new(128, 50, 108, 43)),
        new(ControllerButtons.PS, "PS", new(172, 144, 20, 20), true),
        new(ControllerButtons.Mic, "", new(169, 169, 26, 10))
    ];
    internal static readonly PointF LeftStick = new(133, 135), RightStick = new(231, 135);

    public OverlayRenderer(LayeredSurface surface, float scale)
    {
        _surface = surface;
        _scale = scale;
        _background = new Bitmap(surface.Width, surface.Height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(_background);
        graphics.Clear(Color.Transparent);
        Configure(graphics);
        using GraphicsPath body = ControllerBody();
        using var fill = new LinearGradientBrush(new RectangleF(14, 42, 340, 174),
            Color.FromArgb(180, 41, 51, 62), Color.FromArgb(165, 25, 34, 43), LinearGradientMode.Vertical);
        using var outline = new Pen(Color.FromArgb(200, 111, 127, 140), 1);
        graphics.FillPath(fill, body);
        graphics.DrawPath(outline, body);
        using GraphicsPath center = ControllerCenter();
        using var centerFill = new SolidBrush(Color.FromArgb(220, 12, 17, 23));
        using var centerEdge = new Pen(Color.FromArgb(130, 83, 103, 118), 1);
        graphics.FillPath(centerFill, center);
        graphics.DrawPath(centerEdge, center);
        using var seam = new Pen(Color.FromArgb(80, 108, 123, 136), 0.8f);
        graphics.DrawBezier(seam, 45, 145, 63, 143, 82, 149, 92, 164);
        graphics.DrawBezier(seam, 319, 145, 301, 143, 282, 149, 272, 164);
        using var text = new SolidBrush(Color.FromArgb(200, 181, 195, 205));
        graphics.DrawString("CREATE", _small, text, new RectangleF(97, 42, 40, 12), _center);
        graphics.DrawString("OPTIONS", _small, text, new RectangleF(227, 42, 41, 12), _center);
        // Speaker grille below the touchpad.
        using var dot = new SolidBrush(Color.FromArgb(150, 134, 152, 166));
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < 6; column++) graphics.FillEllipse(dot, 171 + column * 4, 104 + row * 4, 1.4f, 1.4f);
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
        foreach (Cap button in Buttons) DrawCap(graphics, button, snapshot.IsDown(button.Code));
        DrawTrigger(graphics, Buttons[0].Bounds, snapshot.Controls.LeftTrigger, snapshot.IsDown(ControllerButtons.L2));
        DrawTrigger(graphics, Buttons[1].Bounds, snapshot.Controls.RightTrigger, snapshot.IsDown(ControllerButtons.R2));
        DrawStick(graphics, LeftStick, snapshot.Controls.LeftX, snapshot.Controls.LeftY, snapshot.IsDown(ControllerButtons.L3), "L3");
        DrawStick(graphics, RightStick, snapshot.Controls.RightX, snapshot.Controls.RightY, snapshot.IsDown(ControllerButtons.R3), "R3");
        if (!snapshot.Connected)
        {
            using var text = new SolidBrush(Color.FromArgb(225, 181, 195, 205));
            graphics.DrawString("Controller disconnected", _label, text, new RectangleF(95, 187, 174, 18), _center);
        }
        for (int index = 0; index < snapshot.History.Length; index++)
        {
            InputHistoryEntry entry = snapshot.History[index];
            TimerVisual timer = entry.TimerAt(now, snapshot.Frequency);
            float y = index * 22;
            using GraphicsPath row = Rounded(new RectangleF(382, y, 286, 20), 4);
            using var fill = new SolidBrush(Color.FromArgb(150, 23, 29, 35));
            using var label = new SolidBrush(index == 0 ? Color.FromArgb((int)(250 * timer.Opacity), Accent)
                : Color.FromArgb((int)(240 * timer.Opacity), 224, 233, 239));
            using var time = new SolidBrush(Color.FromArgb((int)(240 * timer.Opacity), 232, 240, 244));
            graphics.FillPath(fill, row);
            graphics.DrawString(InputCodes.Label(entry.Code), _label, label, new RectangleF(389, y, 84, 20), _historyLabel);
            string value = timer.Milliseconds == 500 ? "500+ ms" : $"{timer.Milliseconds} ms";
            graphics.DrawString(value, _timer, time, new RectangleF(474, y, 76, 20), _historyTimer);
            string frames = $"{timer.FramesAt60:0.##}{(timer.Milliseconds == 500 ? "+" : "")} f@60";
            graphics.DrawString(frames, _timer, time, new RectangleF(560, y, 102, 20), _historyTimer);
        }
    }

    private void DrawCap(Graphics graphics, Cap cap, bool pressed)
    {
        using GraphicsPath shape = CapShape(cap);
        using var fill = new SolidBrush(pressed ? Accent : Color.FromArgb(215, 23, 29, 35));
        using var border = new Pen(pressed ? Color.FromArgb(255, 139, 242, 250) : Color.FromArgb(200, 108, 123, 136), 1);
        using var text = new SolidBrush(pressed ? Color.FromArgb(255, 8, 30, 35) : Foreground);
        graphics.FillPath(fill, shape);
        graphics.DrawPath(border, shape);
        if (cap.Label.Length != 0)
            graphics.DrawString(cap.Label, cap.Code == ControllerButtons.PS ? _small : _label, text, cap.Bounds, _center);
        DrawSymbol(graphics, cap, pressed);
        if (cap.Code == ControllerButtons.Touchpad)
        {
            using var dot = new SolidBrush(pressed ? Color.FromArgb(70, 8, 30, 35) : Color.FromArgb(55, 180, 199, 210));
            for (int y = 56; y < 89; y += 5)
            for (int x = 135; x < 231; x += 5) graphics.FillEllipse(dot, x, y, 1, 1);
        }
    }

    private static void DrawSymbol(Graphics graphics, Cap cap, bool pressed)
    {
        Color color = pressed ? Color.FromArgb(255, 8, 30, 35) : Foreground;
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float x = cap.Bounds.X + cap.Bounds.Width / 2, y = cap.Bounds.Y + cap.Bounds.Height / 2;
        switch (cap.Code)
        {
            case ControllerButtons.Triangle:
                graphics.DrawPolygon(pen, new PointF[] { new(x, y - 6), new(x + 6, y + 5), new(x - 6, y + 5) }); break;
            case ControllerButtons.Circle: graphics.DrawEllipse(pen, x - 6, y - 6, 12, 12); break;
            case ControllerButtons.Square: graphics.DrawRectangle(pen, x - 5, y - 5, 10, 10); break;
            case ControllerButtons.Cross:
                graphics.DrawLine(pen, x - 5, y - 5, x + 5, y + 5);
                graphics.DrawLine(pen, x + 5, y - 5, x - 5, y + 5); break;
            case ControllerButtons.Mic:
                graphics.DrawArc(pen, x - 1.5f, y - 3, 3, 4, 0, 360);
                graphics.DrawArc(pen, x - 3.5f, y - 3, 7, 6, 0, 180);
                graphics.DrawLine(pen, x, y + 3, x, y + 4);
                break;
            case ControllerButtons.Up or ControllerButtons.Right or ControllerButtons.Down or ControllerButtons.Left:
                PointF[] points = cap.Code switch
                {
                    ControllerButtons.Up => [new(x, y - 4), new(x + 4, y + 3), new(x - 4, y + 3)],
                    ControllerButtons.Right => [new(x + 4, y), new(x - 3, y + 4), new(x - 3, y - 4)],
                    ControllerButtons.Down => [new(x, y + 4), new(x - 4, y - 3), new(x + 4, y - 3)],
                    _ => [new(x - 4, y), new(x + 3, y - 4), new(x + 3, y + 4)]
                };
                using (var brush = new SolidBrush(color)) graphics.FillPolygon(brush, points);
                break;
        }
    }

    private static void DrawTrigger(Graphics graphics, RectangleF bounds, byte pressure, bool pressed)
    {
        using var track = new SolidBrush(Color.FromArgb(130, 108, 123, 136));
        graphics.FillRectangle(track, bounds.X + 5, bounds.Bottom - 5, bounds.Width - 10, 2);
        if (pressure == 0) return;
        using var fill = new SolidBrush(pressed ? Color.FromArgb(255, 8, 30, 35) : Accent);
        graphics.FillRectangle(fill, bounds.X + 5, bounds.Bottom - 5, (bounds.Width - 10) * pressure / 255f, 2);
    }

    private void DrawStick(Graphics graphics, PointF center, byte rawX, byte rawY, bool pressed, string label)
    {
        using var well = new SolidBrush(Color.FromArgb(230, 10, 16, 22));
        using var ring = new Pen(Color.FromArgb(180, 108, 123, 136), 1);
        graphics.FillEllipse(well, center.X - 27, center.Y - 27, 54, 54);
        graphics.DrawEllipse(ring, center.X - 27, center.Y - 27, 54, 54);
        PointF position = new(center.X + ControllerReport.Axis(rawX) * 9, center.Y + ControllerReport.Axis(rawY) * 9);
        bool moved = rawX != 128 || rawY != 128;
        using var stem = new Pen(Accent, 1);
        if (moved) graphics.DrawLine(stem, center, position);
        using var cap = new SolidBrush(pressed ? Accent : Color.FromArgb(245, 33, 43, 53));
        using var outline = new Pen(pressed || moved ? Accent : Color.FromArgb(220, 130, 149, 163), 1.2f);
        graphics.FillEllipse(cap, position.X - 14, position.Y - 14, 28, 28);
        graphics.DrawEllipse(outline, position.X - 14, position.Y - 14, 28, 28);
        using var text = new SolidBrush(pressed ? Color.FromArgb(255, 8, 30, 35) : Foreground);
        graphics.DrawString(label, _small, text, new RectangleF(position.X - 14, position.Y - 14, 28, 28), _center);
    }

    private static GraphicsPath ControllerBody()
    {
        var path = new GraphicsPath();
        path.AddBeziers([
            new PointF(78, 40),
            new(56, 39), new(42, 50), new(35, 73),
            new(21, 115), new(8, 161), new(9, 189),
            new(10, 208), new(20, 216), new(35, 211),
            new(59, 202), new(78, 173), new(94, 158),
            new(106, 164), new(119, 173), new(134, 178),
            new(163, 188), new(201, 188), new(230, 178),
            new(245, 173), new(258, 164), new(270, 158),
            new(286, 173), new(305, 202), new(329, 211),
            new(344, 216), new(354, 208), new(355, 189),
            new(356, 161), new(343, 115), new(329, 73),
            new(322, 50), new(308, 39), new(286, 40),
            new(244, 35), new(120, 35), new(78, 40)
        ]);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath ControllerCenter()
    {
        var path = new GraphicsPath();
        path.AddBezier(126, 44, 143, 42, 221, 42, 238, 44);
        path.AddLine(238, 44, 252, 103);
        path.AddBezier(252, 103, 270, 120, 260, 149, 242, 163);
        path.AddLine(242, 163, 228, 178);
        path.AddBezier(228, 178, 200, 188, 164, 188, 136, 178);
        path.AddLine(136, 178, 122, 163);
        path.AddBezier(122, 163, 104, 149, 94, 120, 112, 103);
        path.AddLine(112, 103, 126, 44);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CapShape(Cap cap)
    {
        if (cap.Circle)
        {
            var circle = new GraphicsPath();
            circle.AddEllipse(cap.Bounds);
            return circle;
        }
        if (cap.Code != ControllerButtons.Touchpad) return Rounded(cap.Bounds, 4);
        // The DualSense touchpad tapers toward the speaker and central trim.
        RectangleF b = cap.Bounds;
        var path = new GraphicsPath();
        path.AddBezier(b.Left + 6, b.Top, b.Left + 2, b.Top, b.Left, b.Top + 2, b.Left + 1, b.Top + 7);
        path.AddLine(b.Left + 1, b.Top + 7, b.Left + 5, b.Bottom - 6);
        path.AddBezier(b.Left + 5, b.Bottom - 6, b.Left + 6, b.Bottom - 1, b.Left + 8, b.Bottom, b.Left + 12, b.Bottom);
        path.AddLine(b.Left + 12, b.Bottom, b.Right - 12, b.Bottom);
        path.AddBezier(b.Right - 12, b.Bottom, b.Right - 8, b.Bottom, b.Right - 6, b.Bottom - 1, b.Right - 5, b.Bottom - 6);
        path.AddLine(b.Right - 5, b.Bottom - 6, b.Right - 1, b.Top + 7);
        path.AddBezier(b.Right - 1, b.Top + 7, b.Right, b.Top + 2, b.Right - 2, b.Top, b.Right - 6, b.Top);
        path.CloseFigure();
        return path;
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
        _small.Dispose();
        _timer.Dispose();
        _center.Dispose();
        _historyLabel.Dispose();
        _historyTimer.Dispose();
    }
}
