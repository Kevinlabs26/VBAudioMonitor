namespace VBAudioMonitor;

/// <summary>
/// A high-precision audio gain slider with keyboard and wheel micro-adjustments.
/// </summary>
internal sealed class SmoothSlider : Control
{
    private const int MaximumValue = 1000;
    private int currentValue = 750;
    private bool isDragging;

    public event EventHandler? ValueChanged;

    public int Value
    {
        get => currentValue;
        set
        {
            int clampedValue = Math.Clamp(value, 0, MaximumValue);
            if (currentValue == clampedValue) return;
            currentValue = clampedValue;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SmoothSlider()
    {
        AccessibleName = "监听音量";
        AccessibleRole = AccessibleRole.Slider;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Theme.Surface;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Surface;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs paintEventArgs)
    {
        base.OnPaint(paintEventArgs);
        paintEventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float thumbRadius = Math.Max(7f, 8.5f * DeviceDpi / 96f);
        float trackLeft = thumbRadius + 4f;
        float trackRight = Math.Max(trackLeft + 2f, Width - thumbRadius - 4f);
        float centerY = Height / 2f;
        float thumbX = trackLeft + (trackRight - trackLeft) * currentValue / MaximumValue;
        float trackHeight = Math.Max(4f, 5f * DeviceDpi / 96f);

        using var trackInactivePath = Theme.Rounded(new RectangleF(trackLeft, centerY - trackHeight / 2f, trackRight - trackLeft, trackHeight), trackHeight / 2f);
        using var trackInactiveBrush = new SolidBrush(Theme.Track);
        paintEventArgs.Graphics.FillPath(trackInactiveBrush, trackInactivePath);

        if (thumbX > trackLeft)
        {
            using var trackActivePath = Theme.Rounded(new RectangleF(trackLeft, centerY - trackHeight / 2f, thumbX - trackLeft, trackHeight), trackHeight / 2f);
            using var trackActiveBrush = new SolidBrush(Theme.Blue);
            paintEventArgs.Graphics.FillPath(trackActiveBrush, trackActivePath);
        }

        float thumbDiameter = thumbRadius * 2f;
        var thumbBounds = new RectangleF(thumbX - thumbRadius, centerY - thumbRadius, thumbDiameter, thumbDiameter);
        using var thumbFillBrush = new SolidBrush(Theme.Surface);
        using var thumbRingPen = new Pen(Theme.Blue, Math.Max(2.5f, 2.8f * DeviceDpi / 96f));
        paintEventArgs.Graphics.FillEllipse(thumbFillBrush, thumbBounds);
        paintEventArgs.Graphics.DrawEllipse(thumbRingPen, thumbBounds);
    }

    protected override void OnMouseDown(MouseEventArgs mouseArgs)
    {
        base.OnMouseDown(mouseArgs);
        if (mouseArgs.Button != MouseButtons.Left) return;
        Focus();
        isDragging = true;
        Capture = true;
        UpdatePositionFromPointer(mouseArgs.X);
    }

    protected override void OnMouseMove(MouseEventArgs mouseArgs)
    {
        base.OnMouseMove(mouseArgs);
        if (isDragging) UpdatePositionFromPointer(mouseArgs.X);
    }

    protected override void OnMouseUp(MouseEventArgs mouseArgs)
    {
        base.OnMouseUp(mouseArgs);
        if (mouseArgs.Button == MouseButtons.Left)
        {
            UpdatePositionFromPointer(mouseArgs.X);
            isDragging = false;
            Capture = false;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs mouseArgs)
    {
        base.OnMouseWheel(mouseArgs);
        Value += (mouseArgs.Delta > 0 ? 50 : -50);
    }

    protected override void OnMouseCaptureChanged(EventArgs eventArgs)
    {
        base.OnMouseCaptureChanged(eventArgs);
        if (!Capture) isDragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs keyArgs)
    {
        int stepDelta = keyArgs.Shift ? 100 : 20;
        switch (keyArgs.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value -= stepDelta; break;
            case Keys.Right: case Keys.Up: Value += stepDelta; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = MaximumValue; break;
            case Keys.PageDown: Value -= 100; break;
            case Keys.PageUp: Value += 100; break;
            default: base.OnKeyDown(keyArgs); return;
        }
        keyArgs.Handled = keyArgs.SuppressKeyPress = true;
    }

    private void UpdatePositionFromPointer(int pointerX)
    {
        float thumbRadius = Math.Max(7f, 8.5f * DeviceDpi / 96f);
        float trackLeft = thumbRadius + 4f;
        float trackWidth = Math.Max(1f, Width - 2 * trackLeft);
        Value = (int)Math.Round(Math.Clamp((pointerX - trackLeft) / trackWidth, 0f, 1f) * MaximumValue);
    }
}
