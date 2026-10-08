using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace VoiceToText.Controls;

internal sealed class CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 14;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CardColor { get; set; } = Color.FromArgb(24, 29, 40);

    public CardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(22);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(13, 17, 24));
        using var path = RoundedRectangle(ClientRectangle, CornerRadius);
        using var brush = new SolidBrush(CardColor);
        e.Graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
