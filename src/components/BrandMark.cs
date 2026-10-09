namespace VBAudioMonitor;

/// <summary>
/// Renders the signature audio waveform brand mark.
/// </summary>
internal sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);
        Theme.DrawMark(paintEventArgs.Graphics, new RectangleF(0, 0, Width - 1, Height - 1));
    }
}
