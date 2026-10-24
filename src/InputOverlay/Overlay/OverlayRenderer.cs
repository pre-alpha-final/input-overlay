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
        new(InputCodes.MouseLeft, "L", new(288, 84, 27, 42)),
        new(InputCodes.MouseRight, "R", new(334, 84, 27, 42)),
        new(InputCodes.MouseMiddle, "", new(318, 94, 13, 25)),
        new(InputCodes.MouseBack, "4", new(271, 156, 12, 26)),
        new(InputCodes.MouseForward, "5", new(271, 124, 12, 26))
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
        using GraphicsPath body = Rounded(new RectangleF(284, 76, 81, 136), 35);
        using var fill = new SolidBrush(Color.FromArgb(135, 17, 23, 29));
        using var outline = new Pen(Color.FromArgb(190, 111, 127, 140), 1);
        graphics.FillPath(fill, body);
        graphics.DrawPath(outline, body);
        foreach (Cap button in Mouse) DrawCap(graphics, button, false);
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
            if (snapshot.IsDown(button.Code)) DrawCap(graphics, button, true);
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
        using GraphicsPath path = Rounded(cap.Bounds, Math.Min(5, cap.Bounds.Width / 3));
        using var fill = new SolidBrush(pressed ? Accent : Color.FromArgb(185, 23, 29, 35));
        using var border = new Pen(pressed ? Color.FromArgb(255, 139, 242, 250) : Color.FromArgb(200, 108, 123, 136), 1);
        using var text = new SolidBrush(pressed ? Color.FromArgb(255, 8, 30, 35) : Color.FromArgb(245, 224, 233, 239));
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);
        if (cap.Label.Length != 0) graphics.DrawString(cap.Label, _label, text, cap.Bounds, _center);
        if (cap.Code == InputCodes.MouseMiddle)
        {
            using var rib = new Pen(pressed ? Color.FromArgb(255, 8, 30, 35) : Color.FromArgb(210, 150, 168, 180));
            for (int i = 1; i <= 3; i++) graphics.DrawLine(rib, cap.Bounds.Left + 3, cap.Bounds.Top + i * 6,
                cap.Bounds.Right - 3, cap.Bounds.Top + i * 6);
        }
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
