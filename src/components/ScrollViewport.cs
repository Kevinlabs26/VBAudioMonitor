using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace VBAudioMonitor;

internal sealed class ScrollViewport : Panel
{
    internal FlowLayoutPanel Content { get; } = new ClippedFlowPanel()
    {
        AutoScroll = true, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty
    };
    private readonly ScrollThumb scrollbar;
    private Rectangle contentClip;

    public ScrollViewport()
    {
        scrollbar = new ScrollThumb(Content);
        BackColor = Theme.Background;
        Controls.Add(Content);
        Controls.Add(scrollbar);
        scrollbar.SendToBack();
        Content.Scroll += (_, _) => Synchronize();
        Content.Layout += (_, _) => Synchronize();
        Content.Paint += (_, _) => Synchronize();
    }

    internal int ContentWidth => Math.Max(1, ClientSize.Width - (int)Math.Ceiling(16 * DeviceDpi / 96f));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    protected override void OnLayout(LayoutEventArgs e)
    {
        // Keep native scrolling/focus behavior; clip its unthemed non-client scrollbar outside this viewport.
        int nativeWidth = Math.Max(1, GetSystemMetricsForDpi(2, (uint)DeviceDpi));
        Content.SetBounds(0, 0, ClientSize.Width + nativeWidth, ClientSize.Height);
        var clip = new Rectangle(0, 0, ContentWidth, ClientSize.Height);
        if (clip != contentClip)
        {
            contentClip = clip;
            var previous = Content.Region;
            Content.Region = new Region(clip);
            previous?.Dispose();
        }
        int width = (int)Math.Ceiling(14 * DeviceDpi / 96f);
        scrollbar.SetBounds(Math.Max(0, ClientSize.Width - width), 0, width, ClientSize.Height);
        base.OnLayout(e);
        Synchronize();
    }

    private void Synchronize()
    {
        scrollbar.Synchronize();
    }

    internal void ApplyTheme()
    {
        BackColor = Theme.Background;
        scrollbar.BackColor = Theme.Background;
        scrollbar.Invalidate();
    }

    private sealed class ClippedFlowPanel : FlowLayoutPanel
    {
        protected override void WndProc(ref Message message)
        {
            // Do not print native chrome that is excluded from the visible window region.
            if (message.Msg == 0x317) message.LParam = (nint)((long)message.LParam & ~2L); // WM_PRINT, PRF_NONCLIENT
            base.WndProc(ref message);
        }
    }

    private sealed class ScrollThumb : Control
    {
        private readonly FlowLayoutPanel content;
        private int maximum => Math.Max(0, content.VerticalScroll.Maximum - content.VerticalScroll.LargeChange + 1);
        private int pageSize => Math.Max(1, content.VerticalScroll.LargeChange);
        internal int Position => Math.Clamp(-content.AutoScrollPosition.Y, 0, maximum);
        private int dragOffset;
        private bool dragging, hovered;

        public ScrollThumb(FlowLayoutPanel content)
        {
            this.content = content;
            AccessibleRole = AccessibleRole.ScrollBar;
            AccessibleName = "页面滚动";
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        internal void Synchronize()
        {
            Visible = content.VerticalScroll.Visible && maximum > 0;
            Invalidate();
        }

        private RectangleF Thumb
        {
            get
            {
                float scale = DeviceDpi / 96f;
                float track = Math.Max(1, Height - 8 * scale);
                float height = Math.Min(track, Math.Max(28 * scale, track * pageSize / ((float)maximum + pageSize)));
                float width = (hovered || dragging || Focused ? 8 : 6) * scale;
                return new RectangleF((Width - width) / 2f,
                    4 * scale + (maximum == 0 ? 0 : (track - height) * Position / maximum), width, height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var thumb = Thumb;
            using var path = Theme.Rounded(thumb, thumb.Width / 2f);
            using var brush = new SolidBrush(dragging || Focused ? Theme.Blue : Theme.Muted);
            e.Graphics.FillPath(brush, path);
        }

        private void SetPosition(int value)
        {
            int next = Math.Clamp(value, 0, maximum);
            if (Position == next) return;
            content.AutoScrollPosition = new Point(0, next);
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (Thumb.Contains(e.Location))
            {
                dragOffset = (int)(e.Y - Thumb.Top);
                dragging = true; Capture = true; Invalidate();
            }
            else SetPosition(Position + (e.Y < Thumb.Top ? -pageSize : pageSize));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            float scale = DeviceDpi / 96f;
            float travel = Height - 8 * scale - Thumb.Height;
            if (travel > 0) SetPosition((int)Math.Round((e.Y - dragOffset - 4 * scale) * maximum / travel));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) { dragging = false; Capture = false; Invalidate(); }
        }

        protected override void OnMouseCaptureChanged(EventArgs e) { dragging = false; Invalidate(); base.OnMouseCaptureChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = (int)Math.Round(32 * DeviceDpi / 96f);
            int? next = e.KeyCode switch { Keys.Up => Position - step, Keys.Down => Position + step,
                Keys.PageUp => Position - pageSize, Keys.PageDown => Position + pageSize,
                Keys.Home => 0, Keys.End => maximum, _ => null };
            if (next.HasValue) { SetPosition(next.Value); e.Handled = e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            int step = lines < 0 ? pageSize : lines * (int)Math.Round(18 * DeviceDpi / 96f);
            SetPosition(Position - e.Delta * step / SystemInformation.MouseWheelScrollDelta);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            base.OnMouseWheel(e);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new ScrollThumbAccessibility(this);
        private sealed class ScrollThumbAccessibility(ScrollThumb owner) : ControlAccessibleObject(owner)
        {
            public override string? Value
            {
                get => $"{owner.Position}/{owner.maximum}";
                set { if (int.TryParse(value, out int position)) owner.SetPosition(position); }
            }
        }
    }
}
