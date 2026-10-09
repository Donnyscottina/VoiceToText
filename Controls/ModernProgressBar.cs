using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace VoiceToText.Controls;

internal sealed class ModernProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private int _value;
    private bool _isIndeterminate = true;
    private float _animationPosition = -0.28f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate == value) return;
            _isIndeterminate = value;
            _animationPosition = -0.28f;
            UpdateAnimation();
            Invalidate();
        }
    }

    public ModernProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) =>
        {
            _animationPosition += 0.018f;
            if (_animationPosition > 1f) _animationPosition = -0.28f;
            Invalidate();
        };
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimation();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateAnimation();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        var radius = Math.Max(2, bounds.Height / 2);
        using var track = RoundedRectangle(bounds, radius);
        using var trackBrush = new SolidBrush(Color.FromArgb(38, 45, 60));
        e.Graphics.FillPath(trackBrush, track);

        RectangleF fill;
        if (IsIndeterminate)
        {
            var width = Math.Max(34f, bounds.Width * 0.28f);
            fill = new RectangleF(bounds.Width * _animationPosition, 0, width, bounds.Height);
        }
        else
        {
            fill = new RectangleF(0, 0, bounds.Width * Value / 100f, bounds.Height);
        }

        if (fill.Width < 1f) return;
        var state = e.Graphics.Save();
        e.Graphics.SetClip(track);
        using var fillBrush = new LinearGradientBrush(fill,
            Color.FromArgb(126, 111, 255), Color.FromArgb(91, 203, 255), LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(fillBrush, fill);
        e.Graphics.Restore(state);
    }

    private void UpdateAnimation() => _animationTimer.Enabled = Visible && IsIndeterminate;

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animationTimer.Dispose();
        base.Dispose(disposing);
    }
}
