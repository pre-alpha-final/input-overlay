using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using InputOverlayXbox.Input;

namespace InputOverlayXbox.Overlay;

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
        new(ControllerButtons.LT, "LT", new(54, 0, 58, 22)),
        new(ControllerButtons.RT, "RT", new(252, 0, 58, 22)),
        new(ControllerButtons.LB, "LB", new(48, 26, 66, 17)),
        new(ControllerButtons.RB, "RB", new(250, 26, 66, 17)),
        new(ControllerButtons.Up, "", new(118, 104, 21, 21)),
        new(ControllerButtons.Right, "", new(142, 128, 21, 21)),
        new(ControllerButtons.Down, "", new(118, 152, 21, 21)),
        new(ControllerButtons.Left, "", new(94, 128, 21, 21)),
        new(ControllerButtons.Y, "Y", new(274, 53, 24, 24), true),
        new(ControllerButtons.B, "B", new(300, 79, 24, 24), true),
        new(ControllerButtons.A, "A", new(274, 105, 24, 24), true),
        new(ControllerButtons.X, "X", new(248, 79, 24, 24), true),
        new(ControllerButtons.View, "", new(140, 83, 22, 22), true),
        new(ControllerButtons.Menu, "", new(202, 83, 22, 22), true),
        new(ControllerButtons.Xbox, "", new(169, 49, 26, 26), true),
        new(ControllerButtons.Share, "", new(171, 108, 22, 22), true)
    ];
    internal static readonly PointF LeftStick = new(79, 88), RightStick = new(230, 144);

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
        using var seam = new Pen(Color.FromArgb(100, 108, 123, 136), 0.8f);
        graphics.DrawBezier(seam, 34, 133, 45, 142, 64, 152, 78, 172);
        graphics.DrawBezier(seam, 330, 133, 319, 142, 300, 152, 286, 172);
        // Faceted hybrid D-pad dish on the Series X|S controller.
        PointF[] dish = Enumerable.Range(0, 8).Select(index =>
        {
            double angle = (index * 45 + 22.5) * Math.PI / 180;
            return new PointF(128.5f + (float)Math.Cos(angle) * 35, 138.5f + (float)Math.Sin(angle) * 35);
        }).ToArray();
        using var dishFill = new SolidBrush(Color.FromArgb(230, 12, 18, 24));
        using var dishEdge = new Pen(Color.FromArgb(190, 108, 123, 136), 1);
        graphics.FillPolygon(dishFill, dish);
        graphics.DrawPolygon(dishEdge, dish);
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
        DrawTrigger(graphics, Buttons[0].Bounds, snapshot.Controls.LeftTrigger, snapshot.IsDown(ControllerButtons.LT));
        DrawTrigger(graphics, Buttons[1].Bounds, snapshot.Controls.RightTrigger, snapshot.IsDown(ControllerButtons.RT));
        DrawStick(graphics, LeftStick, snapshot.Controls.LeftX, snapshot.Controls.LeftY, snapshot.IsDown(ControllerButtons.LS), "LS");
        DrawStick(graphics, RightStick, snapshot.Controls.RightX, snapshot.Controls.RightY, snapshot.IsDown(ControllerButtons.RS), "RS");
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
            string frames = $"~{timer.EstimatedFramesAt60:0.##}{(timer.Milliseconds == 500 ? "+" : "")} f@60";
            graphics.DrawString(frames, _timer, time, new RectangleF(560, y, 102, 20), _historyTimer);
        }
    }

    private void DrawCap(Graphics graphics, Cap cap, bool pressed)
    {
        using GraphicsPath shape = CapShape(cap);
        using var fill = new SolidBrush(pressed ? Accent : Color.FromArgb(215, 23, 29, 35));
        using var border = new Pen(pressed ? Color.FromArgb(255, 139, 242, 250) : Color.FromArgb(200, 108, 123, 136), 1);
        graphics.FillPath(fill, shape);
        graphics.DrawPath(border, shape);
        if (cap.Label.Length != 0)
        {
            Color labelColor = pressed ? Color.FromArgb(255, 8, 30, 35) : cap.Code switch
            {
                ControllerButtons.A => Color.FromArgb(255, 135, 220, 111),
                ControllerButtons.B => Color.FromArgb(255, 246, 122, 119),
                ControllerButtons.X => Color.FromArgb(255, 114, 185, 247),
                ControllerButtons.Y => Color.FromArgb(255, 247, 211, 105),
                _ => Foreground
            };
            using var label = new SolidBrush(labelColor);
            graphics.DrawString(cap.Label, _label, label, cap.Bounds, _center);
        }
        DrawSymbol(graphics, cap, pressed);
    }

    private static void DrawSymbol(Graphics graphics, Cap cap, bool pressed)
    {
        Color color = pressed ? Color.FromArgb(255, 8, 30, 35) : Foreground;
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float x = cap.Bounds.X + cap.Bounds.Width / 2, y = cap.Bounds.Y + cap.Bounds.Height / 2;
        switch (cap.Code)
        {
            case ControllerButtons.View:
                graphics.DrawRectangle(pen, x - 5, y - 4, 7, 6);
                graphics.DrawRectangle(pen, x - 1, y - 1, 7, 6);
                break;
            case ControllerButtons.Menu:
                for (int row = -1; row <= 1; row++) graphics.DrawLine(pen, x - 5, y + row * 4, x + 5, y + row * 4);
                break;
            case ControllerButtons.Share:
                graphics.DrawLine(pen, x, y - 6, x, y + 2);
                graphics.DrawLine(pen, x, y - 6, x - 3, y - 3);
                graphics.DrawLine(pen, x, y - 6, x + 3, y - 3);
                graphics.DrawLines(pen, new PointF[] { new(x - 5, y), new(x - 5, y + 5), new(x + 5, y + 5), new(x + 5, y) });
                break;
            case ControllerButtons.Xbox:
                graphics.DrawBezier(pen, x - 7, y - 7, x - 2, y - 9, x + 3, y + 1, x + 7, y + 7);
                graphics.DrawBezier(pen, x + 7, y - 7, x + 2, y - 9, x - 3, y + 1, x - 7, y + 7);
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
            new(54, 39), new(36, 52), new(28, 80),
            new(15, 120), new(6, 166), new(9, 194),
            new(11, 210), new(22, 218), new(38, 209),
            new(58, 198), new(75, 173), new(94, 161),
            new(114, 171), new(140, 179), new(164, 179),
            new(176, 179), new(188, 179), new(200, 179),
            new(224, 179), new(250, 171), new(270, 161),
            new(289, 173), new(306, 198), new(326, 209),
            new(342, 218), new(353, 210), new(355, 194),
            new(358, 166), new(349, 120), new(336, 80),
            new(328, 52), new(310, 39), new(286, 40),
            new(242, 34), new(122, 34), new(78, 40)
        ]);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CapShape(Cap cap)
    {
        if (!cap.Circle) return Rounded(cap.Bounds, 4);
        var circle = new GraphicsPath();
        circle.AddEllipse(cap.Bounds);
        return circle;
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
