namespace VBAudioMonitor;

internal sealed class SignalChainView
{
    public Panel BuildRootView(Control header, Control routeCard, Control controlCard, Control footer)
    {
        var root = MonitorFormLayout.CreateStack(72, 16, -100, 40);
        root.Padding = new Padding(20, 20, 28, 28);
        root.BackColor = Theme.Background;
        var viewport = new ScrollViewport
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        var cards = viewport.Content;
        cards.Controls.Add(routeCard);
        cards.Controls.Add(controlCard);
        MonitorFormLayout.PlaceCell(root, header, 0);
        MonitorFormLayout.PlaceCell(root, viewport, 2);
        MonitorFormLayout.PlaceCell(root, footer, 3);

        void ArrangeHeader()
        {
            float scale = root.DeviceDpi / 96f;
            bool stacked = (root.ClientSize.Width - root.Padding.Horizontal) / scale < 1100;
            int height = (int)Math.Round((stacked ? 124 : 72) * scale);
            if (header.MinimumSize.Height != height) header.MinimumSize = new Size(0, height);
            if (root.RowStyles[0].Height != height) root.RowStyles[0].Height = height;
        }
        root.ClientSizeChanged += (_, _) => ArrangeHeader();
        root.Layout += (_, _) => ArrangeHeader();

        bool arranging = false;
        void ArrangeCards()
        {
            if (arranging || cards.ClientSize.Width <= 0) return;
            arranging = true;
            try
            {
                float scale = cards.DeviceDpi / 96f;
                int gap = (int)Math.Round(20 * scale);
                int available = viewport.ContentWidth;
                bool stacked = viewport.ClientSize.Width / scale < 940;
                cards.FlowDirection = stacked ? FlowDirection.TopDown : FlowDirection.LeftToRight;
                routeCard.Margin = stacked ? new Padding(0, 0, 0, gap) : new Padding(0, 0, gap, 0);
                controlCard.Margin = Padding.Empty;
                int routeWidth = stacked ? available : (int)Math.Round((available - gap) * 0.60f);
                int controlWidth = stacked ? available : available - gap - routeWidth;
                int height = Math.Max(routeCard.MinimumSize.Height, controlCard.MinimumSize.Height);
                routeCard.Size = new Size(routeWidth, stacked ? routeCard.MinimumSize.Height : height);
                controlCard.Size = new Size(controlWidth, stacked ? controlCard.MinimumSize.Height : height);
            }
            finally { arranging = false; }
        }

        cards.ClientSizeChanged += (_, _) => ArrangeCards();
        cards.Layout += (_, _) => ArrangeCards();
        viewport.ClientSizeChanged += (_, _) => ArrangeCards();
        return root;
    }

    public TableLayoutPanel BuildHeader(BrandMark brand, ActionButton theme, ActionButton refresh,
        ActionButton tray, ActionButton exit, BadgeBox badge)
    {
        var header = MonitorFormLayout.CreateColumns(52, 16, -100, 16, 600, 16, 100);
        header.RowCount = 2;
        header.RowStyles.Clear();
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        header.MinimumSize = new Size(0, 72);
        header.BackColor = Theme.Background;
        MonitorFormLayout.PlaceCell(header, brand, 0, 0);
        brand.Margin = new Padding(0, 8, 0, 12);
        var title = MonitorFormLayout.CreateStack(42, 28);
        MonitorFormLayout.PlaceCell(title, MonitorFormLayout.CreateLabel("VB 音频监听", 23f, true), 0);
        MonitorFormLayout.PlaceCell(title, MonitorFormLayout.CreateLabel("让虚拟线路的声音，回到你的耳机。", 12f), 1);
        MonitorFormLayout.PlaceCell(header, title, 0, 2);
        MonitorFormLayout.PlaceCell(header, badge, 0, 6);
        badge.Margin = new Padding(0, 16, 0, 16);

        theme.Text = Theme.IsDark ? "浅色模式" : "深色模式";
        refresh.Text = "刷新设备";
        tray.Text = "隐藏到托盘";
        exit.Text = "退出程序";
        var toolbar = MonitorFormLayout.CreateColumns(-25, 8, -25, 8, -25, 8, -25);
        ActionButton[] actions = [theme, refresh, tray, exit];
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Compact = true;
            MonitorFormLayout.PlaceCell(toolbar, actions[i], 0, i * 2);
        }
        MonitorFormLayout.PlaceCell(header, toolbar, 0, 4);
        toolbar.Margin = new Padding(0, 16, 0, 16);

        header.ClientSizeChanged += (_, _) =>
        {
            float scale = header.DeviceDpi / 96f;
            bool stacked = header.ClientSize.Width / scale < 1100;
            header.SuspendLayout();
            header.RowStyles[0].Height = 72 * scale;
            header.ColumnStyles[3].Width = stacked ? 0 : 16 * scale;
            header.ColumnStyles[4].Width = stacked ? 0 : 600 * scale;
            header.RowStyles[1].Height = stacked ? 52 * scale : 0;
            badge.Margin = new Padding(0, (int)(16 * scale), 0, (int)(16 * scale));
            header.SetCellPosition(toolbar, new TableLayoutPanelCellPosition(stacked ? 0 : 4, stacked ? 1 : 0));
            header.SetColumnSpan(toolbar, stacked ? 7 : 1);
            toolbar.Margin = stacked ? new Padding(0, (int)(6 * scale), 0, (int)(6 * scale)) : new Padding(0, (int)(16 * scale), 0, (int)(16 * scale));
            header.ResumeLayout();
        };
        return header;
    }

    public Surface BuildRouteCard(DeviceSelector source, DeviceSelector target, DeviceSelector channels,
        DeviceSelector latency, Label sourceInfo, Label targetInfo, LevelMeter meter, Label decibel,
        DeviceSelector profiles, ActionButton saveProfile, ActionButton deleteProfile)
    {
        var card = new Surface { Padding = new Padding(20), MinimumSize = new Size(0, 530), AccessibleName = "音频路由" };
        var stack = MonitorFormLayout.CreateStack(36, 36, 24, 28, 40, 26, 24, 28, 40, 26, 4, 28, 54, 24, 2, 42, 0);
        card.Controls.Add(stack);
        var title = MonitorFormLayout.CreateColumns(-100, 8, 128, 8, 120);
        MonitorFormLayout.PlaceCell(title, MonitorFormLayout.CreateLabel("声音路由", 17f, true));
        saveProfile.Text = "保存方案";
        deleteProfile.Text = "删除";
        saveProfile.Compact = deleteProfile.Compact = true;
        MonitorFormLayout.PlaceCell(title, saveProfile, 0, 2);
        MonitorFormLayout.PlaceCell(title, deleteProfile, 0, 4);
        MonitorFormLayout.PlaceCell(stack, title, 0);
        title.Margin = new Padding(0, 0, 0, 4);
        profiles.AccessibleName = "场景方案";
        profiles.ItemHeight = 30;
        var profileRow = MonitorFormLayout.CreateColumns(112, 8, -100);
        MonitorFormLayout.PlaceCell(profileRow, MonitorFormLayout.CreateLabel("场景方案", 12f), 0, 0);
        MonitorFormLayout.PlaceCell(profileRow, MonitorFormLayout.WrapSelector(profiles), 0, 2);
        MonitorFormLayout.PlaceCell(stack, profileRow, 1);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("网页扬声器与声音来源请选择同一个 CABLE。", 11.5f), 2);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("声音来源 · 虚拟线路", 13f, true), 3);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.WrapSelector(source), 4);
        MonitorFormLayout.PlaceCell(stack, sourceInfo, 5);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("↓  声音转发到", 11.5f), 6);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("监听到 · 耳机或音箱", 13f, true), 7);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.WrapSelector(target), 8);
        MonitorFormLayout.PlaceCell(stack, targetInfo, 9);

        var meterHeader = MonitorFormLayout.CreateColumns(-100, 132);
        MonitorFormLayout.PlaceCell(meterHeader, MonitorFormLayout.CreateLabel("输入电平", 13f, true));
        decibel.TextAlign = ContentAlignment.MiddleRight;
        MonitorFormLayout.PlaceCell(meterHeader, decibel, 0, 1);
        MonitorFormLayout.PlaceCell(stack, meterHeader, 11);
        MonitorFormLayout.PlaceCell(stack, meter, 12);
        var meterScale = MonitorFormLayout.CreateColumns(-25, -25, -25, -25);
        string[] ticks = ["-54 dB", "-24 dB", "-12 dB", "0 dB"];
        for (int i = 0; i < ticks.Length; i++)
            MonitorFormLayout.PlaceCell(meterScale, MonitorFormLayout.CreateLabel(ticks[i], 11.5f, false,
                i == 3 ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft), 0, i);
        MonitorFormLayout.PlaceCell(stack, meterScale, 13);

        var advancedButton = new ActionButton { Text = "高级设置 · 声道与缓冲 ▾", Compact = true, AccessibleName = "高级设置" };
        MonitorFormLayout.PlaceCell(stack, advancedButton, 15);
        var advanced = MonitorFormLayout.CreateStack(12, 112);
        var fields = MonitorFormLayout.CreateColumns(-50, 16, -50);
        var channelField = MonitorFormLayout.CreateStack(30, 40, 32);
        MonitorFormLayout.PlaceCell(channelField, MonitorFormLayout.CreateLabel("监听声道", 13f, true), 0);
        MonitorFormLayout.PlaceCell(channelField, MonitorFormLayout.WrapSelector(channels), 1);
        MonitorFormLayout.PlaceCell(channelField, MonitorFormLayout.CreateLabel("通常选择声道 1 / 2", 11.5f), 2);
        var latencyField = MonitorFormLayout.CreateStack(30, 40, 32);
        MonitorFormLayout.PlaceCell(latencyField, MonitorFormLayout.CreateLabel("音频缓冲", 13f, true), 0);
        MonitorFormLayout.PlaceCell(latencyField, MonitorFormLayout.WrapSelector(latency), 1);
        MonitorFormLayout.PlaceCell(latencyField, MonitorFormLayout.CreateLabel("有卡顿时选 200 毫秒", 11.5f), 2);
        MonitorFormLayout.PlaceCell(fields, channelField, 0, 0);
        MonitorFormLayout.PlaceCell(fields, latencyField, 0, 2);
        MonitorFormLayout.PlaceCell(advanced, fields, 1);
        MonitorFormLayout.PlaceCell(stack, advanced, 16);
        advanced.Visible = false;
        advancedButton.Click += (_, _) =>
        {
            bool expanded = !advanced.Visible;
            stack.SuspendLayout();
            advanced.Visible = expanded;
            stack.RowStyles[16].Height = expanded ? 124 * card.DeviceDpi / 96f : 0;
            UiText.Bind(advancedButton, expanded ? "收起高级设置 ▴" : "高级设置 · 声道与缓冲 ▾");
            card.MinimumSize = new Size(0, card.Padding.Vertical + (int)Math.Ceiling(stack.RowStyles.Cast<RowStyle>().Sum(row => row.Height)));
            card.Height = card.MinimumSize.Height;
            stack.ResumeLayout();
            card.Parent?.PerformLayout();
        };
        return card;
    }

    public Surface BuildControlCard(SmoothSlider slider, Label gain, ActionButton mute, ActionButton diagnostics, ActionButton master,
        Label statusTitle, Label statusMessage, Label latencyStat, Label underrun)
    {
        var card = new Surface { Padding = new Padding(20), MinimumSize = new Size(0, 530), AccessibleName = "监听控制" };
        var stack = MonitorFormLayout.CreateStack(36, 26, 52, 44, 24, 40, 12, 42, 10, 32, 80, 12, 64);
        card.Controls.Add(stack);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("监听控制", 17f, true), 0);
        MonitorFormLayout.PlaceCell(stack, MonitorFormLayout.CreateLabel("调整音量只影响你的监听输出", 11.5f), 1);
        var volume = MonitorFormLayout.CreateColumns(-100, 156);
        MonitorFormLayout.PlaceCell(volume, MonitorFormLayout.CreateLabel("监听音量", 13f, true));
        gain.TextAlign = ContentAlignment.MiddleRight;
        MonitorFormLayout.PlaceCell(volume, gain, 0, 1);
        MonitorFormLayout.PlaceCell(stack, volume, 2);
        MonitorFormLayout.PlaceCell(stack, slider, 3);
        var volumeTicks = MonitorFormLayout.CreateColumns(-33, -33, -34);
        MonitorFormLayout.PlaceCell(volumeTicks, MonitorFormLayout.CreateLabel("静音", 11.5f), 0, 0);
        MonitorFormLayout.PlaceCell(volumeTicks, MonitorFormLayout.CreateLabel("50%", 11.5f, false, ContentAlignment.MiddleCenter), 0, 1);
        MonitorFormLayout.PlaceCell(volumeTicks, MonitorFormLayout.CreateLabel("100%", 11.5f, false, ContentAlignment.MiddleRight), 0, 2);
        MonitorFormLayout.PlaceCell(stack, volumeTicks, 4);
        var quickActions = MonitorFormLayout.CreateColumns(-50, 12, -50);
        MonitorFormLayout.PlaceCell(quickActions, mute, 0, 0);
        MonitorFormLayout.PlaceCell(quickActions, diagnostics, 0, 2);
        MonitorFormLayout.PlaceCell(stack, quickActions, 5);
        MonitorFormLayout.PlaceCell(stack, master, 7);
        statusTitle.TextAlign = ContentAlignment.MiddleLeft;
        statusMessage.TextAlign = ContentAlignment.TopLeft;
        MonitorFormLayout.PlaceCell(stack, statusTitle, 9);
        MonitorFormLayout.PlaceCell(stack, statusMessage, 10);

        var statistics = MonitorFormLayout.CreateStack(32, 32);
        var bufferRow = MonitorFormLayout.CreateColumns(-65, -35);
        var underrunRow = MonitorFormLayout.CreateColumns(-65, -35);
        MonitorFormLayout.PlaceCell(bufferRow, MonitorFormLayout.CreateLabel("缓冲时长", 11.5f), 0, 0);
        latencyStat.TextAlign = ContentAlignment.MiddleRight;
        MonitorFormLayout.PlaceCell(bufferRow, latencyStat, 0, 1);
        MonitorFormLayout.PlaceCell(underrunRow, MonitorFormLayout.CreateLabel("缓冲重置", 11.5f), 0, 0);
        underrun.TextAlign = ContentAlignment.MiddleRight;
        MonitorFormLayout.PlaceCell(underrunRow, underrun, 0, 1);
        MonitorFormLayout.PlaceCell(statistics, bufferRow, 0);
        MonitorFormLayout.PlaceCell(statistics, underrunRow, 1);
        MonitorFormLayout.PlaceCell(stack, statistics, 12);
        return card;
    }

    public TableLayoutPanel BuildFooter(DeviceSelector languages, ActionButton updates)
    {
        var footer = MonitorFormLayout.CreateColumns(-100, 12, 152, 12, 150);
        MonitorFormLayout.PlaceCell(footer, MonitorFormLayout.CreateLabel("关闭→托盘 · 退出→停止监听", 11.5f));
        MonitorFormLayout.PlaceCell(footer, updates, 0, 2);
        MonitorFormLayout.PlaceCell(footer, MonitorFormLayout.WrapSelector(languages), 0, 4);
        return footer;
    }
}
