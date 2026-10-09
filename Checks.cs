using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Velopack;
using Velopack.Locators;

namespace VBAudioMonitor;

internal static class Checks
{
    internal static void ExportIcon()
    {
        Theme.SetDark(false);
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var frames = new List<byte[]>();
        foreach (int size in sizes)
        {
            using var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap)) Theme.DrawMark(graphics, new RectangleF(0, 0, size, size));
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            frames.Add(png.ToArray());
            if (size == 256) bitmap.Save("assets/app.png", ImageFormat.Png);
        }
        using var writer = new BinaryWriter(File.Create("assets/app.ico"));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int index = 0; index < sizes.Length; index++)
        {
            writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
            writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
            writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(frames[index].Length); writer.Write(offset);
            offset += frames[index].Length;
        }
        foreach (var frame in frames) writer.Write(frame);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    public static void Run(bool audio, string? updateFeed = null)
    {
        File.Delete("verification-error.txt");
        File.Delete("verification-result.json");
        CheckPreferencesStorage();
        CheckUpdateSignatures();
        CheckSelectors();
        CheckScrollbars();
        var legacyPreferences = JsonSerializer.Deserialize<Preferences>("""{"Source":"","Target":"","Channel":0,"Latency":100,"Volume":75}""")
            ?? throw new InvalidOperationException("Legacy preferences could not be read");
        Require(legacyPreferences.SliderVolume == 750, "Legacy percentage volume was not migrated");
        Require(legacyPreferences.Profiles == null, "Legacy preferences unexpectedly created listening profiles");
        Require(legacyPreferences.Language == "zh-CN", "Legacy settings did not preserve the Chinese interface");
        Require((legacyPreferences with { Volume = 770 }).SliderVolume == 770, "Existing precise volume was changed");
        var precisePreferences = legacyPreferences with { Volume = 75, Version = 2 };
        Require(precisePreferences.SliderVolume == 75, "New low volume was treated as a legacy percentage");
        var restoredPreferences = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(precisePreferences));
        Require(restoredPreferences is { Version: 2, SliderVolume: 75 },
            "New low volume did not survive preferences serialization");
        var legacyDevice = legacyPreferences with { Target = "headphones" };
        Require(legacyDevice.TargetVolume("headphones") == 750 && legacyDevice.TargetUnmuteVolume("headphones") == 750,
            "Legacy device volume was not migrated");
        Require((legacyDevice with { Volume = 770 }).TargetVolume("headphones") == 770,
            "Legacy precise device volume was changed");
        Require(legacyDevice.TargetVolume("new-output") == 500 && legacyDevice.TargetUnmuteVolume("new-output") == 500,
            "New output did not use a safe default volume");
        var devicePreferences = new Preferences("", "headphones", 0, 100, 0, Version: 2,
            DeviceVolumes: new() { ["headphones"] = 0, ["speakers"] = 75 },
            DeviceUnmuteVolumes: new() { ["headphones"] = 345, ["speakers"] = 620 }, UnmuteVolume: 900);
        Require(devicePreferences.TargetVolume("headphones") == 0 && devicePreferences.TargetUnmuteVolume("headphones") == 345
            && devicePreferences.TargetVolume("speakers") == 75 && devicePreferences.TargetUnmuteVolume("speakers") == 620,
            "Per-device mute or precise volume memory was mixed between devices");
        var restoredDevices = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(devicePreferences));
        Require(restoredDevices != null && restoredDevices.TargetVolume("headphones") == 0
            && restoredDevices.TargetUnmuteVolume("headphones") == 345 && restoredDevices.TargetVolume("speakers") == 75
            && restoredDevices.TargetUnmuteVolume("speakers") == 620, "Device volume memories did not survive serialization");
        var boundedDevices = devicePreferences with
        {
            DeviceVolumes = new() { ["headphones"] = -10, ["speakers"] = 1300 },
            DeviceUnmuteVolumes = new() { ["headphones"] = 0, ["speakers"] = 1300 }
        };
        Require(boundedDevices.TargetVolume("headphones") == 0 && boundedDevices.TargetVolume("speakers") == 1000
            && boundedDevices.TargetUnmuteVolume("headphones") == 1 && boundedDevices.TargetUnmuteVolume("speakers") == 1000,
            "Out-of-range device memories were not clamped safely");
        var fallbackDevices = devicePreferences with
        {
            DeviceVolumes = new() { ["headphones"] = 0, ["speakers"] = 0 }, DeviceUnmuteVolumes = null
        };
        Require(fallbackDevices.TargetUnmuteVolume("headphones") == 900 && fallbackDevices.TargetUnmuteVolume("speakers") == 500,
            "Missing unmute memory reused another device's previous volume");
        CheckProfileModels(precisePreferences);
        var virtualInput = new Endpoint("input", "CABLE In 16ch", 16, 48000);
        var physicalOutput = new Endpoint("output", "Headphones", 2, 48000);
        Require(!MonitorForm.CanMonitor(null, physicalOutput, 0) && !MonitorForm.CanMonitor(virtualInput, null, 0),
            "Missing original endpoints were accepted for monitoring");
        Require(!MonitorForm.CanMonitor(virtualInput, virtualInput with { Id = "virtual-output" }, 0)
            && !MonitorForm.CanMonitor(physicalOutput, physicalOutput, 0)
            && !MonitorForm.CanMonitor(virtualInput, physicalOutput with { Id = virtualInput.Id }, 0),
            "Invalid or feedback endpoints were accepted for monitoring");
        Require(MonitorForm.CanMonitor(virtualInput, physicalOutput, 14)
            && !MonitorForm.CanMonitor(virtualInput, physicalOutput, 16)
            && !MonitorForm.CanMonitor(virtualInput, physicalOutput, 1)
            && !MonitorForm.CanMonitor(virtualInput, physicalOutput, -2), "Invalid stereo channel offset was accepted");
        Require(MonitorForm.CanMonitor(virtualInput with { Channels = 1 }, physicalOutput, 0)
            && !MonitorForm.CanMonitor(virtualInput with { Channels = 1 }, physicalOutput, 2),
            "Mono monitoring did not require channel zero");
        foreach (var errorCode in new[] { AudioClientErrorCode.DeviceInvalidated, AudioClientErrorCode.ResourcesInvalidated,
            AudioClientErrorCode.DeviceInUse, AudioClientErrorCode.ServiceNotRunning })
        {
            var error = new COMException("Temporary audio failure", (int)errorCode);
            Require(MonitorForm.CanRetryRecovery(error), "Temporary audio failure was not allowed to retry: " + errorCode);
            Require(MonitorForm.IsDeviceUnavailableError(error)
                == (errorCode is AudioClientErrorCode.DeviceInvalidated or AudioClientErrorCode.ResourcesInvalidated),
                "Audio failure was incorrectly classified as a disconnected device: " + errorCode);
        }
        foreach (var permanentError in new Exception[]
        {
            new COMException("Unsupported format", (int)AudioClientErrorCode.UnsupportedFormat),
            new ArgumentException("Invalid route parameter"),
            new COMException("Access denied", unchecked((int)0x80070005))
        })
            Require(!MonitorForm.CanRetryRecovery(permanentError) && !MonitorForm.IsDeviceUnavailableError(permanentError),
                "Permanent audio failure was allowed to retry indefinitely: " + permanentError.Message);
        CheckGainSmoothing();
        // Verify the channel conversion using actual BufferedWaveProvider bytes.
        foreach (int channels in new[] { 1, 2, 16 })
        {
            var input = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, channels))
            { ReadFully = false };
            float[] frame = Enumerable.Range(0, channels * 2).Select(i => (i + 1) / 100f).ToArray();
            byte[] data = new byte[frame.Length * 4]; Buffer.BlockCopy(frame, 0, data, 0, data.Length);
            input.AddSamples(data, 0, data.Length);
            int pair = channels == 16 ? 14 : 0;
            var provider = new StereoPairProvider(input.ToSampleProvider(), pair);
            float[] output = Enumerable.Repeat(-1f, 10).ToArray();
            Require(provider.Read(output, 2, 4) == 4, "Wrong stereo frame count");
            Require(output[2] == frame[pair] && output[3] == frame[channels == 1 ? 0 : pair + 1], "Wrong first frame");
            Require(output[4] == frame[channels + pair] && output[5] == frame[channels + (channels == 1 ? 0 : pair + 1)], "Wrong second frame");
            Require(output[1] == -1 && output[6] == -1, "Read wrote outside requested range");
            Require(provider.Read(output, 0, 4) == 0, "End-of-stream not preserved");
        }
        var silence = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 16));
        var silentPair = new StereoPairProvider(silence.ToSampleProvider(), 0);
        var silentOutput = new float[100];
        Require(silentPair.Read(silentOutput, 0, 100) == 100 && silentOutput.All(v => v == 0), "Silence underflow failed");
        try { _ = new StereoPairProvider(silence.ToSampleProvider(), 15); throw new Exception("Invalid pair accepted"); }
        catch (ArgumentOutOfRangeException) { }

        var endpoints = Endpoint.List();
        File.WriteAllText("verification-devices.json", JsonSerializer.Serialize(endpoints, new JsonSerializerOptions { WriteIndented = true }));
        using (var icon = Theme.CreateIcon())
        using (var iconFile = File.Create("verification-app.ico")) icon.Save(iconFile);
        using (var form = new MonitorForm(overrideDark: false))
        {
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Show(); form.Update();
            var controls = Descendants(form).ToArray();
            var gain = controls.OfType<SmoothSlider>().Single();
            var mute = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "静音切换");
            var diagnostics = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "检查音频");
            var master = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "开始或停止监听");
            var profilePicker = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "场景方案");
            var saveProfile = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "保存方案");
            var deleteProfile = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "删除方案");
            var routeCard = controls.Single(control => control.AccessibleName == "音频路由");
            var controlCard = controls.Single(control => control.AccessibleName == "监听控制");
            var toolbar = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "退出程序").Parent
                ?? throw new InvalidOperationException("Header toolbar is missing");
            var header = toolbar.Parent ?? throw new InvalidOperationException("Header is missing");
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save("verification-window.png", ImageFormat.Png);
            using var client = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.Controls[0].DrawToBitmap(client, new Rectangle(Point.Empty, form.ClientSize));
            client.Save("界面预览.png", ImageFormat.Png);
            Require(header.Height == header.MinimumSize.Height,
                $"Default header height does not match its required height: height={header.Height}, minimum={header.MinimumSize.Height}, toolbar={toolbar.Bounds}, root={form.Controls[0].Bounds}, dpi={form.DeviceDpi}");
            foreach (Control action in new Control[] { profilePicker, saveProfile, deleteProfile })
                Require(action.Visible && routeCard.RectangleToScreen(routeCard.ClientRectangle).Contains(action.RectangleToScreen(action.ClientRectangle)),
                    "Listening profile control is clipped in the default layout: " + action.AccessibleName);
            Require(!saveProfile.RectangleToScreen(saveProfile.ClientRectangle).IntersectsWith(deleteProfile.RectangleToScreen(deleteProfile.ClientRectangle)),
                "Save and delete profile actions overlap");
            Require(routeCard.Top == controlCard.Top && routeCard.Right <= controlCard.Left,
                $"Default layout does not place the route and monitoring cards side by side: route={routeCard.Bounds}, control={controlCard.Bounds}, viewport={routeCard.Parent?.ClientSize}, dpi={form.DeviceDpi}, baseline={form.AutoScaleDimensions}, current={form.CurrentAutoScaleDimensions}, client={form.ClientSize}");
            Require(mute.RectangleToScreen(mute.ClientRectangle).Top == diagnostics.RectangleToScreen(diagnostics.ClientRectangle).Top
                && !mute.RectangleToScreen(mute.ClientRectangle).IntersectsWith(diagnostics.RectangleToScreen(diagnostics.ClientRectangle))
                && diagnostics.RectangleToScreen(diagnostics.ClientRectangle).Bottom <= master.RectangleToScreen(master.ClientRectangle).Top,
                "Mute and diagnostics actions overlap each other or the monitoring action");
            int originalGain = gain.Value;
            gain.Value = 345;
            gain.Value = 0;
            Require(mute.Text == "取消静音" && mute.Danger, "Zero slider volume did not update the mute button");
            gain.Value = 345;
            Require(mute.Text == "静音" && !mute.Danger, "Restored slider volume left the mute button active");
            mute.PerformClick();
            Require(gain.Value == 0 && mute.Text == "取消静音" && mute.Danger, "Mute button did not set zero volume");
            mute.PerformClick();
            Require(gain.Value == 345 && mute.Text == "静音" && !mute.Danger, "Unmute did not restore the previous volume");
            gain.Value = originalGain;
            var targetSelector = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "监听目标");
            object? originalTarget = targetSelector.SelectedItem;
            var headphonesA = new Endpoint("__check_headphones_a_" + Guid.NewGuid().ToString("N"), "检查耳机 A", 2, 48000);
            var headphonesB = new Endpoint("__check_headphones_b_" + Guid.NewGuid().ToString("N"), "检查耳机 B", 2, 48000);
            targetSelector.Items.AddRange([headphonesA, headphonesB]);
            targetSelector.SelectedItem = headphonesA;
            Require(gain.Value == 500, "First output did not start at fifty percent");
            gain.Value = 320; mute.PerformClick();
            targetSelector.SelectedItem = headphonesB;
            Require(gain.Value == 500, "Second output inherited the first output's volume");
            gain.Value = 640;
            targetSelector.SelectedItem = headphonesA;
            Require(gain.Value == 0 && mute.Text == "取消静音", "Returning to a muted output did not preserve mute");
            mute.PerformClick();
            Require(gain.Value == 320, "Unmute reused another output's previous volume");
            targetSelector.SelectedItem = headphonesB;
            Require(gain.Value == 640, "Returning to another output lost its remembered volume");
            targetSelector.SelectedItem = originalTarget;
            gain.Value = originalGain;
            targetSelector.Items.Remove(headphonesA); targetSelector.Items.Remove(headphonesB);
            CheckMissingRouteRecovery(form, controls, master);
            form.Size = form.MinimumSize; form.PerformLayout(); form.Update();
            using var narrow = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(narrow, new Rectangle(Point.Empty, form.Size));
            narrow.Save("verification-window-narrow.png", ImageFormat.Png);
            Require(header.Height == header.MinimumSize.Height,
                $"Narrow header height does not match its required height: height={header.Height}, minimum={header.MinimumSize.Height}, toolbar={toolbar.Bounds}, root={form.Controls[0].Bounds}, dpi={form.DeviceDpi}");
            Require(routeCard.Left == controlCard.Left && routeCard.Bottom <= controlCard.Top,
                "Minimum window size did not stack the cards without overlap");
            var cardFlow = routeCard.Parent as FlowLayoutPanel ?? throw new InvalidOperationException("Card flow is missing");
            var viewport = cardFlow.Parent as ScrollViewport ?? throw new InvalidOperationException("Card viewport is missing");
            foreach (Control action in new Control[] { profilePicker, saveProfile, deleteProfile })
                Require(viewport.RectangleToScreen(viewport.ClientRectangle).Contains(action.RectangleToScreen(action.ClientRectangle)),
                    "Listening profile control is clipped in the narrow layout: " + action.AccessibleName);
            var toolbarButtons = toolbar.Controls.OfType<ActionButton>().ToArray();
            Require(toolbarButtons.Length == 4, "Header toolbar does not contain all four actions");
            foreach (var button in toolbarButtons)
            {
                var bounds = button.RectangleToScreen(button.ClientRectangle);
                Require(button.Visible && bounds.Width > 0 && bounds.Height > 0
                    && toolbar.RectangleToScreen(toolbar.ClientRectangle).Contains(bounds)
                    && header.RectangleToScreen(header.ClientRectangle).Contains(bounds)
                    && form.Controls[0].RectangleToScreen(form.Controls[0].ClientRectangle).Contains(bounds)
                    && !viewport.RectangleToScreen(viewport.ClientRectangle).IntersectsWith(bounds),
                    $"Narrow header action is clipped or covered: {button.Text}, button={bounds}, header={header.RectangleToScreen(header.ClientRectangle)}, viewport={viewport.RectangleToScreen(viewport.ClientRectangle)}");
            }
            var advancedButton = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "高级设置");
            var advancedSelectors = controls.OfType<DeviceSelector>().Where(selector =>
                selector.AccessibleName is "监听声道" or "音频缓冲").ToArray();
            int collapsedHeight = routeCard.MinimumSize.Height;
            advancedButton.PerformClick(); form.PerformLayout(); form.Update();
            using var expanded = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(expanded, new Rectangle(Point.Empty, form.Size));
            expanded.Save("verification-window-narrow-expanded.png", ImageFormat.Png);
            Require(advancedSelectors.Length == 2 && advancedSelectors.All(selector => selector.Visible)
                && routeCard.MinimumSize.Height > collapsedHeight, "Advanced settings did not expand");
            Require(routeCard.Left == controlCard.Left && routeCard.Bottom <= controlCard.Top,
                "Expanded advanced settings overlap the monitoring card");
            int expandedHeight = routeCard.MinimumSize.Height;
            advancedButton.PerformClick(); form.PerformLayout(); form.Update();
            Require(advancedSelectors.All(selector => !selector.Visible) && routeCard.MinimumSize.Height < expandedHeight,
                "Advanced settings did not collapse");
            Require(routeCard.Left == controlCard.Left && routeCard.Bottom <= controlCard.Top,
                "Collapsed advanced settings overlap the monitoring card");
            var scrollViewport = viewport.Content;
            scrollViewport.ScrollControlIntoView(controlCard); form.Update();
            var viewportScrollbar = viewport.Controls.Cast<Control>().Single(control => control.AccessibleRole == AccessibleRole.ScrollBar);
            int scrollMaximum = scrollViewport.VerticalScroll.Maximum - scrollViewport.VerticalScroll.LargeChange + 1;
            Require(viewportScrollbar.AccessibilityObject.Value == $"{-scrollViewport.AutoScrollPosition.Y}/{scrollMaximum}",
                "Themed scrollbar did not follow programmatic native scrolling");
            Require(viewport.RectangleToScreen(viewport.ClientRectangle).Contains(master.RectangleToScreen(master.ClientRectangle)),
                "Minimum window size cannot scroll the monitoring action into view");
            Require(viewport.RectangleToScreen(viewport.ClientRectangle).Contains(diagnostics.RectangleToScreen(diagnostics.ClientRectangle))
                && !mute.RectangleToScreen(mute.ClientRectangle).IntersectsWith(diagnostics.RectangleToScreen(diagnostics.ClientRectangle)),
                "Narrow monitoring card cannot display the diagnostics action without overlap");
            using var narrowControl = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(narrowControl, new Rectangle(Point.Empty, form.Size));
            narrowControl.Save("verification-window-narrow-control.png", ImageFormat.Png);
            scrollViewport.AutoScrollPosition = Point.Empty;
            float dpiScale = form.DeviceDpi / 96f;
            form.ClientSize = new Size((int)Math.Round(1560 * dpiScale), (int)Math.Round(880 * dpiScale));
            form.PerformLayout(); form.Update();
            Require(routeCard.Top == controlCard.Top && routeCard.Right <= controlCard.Left,
                "Wide layout did not return the cards to a single row");
            using var wide = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(wide, new Rectangle(Point.Empty, form.Size));
            wide.Save("verification-window-wide.png", ImageFormat.Png);
            Require(header.Height == header.MinimumSize.Height,
                $"Wide header height does not match its required height: height={header.Height}, minimum={header.MinimumSize.Height}, toolbar={toolbar.Bounds}, root={form.Controls[0].Bounds}, dpi={form.DeviceDpi}");
        }
        Theme.SetDark(true);
        using (var darkForm = new MonitorForm(overrideDark: true))
        {
            darkForm.ShowInTaskbar = false;
            darkForm.Opacity = 0;
            darkForm.Show(); darkForm.Update();
            using var darkClient = new Bitmap(darkForm.ClientSize.Width, darkForm.ClientSize.Height);
            darkForm.Controls[0].DrawToBitmap(darkClient, new Rectangle(Point.Empty, darkForm.ClientSize));
            darkClient.Save("界面预览-深色.png", ImageFormat.Png);
        }
        Theme.SetDark(false);
        CheckDiagnostics();
        CheckTrayProfiles();
        CheckProfiles();
        CheckProfileNameForm();
        CheckLocalization();
        CheckUpdates();
        if (updateFeed != null) CheckUpdateFeed(updateFeed);
        CheckFormDisposal();
        CheckIcon();
        var result = new List<object>();
        int successfulRoutes = 0;
        if (audio)
        {
            var physical = Endpoint.PreferredOutput(endpoints)
                ?? throw new Exception("No physical output for the audio check");
            var cables = endpoints.Where(e => e.IsVirtual && e.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)).ToList();
            Require(cables.Count > 0, "No cable to test");
            foreach (var cable in cables)
            {
                try { result.Add(TestRelay(cable, physical)); successfulRoutes++; }
                catch (Exception ex) when (ex.HResult == unchecked((int)0x8889000A))
                {
                    result.Add(new { Source = cable.Name, Skipped = true, Reason = "Device in use; VB-CABLE's two input pins cannot be used simultaneously." });
                }
            }
        }
        bool passed = !audio || successfulRoutes > 0;
        File.WriteAllText("verification-result.json", JsonSerializer.Serialize(new { Passed = passed, AudioTest = audio, PreferencesSecurityTest = true, UpdateSignatureTest = true, UpdateFeedTest = updateFeed != null, Results = result }, new JsonSerializerOptions { WriteIndented = true }));
        Require(passed, "No audio route could be verified: devices are busy.");
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendSelectorMessage(nint window, uint message, nint parameter, nint data);

    private static void CheckSelectors()
    {
        bool originalTheme = Theme.IsDark;
        try
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme.SetDark(dark);
                using var form = new Form { Font = Theme.TextFont(14), BackColor = Theme.Surface,
                    AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96),
                    ShowInTaskbar = false, Opacity = 0, ClientSize = new Size(440, 160) };
                var selector = new DeviceSelector { AccessibleName = "Selector check" };
                selector.Items.AddRange(["Speakers & headphones", "CABLE In 16ch (VB-Audio Virtual Cable)"]);
                selector.SelectedIndex = 0;
                var host = MonitorFormLayout.WrapSelector(selector);
                host.Bounds = new Rectangle(16, 16, 400, 50);
                form.Controls.Add(host);
                form.Show(); form.Update();
                Require(selector.Focus(), "Styled selector cannot receive keyboard focus");
                Require(selector.AccessibilityObject.Role == AccessibleRole.ComboBox,
                    "Styled selector lost its native accessibility role");
                int selectionChanges = 0;
                selector.SelectedIndexChanged += (_, _) => selectionChanges++;
                SendSelectorMessage(selector.Handle, 0x100, (nint)Keys.Down, 0); // WM_KEYDOWN
                SendSelectorMessage(selector.Handle, 0x101, (nint)Keys.Down, 0); // WM_KEYUP
                Require(selector.SelectedIndex == 1 && selectionChanges == 1,
                    "Native keyboard navigation did not select the next item exactly once");
                selector.DroppedDown = true;
                Require(selector.DroppedDown, "Styled selector cannot open its native list");
                SendSelectorMessage(selector.Handle, 0x100, (nint)Keys.Escape, 0);
                SendSelectorMessage(selector.Handle, 0x101, (nint)Keys.Escape, 0);
                Require(!selector.DroppedDown, "Escape did not close the native selector list");
                selector.Update();
                Require(host.ClientRectangle.Contains(selector.Bounds)
                    && selector.Height >= host.Height - (int)Math.Ceiling(8 * selector.DeviceDpi / 96f),
                    "Selector is clipped or leaves an excessive gap around its clickable field");
                using var bitmap = new Bitmap(selector.Width, selector.Height);
                selector.DrawToBitmap(bitmap, selector.ClientRectangle);
                bitmap.Save($"verification-selector-{(dark ? "dark" : "light")}-focused.png", ImageFormat.Png);
                if (dark)
                    for (int x = Math.Max(0, bitmap.Width - (int)(32 * selector.DeviceDpi / 96f)); x < bitmap.Width - 4; x++)
                        for (int y = 4; y < bitmap.Height - 4; y++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            Require(pixel.R < 235 || pixel.G < 235 || pixel.B < 235,
                                "Native white arrow chrome remains in the dark selector");
                        }
                selector.Enabled = false;
                selector.DrawToBitmap(bitmap, selector.ClientRectangle);
                bitmap.Save($"verification-selector-{(dark ? "dark" : "light")}-disabled.png", ImageFormat.Png);
                host.Width /= 2;
                host.PerformLayout();
                Require(selector.Width == host.ClientSize.Width && host.ClientRectangle.Contains(selector.Bounds),
                    "Styled selector does not fit after a narrow resize");
                form.Close();
            }
        }
        finally { Theme.SetDark(originalTheme); }
    }

    private static void CheckScrollbars()
    {
        bool originalTheme = Theme.IsDark;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme.SetDark(dark);
                using var form = new Form { ShowInTaskbar = false, Opacity = 0,
                    AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96), ClientSize = new Size(320, 240) };
                var viewport = new ScrollViewport { Dock = DockStyle.Fill };
                viewport.Content.FlowDirection = FlowDirection.TopDown;
                var tall = new Panel { Size = new Size(240, 1000), Margin = Padding.Empty, BackColor = Theme.Surface };
                viewport.Content.Controls.Add(tall);
                form.Controls.Add(viewport);
                form.Show(); form.Update(); Application.DoEvents();
                var bar = viewport.Controls.Cast<Control>().Single(control => control.AccessibleRole == AccessibleRole.ScrollBar);
                Require(bar.Visible && bar.Focus(), "Overflowing viewport has no focusable themed scrollbar");
                int maximum = viewport.Content.VerticalScroll.Maximum - viewport.Content.VerticalScroll.LargeChange + 1;
                SendSelectorMessage(bar.Handle, 0x100, (nint)Keys.End, 0);
                Require(-viewport.Content.AutoScrollPosition.Y == maximum, "End did not reach the native scroll limit");
                SendSelectorMessage(bar.Handle, 0x100, (nint)Keys.Home, 0);
                Require(viewport.Content.AutoScrollPosition.Y == 0, "Home did not return to the top");
                int y = (int)Math.Round(10 * bar.DeviceDpi / 96f);
                bar.GetType().GetMethod("OnMouseDown", flags)!.Invoke(bar, [new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, y, 0)]);
                bar.GetType().GetMethod("OnMouseMove", flags)!.Invoke(bar, [new MouseEventArgs(MouseButtons.Left, 0, bar.Width / 2, bar.Height + 50, 0)]);
                bar.GetType().GetMethod("OnMouseUp", flags)!.Invoke(bar, [new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, bar.Height + 50, 0)]);
                Require(!bar.Capture && -viewport.Content.AutoScrollPosition.Y == maximum,
                    "Dragging the thumb did not clamp at the bottom or release pointer capture");
                viewport.Content.AutoScrollPosition = Point.Empty;
                viewport.PerformLayout();
                bar.GetType().GetMethod("OnMouseWheel", flags)!.Invoke(bar, [new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, -120)]);
                if (SystemInformation.MouseWheelScrollLines != 0)
                    Require(viewport.Content.AutoScrollPosition.Y < 0, "Wheel over the themed scrollbar did not scroll native content");
                Require(bar.AccessibilityObject.Role == AccessibleRole.ScrollBar
                    && bar.AccessibilityObject.Value == $"{-viewport.Content.AutoScrollPosition.Y}/{maximum}",
                    "Scrollbar accessibility value is out of sync with native content");
                using var bitmap = new Bitmap(viewport.Width, viewport.Height);
                viewport.DrawToBitmap(bitmap, viewport.ClientRectangle);
                bitmap.Save($"verification-scrollbar-{(dark ? "dark" : "light")}.png", ImageFormat.Png);
                if (dark)
                    for (int x = bar.Left; x < bitmap.Width; x++)
                        for (int row = 0; row < bitmap.Height; row++)
                        {
                            Color pixel = bitmap.GetPixel(x, row);
                            Require(pixel.R < 230 || pixel.G < 230 || pixel.B < 230,
                                "Native white scrollbar leaked into the dark viewport");
                        }
                tall.Height = 20;
                viewport.Content.PerformLayout(); viewport.PerformLayout(); form.Update();
                Require(!bar.Visible && viewport.Content.AutoScrollPosition.Y == 0,
                    "Scrollbar or scroll offset remained after content stopped overflowing");
                form.Close();
            }
        }
        finally { Theme.SetDark(originalTheme); }
    }

    private static void CheckPreferencesStorage()
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "verification-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var preferences = new Preferences("test-source", "test-target", 0, 100, 320, Version: 2, Language: "fr");
            File.WriteAllText(path + ".tmp", "existing temporary file must be preserved");
            Require(PreferencesStorage.SavePreferences(preferences, path)
                && PreferencesStorage.LoadPreferences(path) == preferences, "Secure settings save/load lost preferences");
            Require(File.ReadAllText(path + ".tmp") == "existing temporary file must be preserved"
                && Directory.GetFiles(directory, "*.tmp").Length == 1, "Settings save overwrote an existing temporary file or leaked its own temporary file");
            File.WriteAllText(path, "{broken");
            Require(PreferencesStorage.LoadPreferences(path) == null, "Malformed settings were accepted");
            File.WriteAllText(path, "{\"Source\":\"test\",\"Target\":\"out\",\"Channel\":0,\"Latency\":100,\"Volume\":32}", new System.Text.UTF8Encoding(true));
            Require(PreferencesStorage.LoadPreferences(path)?.Volume == 32, "UTF-8 BOM settings stopped loading");
            using (var oversized = new FileStream(path, FileMode.Create)) oversized.SetLength(PreferencesStorage.MaximumFileBytes + 1L);
            Require(PreferencesStorage.LoadPreferences(path) == null, "Oversized settings were read into memory");
            Require(PreferencesStorage.SavePreferences(preferences, path), "Valid settings did not replace a corrupt file");
            byte[] before = File.ReadAllBytes(path);
            Require(!PreferencesStorage.SavePreferences(preferences with { Source = new string('x', PreferencesStorage.MaximumFileBytes) }, path)
                && File.ReadAllBytes(path).SequenceEqual(before), "Oversized save destroyed the previous settings");
            string blockedPath = Path.Combine(directory, "blocked.json");
            Directory.CreateDirectory(blockedPath);
            Require(!PreferencesStorage.SavePreferences(preferences, blockedPath)
                && Directory.Exists(blockedPath) && Directory.GetFiles(directory, "*.tmp").Length == 1,
                "Failed settings replacement destroyed its destination or leaked a temporary file");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void CheckUpdateSignatures()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string hash = new string('A', 64);
        const string version = "1.2.0", name = "VBAudioMonitor-1.2.0-full.nupkg";
        byte[] signature = key.SignData(UpdateProof.Payload(version, name, 123, hash), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string publicKey = key.ExportSubjectPublicKeyInfoPem();
        Require(UpdateProof.Verify(version, name, 123, hash, signature, publicKey), "Valid update proof was rejected");
        Require(!UpdateProof.Verify(version, name, 123, hash, signature, wrongKey.ExportSubjectPublicKeyInfoPem())
            && !UpdateProof.Verify("1.3.0", name, 123, hash, signature, publicKey)
            && !UpdateProof.Verify(version, "VBAudioMonitor-1.3.0-full.nupkg", 123, hash, signature, publicKey)
            && !UpdateProof.Verify(version, name, 124, hash, signature, publicKey)
            && !UpdateProof.Verify(version, name, 123, new string('B', 64), signature, publicKey)
            && !UpdateProof.Verify(version, name, 123, hash, signature[..63], publicKey),
            "Wrong key, changed metadata or truncated signature was accepted");
        signature[0] ^= 1;
        Require(!UpdateProof.Verify(version, name, 123, hash, signature, publicKey), "Changed signature was accepted");
        using var pinned = ECDsa.Create();
        pinned.ImportFromPem(UpdateProof.PublicKey);
        Require(pinned.KeySize == 256 && !UpdateProof.PublicKey.Contains("PRIVATE"), "Pinned identity is not a public P-256 key");
        foreach (var asset in new[]
        {
            new VelopackAsset { PackageId = "VBAudioMonitor", Version = SemanticVersion.Parse(version), Type = VelopackAssetType.Full, FileName = "../update.nupkg" },
            new VelopackAsset { PackageId = "Other", Version = SemanticVersion.Parse(version), Type = VelopackAssetType.Full, FileName = name },
            new VelopackAsset { PackageId = "VBAudioMonitor", Version = SemanticVersion.Parse(version), Type = VelopackAssetType.Delta, FileName = name }
        })
        {
            bool rejected = false;
            try { SignedUpdateSource.ValidateIdentity(asset); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Path traversal, wrong application or unsigned delta was accepted");
        }
    }

    private static void CheckProfileModels(Preferences preferences)
    {
        Require(ListeningProfile.IsValidName(" 配置 ") && ListeningProfile.IsValidName(new string('景', 24)),
            "Valid listening profile name was rejected");
        foreach (string? name in new[] { null, "", "   ", new string('景', 25), "\n配置", "配置\t", "配\0置" })
            Require(!ListeningProfile.IsValidName(name), "Invalid listening profile name was accepted");
        var profile = new ListeningProfile("网页耳机", "profile-source", "profile-target", 0, 100, 0, 320);
        Require(profile.IsValid && profile.ToString() == profile.Name
            && (profile with { Latency = 60, Volume = 1000, UnmuteVolume = 1 }).IsValid
            && (profile with { Latency = 200, Channel = 14, UnmuteVolume = 1000 }).IsValid,
            "Valid listening profile bounds or display name were rejected");
        foreach (var invalid in new[]
        {
            profile with { Name = "\n配置" }, profile with { Name = null! }, profile with { Source = null! },
            profile with { Source = "" }, profile with { Target = " " },
            profile with { Target = profile.Source }, profile with { Channel = -2 }, profile with { Channel = 1 },
            profile with { Latency = 75 }, profile with { Volume = -1 }, profile with { Volume = 1001 },
            profile with { UnmuteVolume = 0 }, profile with { UnmuteVolume = 1001 }
        })
            Require(!invalid.IsValid, "Corrupt listening profile was accepted: " + invalid);
        var roundTrip = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences with { Profiles = [profile] }));
        Require(roundTrip is { Profiles: { Count: 1 } } && roundTrip.Profiles[0] == profile
            && roundTrip.Profiles[0].Volume == 0 && roundTrip.Profiles[0].UnmuteVolume == 320,
            "Listening profile or muted gain did not survive preferences serialization");
        var damaged = JsonSerializer.Deserialize<ListeningProfile>(JsonSerializer.Serialize(profile with { Volume = 1001 }));
        Require(damaged is { Volume: 1001, IsValid: false }, "Corrupt imported profile was silently clamped instead of rejected");
    }

    private static void CheckProfiles()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = new MonitorForm(overrideDark: false) { ShowInTaskbar = false, Opacity = 0 };
        form.Show(); form.Update();
        var controls = Descendants(form).ToArray();
        var source = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "声音来源");
        var target = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "监听目标");
        var channel = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "监听声道");
        var latency = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "音频缓冲");
        var picker = controls.OfType<DeviceSelector>().Single(selector => selector.AccessibleName == "场景方案");
        var gain = controls.OfType<SmoothSlider>().Single();
        var mute = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "静音切换");
        var master = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "开始或停止监听");
        var deleteAction = controls.OfType<ActionButton>().Single(button => button.AccessibleName == "删除方案");
        var save = typeof(MonitorForm).GetMethod("SaveProfile", flags)!;
        var apply = typeof(MonitorForm).GetMethod("ApplyProfile", flags)!;
        var delete = typeof(MonitorForm).GetMethod("DeleteProfile", flags)!;
        var load = typeof(MonitorForm).GetMethod("LoadDevices", flags)!;
        var endpoints = Endpoint.List();
        var fakeSource = new Endpoint("__check_profile_source__", "CABLE 检查方案来源", 2, 48000);
        var fakeTarget = new Endpoint("__check_profile_target__", "检查方案耳机", 2, 48000);
        Require(endpoints.All(endpoint => endpoint.Id != fakeSource.Id && endpoint.Id != fakeTarget.Id),
            "Listening profile check identifiers unexpectedly match real endpoints");
        string? originalSource = (source.SelectedItem as Endpoint)?.Id;
        string? originalTarget = (target.SelectedItem as Endpoint)?.Id;
        int originalGain = gain.Value, originalLatency = latency.SelectedIndex, originalChannel = channel.SelectedIndex;
        int initialCount = picker.Items.Count;
        string name = "CheckProfile-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Require(save.Invoke(form, ["\n" + name]) is false && picker.Items.Count == initialCount,
                "Saving a profile silently trimmed control characters from its name");
            source.Items.Add(fakeSource); target.Items.Add(fakeTarget);
            source.SelectedItem = fakeSource; target.SelectedItem = fakeTarget;
            channel.SelectedIndex = 0; latency.SelectedIndex = 2;
            gain.Value = 320; mute.PerformClick();
            Require(save.Invoke(form, [" " + name + " "]) is true && picker.Items.Count == initialCount + 1,
                "Valid listening profile was not saved in memory");
            var saved = picker.SelectedItem as ListeningProfile ?? throw new InvalidOperationException("Saved profile was not selected");
            Require(saved.Name == name && saved.Source == fakeSource.Id && saved.Target == fakeTarget.Id
                && saved.Channel == 0 && saved.Latency == 200 && saved.Volume == 0 && saved.UnmuteVolume == 320,
                "Saved profile did not capture the complete muted route configuration");
            Require(save.Invoke(form, [name.ToUpperInvariant()]) is true && picker.Items.Count == initialCount + 1,
                "Case-insensitive profile replacement created a duplicate");
            saved = (ListeningProfile)picker.SelectedItem!;
            gain.Value = 640; latency.SelectedIndex = 0;
            var routeType = typeof(MonitorForm).GetNestedType("ListeningRoute", BindingFlags.NonPublic)!;
            var waitingRoute = Activator.CreateInstance(routeType, fakeSource, fakeTarget, 0, 100)!;
            var timer = (System.Windows.Forms.Timer)typeof(MonitorForm).GetField("recoveryTimer", flags)!.GetValue(form)!;
            typeof(MonitorForm).GetMethod("BeginRecovery", flags)!.Invoke(form, [waitingRoute, "等待不存在的方案设备", 2000]);
            Require(picker.Enabled && timer.Enabled && master.Text == "停止等待", "Profile picker was disabled while waiting for recovery");
            apply.Invoke(form, [saved]);
            Require(!timer.Enabled && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null
                && typeof(MonitorForm).GetField("recoveryRoute", flags)!.GetValue(form) == null,
                "Applying a profile did not cancel waiting or started audio automatically");
            Require(source.SelectedItem == null && target.SelectedItem == null && channel.SelectedIndex == -1
                && !master.Enabled && gain.Value == 0 && latency.SelectedIndex == 2
                && (int)typeof(MonitorForm).GetField("preMuteGain", flags)!.GetValue(form)! == 320,
                "Missing profile endpoints fell back to real devices or lost their volume configuration");
            var tray = (TrayManager)typeof(MonitorForm).GetField("trayManager", flags)!.GetValue(form)!;
            var trayProfiles = (ToolStripMenuItem)typeof(TrayManager).GetField("profilesMenu", flags)!.GetValue(tray)!;
            picker.SelectedIndex = 0;
            Require(!deleteAction.Enabled && trayProfiles.DropDownItems.OfType<ToolStripMenuItem>().All(item => !item.Checked),
                "Current configuration placeholder left profile deletion or tray selection active");
            picker.SelectedItem = saved;
            Require(deleteAction.Enabled && picker.SelectedItem is ListeningProfile
                && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null,
                "Selecting the saved profile did not restore deletion state without starting audio");
            load.Invoke(form, [new List<Endpoint> { fakeSource, fakeTarget }, saved]);
            Require((source.SelectedItem as Endpoint)?.Id == fakeSource.Id && (target.SelectedItem as Endpoint)?.Id == fakeTarget.Id
                && channel.SelectedIndex == 0 && master.Enabled
                && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null,
                "Valid profile channel was not restored without starting audio");
            load.Invoke(form, [new List<Endpoint> { fakeSource, fakeTarget }, saved with { Channel = 14 }]);
            Require((source.SelectedItem as Endpoint)?.Id == fakeSource.Id && (target.SelectedItem as Endpoint)?.Id == fakeTarget.Id
                && channel.SelectedIndex == -1 && !master.Enabled,
                "Outdated profile channel silently fell back to a valid channel");
            load.Invoke(form, [new List<Endpoint> { fakeSource, fakeTarget }, null]);
            Require(channel.SelectedIndex == -1 && !master.Enabled,
                "Refreshing devices silently replaced an outdated profile channel");
            channel.SelectedIndex = 0;
            Require(master.Enabled && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null,
                "Selecting a valid channel did not re-enable manual monitoring without starting audio");
            channel.SelectedIndex = -1;
            Require(!master.Enabled, "Invalid channel selection left manual monitoring enabled");
            gain.Value = 640;
            apply.Invoke(form, [saved with { Volume = 1001 }]);
            Require(gain.Value == 640 && (target.SelectedItem as Endpoint)?.Id == fakeTarget.Id,
                "Invalid profile changed the active configuration");
            Require(delete.Invoke(form, [name.ToLowerInvariant()]) is true && picker.Items.Count == initialCount
                && gain.Value == 640 && latency.SelectedIndex == 2 && channel.SelectedIndex == -1
                && (source.SelectedItem as Endpoint)?.Id == fakeSource.Id && (target.SelectedItem as Endpoint)?.Id == fakeTarget.Id,
                "Deleting a profile changed the current route or gain");
            Require(delete.Invoke(form, [name]) is false && picker.Items.Count == initialCount,
                "Deleting a missing profile changed the profile list");
        }
        finally
        {
            typeof(MonitorForm).GetMethod("StopRelay", flags)!.Invoke(form, null);
            load.Invoke(form, [endpoints, null]);
            source.SelectedItem = source.Items.OfType<Endpoint>().FirstOrDefault(endpoint => endpoint.Id == originalSource);
            target.SelectedItem = target.Items.OfType<Endpoint>().FirstOrDefault(endpoint => endpoint.Id == originalTarget);
            channel.SelectedIndex = originalChannel >= 0 && originalChannel < channel.Items.Count ? originalChannel : -1;
            latency.SelectedIndex = originalLatency; gain.Value = originalGain;
            picker.SelectedIndex = 0;
        }
    }

    private static void CheckLocalization()
    {
        UiText.SetLanguage("unsupported");
        Require(UiText.Language == "zh-CN", "Unknown language did not fall back to Chinese");
        foreach (var (key, translations) in UiText.Translations)
        {
            var placeholders = Regex.Matches(key, @"\{(\d+)[^}]*\}").Select(match => match.Groups[1].Value).Order().ToArray();
            foreach (string translated in new[] { translations.English, translations.French })
            {
                Require(!string.IsNullOrWhiteSpace(translated) && !Regex.IsMatch(translated, @"[\u4e00-\u9fff]"), "Missing translation: " + key);
                Require(placeholders.SequenceEqual(Regex.Matches(translated, @"\{(\d+)[^}]*\}").Select(match => match.Groups[1].Value).Order()),
                    "Translation changed format arguments: " + key);
            }
        }
        var preferences = new Preferences("source", "target", 0, 100, 320, Language: "fr");
        Require(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))?.Language == "fr",
            "Language preference did not survive serialization");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = new MonitorForm(overrideDark: false) { Opacity = 0, ShowInTaskbar = false };
        form.Show(); form.Update();
        var controls = Descendants(form).ToArray();
        var sourceSelector = controls.OfType<DeviceSelector>().Single(control => control.Name == "声音来源");
        var targetSelector = controls.OfType<DeviceSelector>().Single(control => control.Name == "监听目标");
        var channel = controls.OfType<DeviceSelector>().Single(control => control.Name == "监听声道");
        var latency = controls.OfType<DeviceSelector>().Single(control => control.Name == "音频缓冲");
        var language = controls.OfType<DeviceSelector>().Single(control => control.Name == "界面语言");
        var picker = controls.OfType<DeviceSelector>().Single(control => control.Name == "场景方案");
        var gain = controls.OfType<SmoothSlider>().Single();
        var master = controls.OfType<ActionButton>().Single(control => control.Name == "开始或停止监听");
        var toolbar = controls.OfType<ActionButton>().Single(control => control.Name == "退出程序").Parent!;
        var source = new Endpoint("__check_language_source__", "CABLE language test", 4, 48000);
        var target = new Endpoint("__check_language_target__", "Language test headphones", 2, 48000);
        Require(Endpoint.List().All(endpoint => endpoint.Id != source.Id && endpoint.Id != target.Id), "Language test IDs match real devices");
        sourceSelector.Items.Add(source); targetSelector.Items.Add(target);
        sourceSelector.SelectedItem = source; targetSelector.SelectedItem = target;
        channel.SelectedIndex = 1; latency.SelectedIndex = 2; gain.Value = 320; gain.Value = 0;
        var saved = (bool)typeof(MonitorForm).GetMethod("SaveProfile", flags)!.Invoke(form, ["会议耳机"] )!;
        Require(saved, "Could not set up a language test preset");
        var preset = picker.SelectedItem;
        var relayField = typeof(MonitorForm).GetField("activeRelay", flags)!;
        var recoveryField = typeof(MonitorForm).GetField("recoveryRoute", flags)!;
        var recoveryTimer = (System.Windows.Forms.Timer)typeof(MonitorForm).GetField("recoveryTimer", flags)!.GetValue(form)!;
        var defaultSize = form.ClientSize;
        try
        {
            foreach (bool dark in new[] { false, true })
            foreach (string code in new[] { "en", "fr", "zh-CN" })
            {
                if (Theme.IsDark != dark) typeof(MonitorForm).GetMethod("ToggleTheme", flags)!.Invoke(form, null);
                language.SelectedItem = UiText.Languages.Single(item => item.Code == code);
                form.Update();
                Require(UiText.Language == code && ReferenceEquals(sourceSelector.SelectedItem, source) && ReferenceEquals(targetSelector.SelectedItem, target)
                    && channel.SelectedIndex == 1 && latency.SelectedIndex == 2 && gain.Value == 0 && picker.SelectedItem == preset
                    && (int)typeof(MonitorForm).GetField("preMuteGain", flags)!.GetValue(form)! == 320 && relayField.GetValue(form) == null,
                    "Language switch changed devices, channels, buffer, mute memory, preset or audio state: " + code);
                Require(controls.OfType<Label>().Any(label => label.Text.Contains("会议耳机")), "Language switch lost the saved preset name in a status message");
                var routeType = typeof(MonitorForm).GetNestedType("ListeningRoute", flags)!;
                var route = Activator.CreateInstance(routeType, source, target, 2, 200)!;
                typeof(MonitorForm).GetMethod("BeginRecovery", flags)!.Invoke(form,
                    [route, "原设备重新连接后会自动恢复监听；点击停止等待可取消。", 2000]);
                string recoveryLanguage = code == "en" ? "fr" : "en";
                language.SelectedItem = UiText.Languages.Single(item => item.Code == recoveryLanguage);
                Require(ReferenceEquals(recoveryField.GetValue(form), route) && recoveryTimer.Enabled && !sourceSelector.Enabled
                    && master.Text == UiText.T("停止等待") && relayField.GetValue(form) == null,
                    "Language switch cancelled recovery or opened audio");
                typeof(MonitorForm).GetMethod("StopRelay", flags)!.Invoke(form, null);
                var activeRelay = new AudioRelay();
                relayField.SetValue(form, activeRelay);
                typeof(MonitorForm).GetMethod("SetEngineState", flags)!.Invoke(form, [true]);
                language.SelectedItem = UiText.Languages.Single(item => item.Code == code);
                Require(ReferenceEquals(relayField.GetValue(form), activeRelay) && !sourceSelector.Enabled
                    && master.Text == UiText.T("停止监听"), "Language switch stopped an existing relay");
                typeof(MonitorForm).GetMethod("StopRelay", flags)!.Invoke(form, null);
                if (code != "zh-CN")
                    foreach (var control in controls.Where(control => control is Label or ActionButton))
                        Require(!Regex.IsMatch(control.Text, @"[\u4e00-\u9fff]"), "Untranslated interface text: " + control.Text);
                foreach (var size in new[] { defaultSize, Size.Empty })
                {
                    if (size.IsEmpty) form.Size = form.MinimumSize;
                    else form.ClientSize = size;
                    form.PerformLayout(); form.Update();
                    var textControls = toolbar.Controls.Cast<Control>().Concat(controls.Where(control => control.Name is "保存方案" or "删除方案"
                        || control is Label && Equals(control.Tag, "场景方案")));
                    foreach (var button in textControls)
                    {
                        using var graphics = button.CreateGraphics();
                        var measured = TextRenderer.MeasureText(graphics, button.Text, button.Font, new Size(int.MaxValue, int.MaxValue),
                            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                        Require(measured.Width <= button.Width && measured.Height <= button.Height, "Translated action or preset label is clipped: " + button.Text);
                    }
                    Require(form.RectangleToScreen(form.ClientRectangle).Contains(language.RectangleToScreen(language.ClientRectangle))
                        && language.Parent!.RectangleToScreen(language.Parent.ClientRectangle).Contains(language.RectangleToScreen(language.ClientRectangle)),
                        "Language selector is outside the window");
                    using var screenshot = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, form.Size));
                    screenshot.Save($"verification-language-{code}{(dark ? "-dark" : "")}{(size.IsEmpty ? "-narrow" : "")}.png", ImageFormat.Png);
                }
                form.ClientSize = defaultSize;
                var report = AudioDiagnostics.Run(null, null, 0, 0, false, "TEST-NATIVE-ERROR");
                Require(report.Details.Contains("TEST-NATIVE-ERROR") && (code == "zh-CN" || !Regex.IsMatch(report.Details, @"[\u4e00-\u9fff]")),
                    "Diagnostic translation changed native error data or left Chinese text");
                using var diagnostic = new DiagnosticsForm(report) { Opacity = 0 };
                diagnostic.Show(); diagnostic.Update();
                var details = Descendants(diagnostic).OfType<RichTextBox>().Single();
                Require(details.ReadOnly && details.Text.Replace("\r\n", "\n").TrimEnd('\n') == report.Details.Replace("\r\n", "\n").TrimEnd('\n'), "Localized report lost read-only content");
                using var diagnosticImage = new Bitmap(diagnostic.Width, diagnostic.Height);
                diagnostic.DrawToBitmap(diagnosticImage, new Rectangle(Point.Empty, diagnostic.Size));
                diagnosticImage.Save($"verification-diagnostics-{code}{(dark ? "-dark" : "")}.png", ImageFormat.Png);
                using var nameDialog = new ProfileNameForm("会议耳机") { Opacity = 0 };
                nameDialog.Show(); nameDialog.Update();
                Require(nameDialog.ProfileName == "会议耳机", "Language changed the user-defined preset name");
                foreach (var control in Descendants(nameDialog).Where(control => control is Label or ActionButton))
                {
                    using var graphics = control.CreateGraphics();
                    bool multiline = control.Text.Contains('\n');
                    var measured = TextRenderer.MeasureText(graphics, control.Text, control.Font,
                        new Size(multiline ? control.Width : int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPrefix | (multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
                    Require(measured.Width <= control.Width && measured.Height <= control.Height, "Localized preset dialog clips text: " + control.Text);
                }
                using var nameImage = new Bitmap(nameDialog.Width, nameDialog.Height);
                nameDialog.DrawToBitmap(nameImage, new Rectangle(Point.Empty, nameDialog.Size));
                nameImage.Save($"verification-profile-{code}{(dark ? "-dark" : "")}.png", ImageFormat.Png);
                typeof(MonitorForm).GetMethod("SaveProfile", flags)!.Invoke(form, ["会议耳机"]);
                preset = picker.SelectedItem;
            }
        }
        finally
        {
            typeof(MonitorForm).GetMethod("StopRelay", flags)!.Invoke(form, null);
            UiText.SetLanguage("zh-CN");
            Theme.SetDark(false);
        }
    }

    private sealed class SignedCheckLocator(string directory)
        : TestVelopackLocator("VBAudioMonitor", "1.0.0", directory, null, null, Path.Combine(directory, "Update.exe"))
    {
        public override IProcessImpl Process { get; } = new NoApplyCheckProcess();
    }

    private sealed class NoApplyCheckProcess : IProcessImpl
    {
        public string GetCurrentProcessPath() => Path.Combine(Environment.CurrentDirectory, "VBAudioMonitor.Check.exe");
        public uint GetCurrentProcessId() => 0;
        public void StartProcess(string path, IEnumerable<string> arguments, string directory, bool showWindow) =>
            throw new InvalidOperationException("Regression checks must never execute the native updater.");
        public void Exit(int exitCode) => throw new InvalidOperationException("Regression checks must never apply an update.");
    }

    private sealed class UninstalledCheckLocator : TestVelopackLocator
    {
        public UninstalledCheckLocator() : base("VBAudioMonitor.Check", "1.1.0", Environment.CurrentDirectory) { }
        public override SemanticVersion? CurrentlyInstalledVersion => null;
        public override bool IsPortable => true;
    }

    private sealed class CheckUpdateManager : UpdateManager
    {
        public UpdateInfo? Available;
        public bool FailDownload;
        public int Checks, Downloads;
        public Task<UpdateInfo?>? DelayedCheck;
        public CheckUpdateManager() : base("https://example.invalid", locator: new TestVelopackLocator("VBAudioMonitor", "1.1.0", Path.Combine(Environment.CurrentDirectory, "verification-update-cache"))) { }
        public override VelopackAsset? UpdatePendingRestart => null;
        public override Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            Checks++;
            return DelayedCheck ?? Task.FromResult(Available);
        }
        public override Task DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)
        {
            Downloads++;
            if (FailDownload) return Task.FromException(new IOException("TEST-DOWNLOAD-FAILURE"));
            progress?.Invoke(100);
            return Task.CompletedTask;
        }
    }

    private static void CheckUpdates()
    {
        string oldFolder = Path.Combine(Environment.CurrentDirectory, "old-portable");
        string userFolder = Path.Combine(Environment.CurrentDirectory, "fake-user-data");
        Require(PreferencesStorage.ResolveStoragePath(oldFolder, userFolder, false) == Path.Combine(oldFolder, "settings.json"), "Portable settings moved unexpectedly");
        Require(PreferencesStorage.ResolveStoragePath(oldFolder, userFolder, true) == Path.Combine(userFolder, "VBAudioMonitor", "settings.json"), "Installed settings remain in the replaceable application folder");
        using var previewUpdates = new AppUpdates(new UpdateManager("https://example.invalid", locator: new UninstalledCheckLocator()));
        Require(!previewUpdates.CanUpdate, "Uninstalled verification process unexpectedly enabled live updates");
        previewUpdates.CheckAndDownloadAsync().GetAwaiter().GetResult();
        Require(!previewUpdates.ReadyToRestart && !previewUpdates.IsBusy, "Portable preview attempted an update");
        var updates = Task.Run(async () =>
        {
            var manager = new CheckUpdateManager();
            var service = new AppUpdates(manager);
            Require(service.CanUpdate, "Mock installed edition cannot update");
            await service.CheckAndDownloadAsync();
            Require(!service.IsBusy && !service.ReadyToRestart && manager.Downloads == 0, "An up-to-date app attempted a download");
            var delayed = new TaskCompletionSource<UpdateInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
            manager.DelayedCheck = delayed.Task;
            var first = service.CheckAndDownloadAsync();
            int checks = manager.Checks;
            await service.CheckAndDownloadAsync();
            Require(service.IsBusy && manager.Checks == checks, "Repeated update checks started concurrent operations");
            delayed.SetException(new IOException("TEST-OFFLINE"));
            await first;
            Require(!service.IsBusy && !service.ReadyToRestart && service.StatusText.Contains("TEST-OFFLINE"), "Offline failure blocked retry or lost details");
            manager.DelayedCheck = null;
            manager.Available = new UpdateInfo(new VelopackAsset { Version = SemanticVersion.Parse("1.2.0"), Type = VelopackAssetType.Full, FileName = "VBAudioMonitor-1.2.0-full.nupkg" }, false);
            manager.FailDownload = true;
            await service.CheckAndDownloadAsync();
            Require(!service.IsBusy && !service.ReadyToRestart && service.StatusText.Contains("TEST-DOWNLOAD-FAILURE"), "Failed download became installable");
            bool blocked = false;
            try { service.RestartAfterExit(); } catch (InvalidOperationException) { blocked = true; }
            Require(blocked, "An unverified update could be applied");
            manager.FailDownload = false;
            await service.CheckAndDownloadAsync();
            Require(!service.IsBusy && service.ReadyToRestart && service.StatusText.Contains("1.2.0"), "Completed update was not retained for restart");
            checks = manager.Checks;
            await service.CheckAndDownloadAsync();
            Require(manager.Checks == checks, "A ready update was downloaded again");
            return service;
        }).GetAwaiter().GetResult();
        using (updates)
        try
        {
            foreach (bool dark in new[] { false, true })
            foreach (string language in new[] { "en", "fr", "zh-CN" })
            {
                Theme.SetDark(dark); UiText.SetLanguage(language);
                using var dialog = new UpdateForm(updates) { Opacity = 0, ShowInTaskbar = false };
                dialog.Show(); dialog.Update();
                var buttons = Descendants(dialog).OfType<ActionButton>().ToArray();
                Require(buttons.Single(button => button.DialogResult == DialogResult.OK).Enabled && !buttons.Single(button => button.DialogResult == DialogResult.None).Enabled,
                    "Ready update dialog does not enable installation or disables the wrong action");
                foreach (var control in Descendants(dialog).Where(control => control is Label or ActionButton))
                {
                    using var graphics = control.CreateGraphics();
                    var measured = TextRenderer.MeasureText(graphics, control.Text, control.Font, new Size(control.Width, int.MaxValue),
                        TextFormatFlags.NoPrefix | (control is ActionButton ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak));
                    Require(measured.Width <= control.Width && measured.Height <= control.Height, "Update dialog clips translated text: " + control.Text);
                }
                using var image = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                image.Save($"verification-update-{language}{(dark ? "-dark" : "")}.png", ImageFormat.Png);
            }
        }
        finally { Theme.SetDark(false); UiText.SetLanguage("zh-CN"); }
    }

    private static void CheckUpdateFeed(string feedPath)
    {
        string feed = Path.GetFullPath(feedPath);
        string cache = Path.Combine(Environment.CurrentDirectory, "verification-update-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        try
        {
            Task.Run(async () =>
            {
                var locator = new SignedCheckLocator(cache);
                var signedManager = new SignedUpdateManager(locator, feed);
                using var updates = new AppUpdates(signedManager);
                await updates.CheckAndDownloadAsync();
                Require(updates.ReadyToRestart && !updates.IsBusy, "Published package download or verification failed: " + updates.StatusText);
                var downloaded = Directory.GetFiles(cache, "*.nupkg");
                Require(downloaded.Length == 1, "Update download did not prepare one full package");
                Require(signedManager.UpdatePendingRestart != null, "Verified cached package was not available after restart");
                string proofPath = downloaded[0] + ".sig";
                byte[] proof = File.ReadAllBytes(proofPath);
                File.WriteAllBytes(proofPath, proof[..63]);
                Require(signedManager.UpdatePendingRestart == null, "Truncated cached signature became installable after restart");
                bool applyBlocked = false;
                try { updates.RestartAfterExit(); } catch (InvalidDataException) { applyBlocked = true; }
                Require(applyBlocked, "Changed cached signature did not block the apply entry point");
                File.WriteAllBytes(proofPath, proof);
                File.Delete(proofPath);
                Require(signedManager.UpdatePendingRestart == null, "Unsigned cached package became installable");
                File.WriteAllBytes(proofPath, proof);
                using (var archive = System.IO.Compression.ZipFile.OpenRead(downloaded[0]))
                {
                    Require(archive.Entries.Any(entry => entry.FullName.EndsWith("/VBAudioMonitor.exe", StringComparison.OrdinalIgnoreCase))
                        && archive.Entries.All(entry => !entry.FullName.EndsWith("/settings.json", StringComparison.OrdinalIgnoreCase)),
                        "Update package is missing the executable or includes personal settings");
                }
                using (var tamperCache = new FileStream(downloaded[0], FileMode.Open, FileAccess.ReadWrite))
                {
                    int original = tamperCache.ReadByte();
                    tamperCache.Position = 0; tamperCache.WriteByte((byte)(original ^ 1));
                    tamperCache.Flush();
                }
                Require(signedManager.UpdatePendingRestart == null, "Changed cached package became installable after restart");
                bool changedPackageBlocked = false;
                try { updates.RestartAfterExit(); } catch (CryptographicException) { changedPackageBlocked = true; }
                Require(changedPackageBlocked, "Changed cached package passed the apply gate");
                using (var restoreCache = new FileStream(downloaded[0], FileMode.Open, FileAccess.ReadWrite))
                {
                    int changed = restoreCache.ReadByte();
                    restoreCache.Position = 0; restoreCache.WriteByte((byte)(changed ^ 1));
                }
                string badFeed = Path.Combine(cache, "corrupt-feed"), badCache = Path.Combine(cache, "corrupt-cache");
                Directory.CreateDirectory(badFeed); Directory.CreateDirectory(badCache);
                File.Copy(Path.Combine(feed, "releases.win.json"), Path.Combine(badFeed, "releases.win.json"));
                File.Copy(Path.Combine(feed, Path.GetFileName(downloaded[0]) + ".sig"),
                    Path.Combine(badFeed, Path.GetFileName(downloaded[0]) + ".sig"));
                string damaged = Path.Combine(badFeed, Path.GetFileName(downloaded[0]));
                File.Copy(downloaded[0], damaged);
                using (var tamper = new FileStream(damaged, FileMode.Open, FileAccess.ReadWrite))
                {
                    int original = tamper.ReadByte(); tamper.Position = 0; tamper.WriteByte((byte)(original ^ 1));
                }
                string updaterPath = Path.Combine(badCache, "Update.exe");
                File.WriteAllText(updaterPath, "existing updater must remain unchanged");
                var badLocator = new SignedCheckLocator(badCache);
                using var rejected = new AppUpdates(new SignedUpdateManager(badLocator, badFeed));
                await rejected.CheckAndDownloadAsync();
                Require(!rejected.ReadyToRestart && !rejected.IsBusy && Directory.GetFiles(badCache, "*.nupkg").Length == 0,
                    "A package with a damaged checksum was made installable");
                Require(File.ReadAllText(updaterPath) == "existing updater must remain unchanged",
                    "An unauthenticated package replaced the native updater");
                var forgedFeed = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(badFeed, "releases.win.json")))!;
                using (var damagedStream = File.OpenRead(damaged))
                    forgedFeed["Assets"]![0]!["SHA256"] = Convert.ToHexString(SHA256.HashData(damagedStream));
                File.WriteAllText(Path.Combine(badFeed, "releases.win.json"), forgedFeed.ToJsonString());
                await rejected.CheckAndDownloadAsync();
                Require(!rejected.ReadyToRestart && !rejected.IsBusy && Directory.GetFiles(badCache, "*.nupkg").Length == 0,
                    "Changing the package and feed hash bypassed publisher authentication");
                string signaturePath = Path.Combine(badFeed, Path.GetFileName(downloaded[0]) + ".sig");
                File.Delete(signaturePath);
                await rejected.CheckAndDownloadAsync();
                Require(!rejected.ReadyToRestart && !rejected.IsBusy, "Missing publisher signature was accepted");
                File.WriteAllBytes(signaturePath, new byte[65]);
                await rejected.CheckAndDownloadAsync();
                Require(!rejected.ReadyToRestart && !rejected.IsBusy, "Oversized publisher signature was accepted");
                Require(File.ReadAllText(updaterPath) == "existing updater must remain unchanged",
                    "A forged feed or signature replaced the native updater");
            }).GetAwaiter().GetResult();
        }
        finally
        {
            Require(Path.GetFullPath(cache).StartsWith(Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Update check cache escaped the workspace");
            Directory.Delete(cache, recursive: true);
        }
    }

    private static void CheckFormDisposal()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var form = new MonitorForm(overrideDark: false) { Opacity = 0, ShowInTaskbar = false };
        form.Show();
        var relayField = typeof(MonitorForm).GetField("activeRelay", flags)!;
        var relay = new AudioRelay(); // No Start call: this check never opens an audio stream.
        relayField.SetValue(form, relay);
        var timer = (System.Windows.Forms.Timer)typeof(MonitorForm).GetField("telemetryTimer", flags)!.GetValue(form)!;
        timer.Start();
        var tray = typeof(MonitorForm).GetField("trayManager", flags)!.GetValue(form)!;
        var menu = (ContextMenuStrip)typeof(TrayManager).GetField("trayMenu", flags)!.GetValue(tray)!;
        form.Close();
        Require(!form.IsDisposed && !form.Visible && ReferenceEquals(relayField.GetValue(form), relay),
            "Closing the main window no longer preserves monitoring in the tray");
        form.Dispose();
        Require(relayField.GetValue(form) == null && !timer.Enabled && menu.IsDisposed,
            "Disposing the main window did not release its relay, timer and tray menu");
        form.Dispose();
    }

    private static void CheckIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream("VBAudioMonitor.assets.app.ico")!;
        using var reader = new BinaryReader(stream);
        Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1, "Application icon has an invalid ICO header");
        int count = reader.ReadUInt16();
        var frames = new List<(int Size, int Length, int Offset)>();
        for (int index = 0; index < count; index++)
        {
            int width = reader.ReadByte(); int height = reader.ReadByte();
            if (width == 0) width = 256;
            if (height == 0) height = 256;
            reader.ReadByte(); reader.ReadByte(); reader.ReadUInt16();
            Require(width == height && reader.ReadUInt16() == 32, "Icon frame is not square with 32-bit color");
            frames.Add((width, reader.ReadInt32(), reader.ReadInt32()));
        }
        Require(new[] { 16, 32, 48, 256 }.All(size => frames.Any(frame => frame.Size == size)), "Icon lacks tray, taskbar or high-resolution frames");
        foreach (var frame in frames)
        {
            Require(frame.Offset >= 6 + count * 16 && frame.Length > 0 && frame.Offset + frame.Length <= stream.Length, "Icon frame points outside the file");
            stream.Position = frame.Offset;
            using var imageData = new MemoryStream(reader.ReadBytes(frame.Length));
            using var image = new Bitmap(imageData);
            Require(image.Width == frame.Size && image.Height == frame.Size && image.GetPixel(0, 0).A == 0,
                "Icon frame dimensions or transparent corners are invalid");
        }
    }

    private static void CheckProfileNameForm()
    {
        using (var empty = new ProfileNameForm())
        {
            var controls = Descendants(empty).ToArray();
            var input = controls.OfType<TextBox>().Single(control => control.AccessibleName == "方案名称");
            var save = controls.OfType<ActionButton>().Single(button => button.DialogResult == DialogResult.OK);
            Require(input.Text == "" && empty.ProfileName == "" && !save.Enabled, "New profile dialog did not start with an empty disabled name");
        }
        bool previousDark = Theme.IsDark;
        try
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme.SetDark(dark);
                using var dialog = new ProfileNameForm("网页耳机") { Opacity = 0 };
                dialog.Show(); dialog.Update();
                var visible = Descendants(dialog).ToArray();
                var name = visible.OfType<TextBox>().Single(control => control.AccessibleName == "方案名称");
                var saveAction = visible.OfType<ActionButton>().Single(button => button.DialogResult == DialogResult.OK);
                var cancel = visible.OfType<ActionButton>().Single(button => button.DialogResult == DialogResult.Cancel);
                Require(name.Text == "网页耳机" && dialog.ProfileName == "网页耳机" && saveAction.Enabled && name.MaxLength == 24
                    && ReferenceEquals(dialog.AcceptButton, saveAction) && ReferenceEquals(dialog.CancelButton, cancel),
                    "Existing profile dialog did not preserve its default name and keyboard actions");
                foreach (string invalidName in new[] { "", "   ", "方案\t名称" })
                {
                    name.Text = invalidName;
                    Require(!saveAction.Enabled, "Profile name dialog allowed an empty or control-character name");
                }
                name.Text = "  新方案  ";
                Require(saveAction.Enabled && dialog.ProfileName == "新方案", "Profile name dialog did not accept and trim a valid name");
                name.Text = new string('名', name.MaxLength);
                using (var graphics = name.CreateGraphics())
                {
                    var measured = TextRenderer.MeasureText(graphics, name.Text, name.Font, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    Require(saveAction.Enabled && measured.Width <= name.ClientSize.Width && measured.Height <= name.ClientSize.Height,
                        $"Longest profile name is clipped: dark={dark}, measured={measured}, available={name.ClientSize}");
                }
                name.Text = "网页耳机"; dialog.Update();
                using var screenshot = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(screenshot, new Rectangle(Point.Empty, dialog.Size));
                screenshot.Save(dark ? "verification-profile-name-dark.png" : "verification-profile-name.png", ImageFormat.Png);
                Require(dialog.BackColor == Theme.Background && name.BackColor == Theme.Input && name.ForeColor == Theme.Ink,
                    "Profile name dialog did not apply the current theme to its input");
                foreach (var control in visible.Where(control => control.Visible))
                {
                    var bounds = control.RectangleToScreen(control.ClientRectangle);
                    Require(bounds.Width > 0 && bounds.Height > 0 && dialog.RectangleToScreen(dialog.ClientRectangle).Contains(bounds)
                        && control.Parent!.RectangleToScreen(control.Parent.ClientRectangle).Contains(bounds),
                        $"Profile name dialog clips a visible control: type={control.GetType().Name}, text={control.Text}, bounds={bounds}");
                }
                Require(name.Height >= name.Font.Height
                    && name.RectangleToScreen(name.ClientRectangle).Bottom <= saveAction.RectangleToScreen(saveAction.ClientRectangle).Top
                    && !saveAction.RectangleToScreen(saveAction.ClientRectangle).IntersectsWith(cancel.RectangleToScreen(cancel.ClientRectangle)),
                    "Profile name input or save/cancel actions are clipped or overlap");
                foreach (var control in visible.Where(control => control is Label or ActionButton))
                {
                    bool singleLine = control is ActionButton || !control.Text.Contains('\n');
                    var flags = TextFormatFlags.NoPrefix | (singleLine ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak);
                    using var graphics = control.CreateGraphics();
                    var measured = TextRenderer.MeasureText(graphics, control.Text, control.Font,
                        new Size(singleLine ? int.MaxValue : control.ClientSize.Width, int.MaxValue), flags);
                    Require(measured.Width <= control.ClientSize.Width && measured.Height <= control.ClientSize.Height,
                        $"Profile name dialog text is clipped: dark={dark}, text={control.Text}, measured={measured}, available={control.ClientSize}, dpi={control.DeviceDpi}");
                }
            }
        }
        finally { Theme.SetDark(previousDark); }
    }

    private static void CheckTrayProfiles()
    {
        using var host = new Form();
        using var icon = Theme.CreateIcon();
        using var tray = new TrayManager(host, icon);
        var menu = (ToolStripMenuItem)(typeof(TrayManager).GetField("profilesMenu", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(tray)
            ?? throw new InvalidOperationException("Tray profiles menu is missing"));
        Require(menu.DropDownItems.Count == 1 && !menu.DropDownItems[0].Enabled, "Empty tray profile menu did not show a disabled placeholder");
        var first = new ListeningProfile("耳机方案", "__check_tray_source__", "__check_tray_target_a__", 0, 100, 320);
        var second = first with { Name = "A&B", Target = "__check_tray_target_b__", Volume = 640 };
        ListeningProfile? selected = null;
        tray.ProfileSelected += profile => selected = profile;
        tray.UpdateProfiles([first, second], first);
        Require(menu.DropDownItems.Count == 2 && ((ToolStripMenuItem)menu.DropDownItems[0]).Checked
            && !((ToolStripMenuItem)menu.DropDownItems[1]).Checked && menu.DropDownItems[1].Text == "A&&B",
            "Tray profiles did not preserve selected state or escape the display name");
        menu.DropDownItems[1].PerformClick();
        Require(selected == second, "Tray profile selection did not send the requested profile");
        tray.UpdateProfiles([]);
        Require(menu.DropDownItems.Count == 1 && !menu.DropDownItems[0].Enabled && selected == second,
            "Clearing tray profiles left stale actions or triggered a selection");
    }

    private static void CheckMissingRouteRecovery(MonitorForm form, Control[] controls, ActionButton master)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var missingSource = new Endpoint("__check_missing_source__", "CABLE 检查来源", 2, 48000);
        var missingTarget = new Endpoint("__check_missing_target__", "检查耳机", 2, 48000);
        Require(Endpoint.List().All(endpoint => endpoint.Id != missingSource.Id && endpoint.Id != missingTarget.Id),
            "Recovery check identifiers unexpectedly match a real endpoint");
        var routeType = typeof(MonitorForm).GetNestedType("ListeningRoute", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Recovery route type is missing");
        var route = Activator.CreateInstance(routeType, missingSource, missingTarget, 0, 100)
            ?? throw new InvalidOperationException("Recovery route could not be created");
        var timer = (System.Windows.Forms.Timer)(typeof(MonitorForm).GetField("recoveryTimer", flags)?.GetValue(form)
            ?? throw new InvalidOperationException("Recovery timer is missing"));
        var selectors = controls.OfType<DeviceSelector>().Where(selector => selector.AccessibleName
            is "声音来源" or "监听目标" or "监听声道" or "音频缓冲").ToArray();
        typeof(MonitorForm).GetMethod("BeginRecovery", flags)!.Invoke(form, [route, "等待不存在的检查设备", 2000]);
        Require(master.Text == "停止等待" && master.Enabled && timer.Enabled && selectors.All(selector => !selector.Enabled),
            "Recovery waiting state did not lock route selectors and expose cancellation");
        typeof(MonitorForm).GetMethod("TryRecoverRoute", flags)!.Invoke(form, null);
        Require(master.Text == "停止等待" && timer.Enabled
            && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null,
            "Missing original endpoints incorrectly resumed audio");
        master.PerformClick();
        Require(master.Text == "开始监听" && !timer.Enabled && selectors.All(selector => selector.Enabled)
            && typeof(MonitorForm).GetField("recoveryRoute", flags)!.GetValue(form) == null
            && typeof(MonitorForm).GetField("activeRelay", flags)!.GetValue(form) == null,
            "Cancel recovery did not stop waiting and restore idle controls");
    }

    private static void CheckDiagnostics()
    {
        var missing = AudioDiagnostics.Run(null, null, 0, 750, false);
        Require(missing.NeedsAttention && missing.Details.Contains("声音来源未选择") && missing.Details.Contains("监听设备未选择"),
            "Diagnostics did not explain missing source and output selections");
        var muted = AudioDiagnostics.Run(null, null, 0, 0, false, "检查历史错误");
        Require(muted.NeedsAttention && muted.Details.Contains("当前监听已静音") && muted.Details.Contains("检查历史错误")
            && muted.Details.Contains("不会播放测试声音或改变设置"), "Read-only diagnostics lost mute or historical error guidance");
        var report = new DiagnosticReport("有几项需要确认",
            string.Join(Environment.NewLine, Enumerable.Repeat(muted.Details, 3)) + "\n检查结束标记", true);
        foreach (bool dark in new[] { false, true })
        {
            Theme.SetDark(dark);
            using var dialog = new DiagnosticsForm(report) { Opacity = 0 };
            dialog.Show(); dialog.Update();
            var controls = Descendants(dialog).ToArray();
            var details = controls.OfType<RichTextBox>().Single(control => control.AccessibleName == "音频检查详情");
            var headline = controls.OfType<Label>().Single(control => control.AccessibleName == "诊断结果");
            var close = controls.OfType<ActionButton>().Single();
            using var normal = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(normal, new Rectangle(Point.Empty, dialog.Size));
            normal.Save(dark ? "verification-diagnostics-dark.png" : "verification-diagnostics-light.png", ImageFormat.Png);
            Require(details.ReadOnly && details.WordWrap && details.ScrollBars == RichTextBoxScrollBars.Vertical
                && details.Text.Replace("\r\n", "\n").TrimEnd('\n') == report.Details.Replace("\r\n", "\n").TrimEnd('\n')
                && details.BackColor == Theme.Surface && details.ForeColor == Theme.Ink,
                $"Diagnostics content is not readable, read-only, scrollable, or correctly themed: readonly={details.ReadOnly}, wrap={details.WordWrap}, scroll={details.ScrollBars}, font={details.Font.SizeInPoints}, background={details.BackColor}, foreground={details.ForeColor}, expectedBackground={Theme.Surface}, expectedForeground={Theme.Ink}");
            using (var bodyGraphics = details.CreateGraphics())
            using (var actionGraphics = close.CreateGraphics())
            {
                var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                int bodyHeight = TextRenderer.MeasureText(bodyGraphics, "正文 Ag", details.Font, new Size(int.MaxValue, int.MaxValue), flags).Height;
                int actionHeight = TextRenderer.MeasureText(actionGraphics, "正文 Ag", close.Font, new Size(int.MaxValue, int.MaxValue), flags).Height;
                Require(bodyHeight >= actionHeight * 0.65f && bodyHeight <= actionHeight * 1.25f,
                    $"Diagnostics body text is too small or disproportionate to its action: bodyHeight={bodyHeight}, actionHeight={actionHeight}, dpi={dialog.DeviceDpi}");
            }
            dialog.Size = dialog.MinimumSize;
            using var largeFont = new Font(details.Font.FontFamily, details.Font.Size * 1.3f, details.Font.Style, details.Font.Unit);
            details.Font = largeFont;
            dialog.PerformLayout(); dialog.Update();
            details.SelectionStart = details.TextLength; details.ScrollToCaret();
            using var narrow = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(narrow, new Rectangle(Point.Empty, dialog.Size));
            narrow.Save(dark ? "verification-diagnostics-dark-narrow.png" : "verification-diagnostics-light-narrow.png", ImageFormat.Png);
            var contentBounds = details.RectangleToScreen(details.ClientRectangle);
            var headlineBounds = headline.RectangleToScreen(headline.ClientRectangle);
            var closeBounds = close.RectangleToScreen(close.ClientRectangle);
            Require(dialog.RectangleToScreen(dialog.ClientRectangle).Contains(contentBounds)
                && details.Parent!.RectangleToScreen(details.Parent.ClientRectangle).Contains(contentBounds)
                && dialog.RectangleToScreen(dialog.ClientRectangle).Contains(closeBounds)
                && headlineBounds.Bottom <= contentBounds.Top && contentBounds.Bottom <= closeBounds.Top
                && headline.Height >= headline.Font.Height && details.Height >= details.Font.Height * 3,
                $"Narrow diagnostics clips or overlaps its heading, content, or close action: heading={headlineBounds}, details={contentBounds}, close={closeBounds}");
            foreach (var control in controls.Where(control => control is Label or ActionButton))
            {
                using var graphics = control.CreateGraphics();
                var measured = TextRenderer.MeasureText(graphics, control.Text, control.Font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                Require(measured.Width <= control.ClientSize.Width && measured.Height <= control.ClientSize.Height,
                    $"Diagnostics title, summary, or action text is clipped: dark={dark}, text={control.Text}, measured={measured}, available={control.ClientSize}, dpi={control.DeviceDpi}");
            }
            var lastText = details.GetPositionFromCharIndex(details.Text.LastIndexOf("检查结束标记", StringComparison.Ordinal) + "检查结束标记".Length - 1);
            Require(details.ClientRectangle.Contains(lastText) && lastText.Y + details.Font.Height <= details.ClientSize.Height,
                "Large diagnostics text cannot scroll its final line into view");
        }
        Theme.SetDark(false);
    }

    private static void CheckGainSmoothing()
    {
        var input = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(1000, 2)) { ReadFully = false };
        float[] units = Enumerable.Repeat(1f, 5000).ToArray();
        byte[] bytes = new byte[units.Length * 4]; Buffer.BlockCopy(units, 0, bytes, 0, bytes.Length);
        input.AddSamples(bytes, 0, bytes.Length);
        var samples = input.ToSampleProvider();
        var smooth = new SmoothGainProvider(samples, 1f);
        float[] ramp = Enumerable.Repeat(-1f, 408).ToArray();
        Require(smooth.Read(ramp, 3, 402) == 402 && ramp[2] == -1 && ramp[405] == -1,
            "Smooth gain wrote outside the requested sample range");
        Require(ramp[3] == 0 && ramp[403] == 1 && ramp[203] > 0 && ramp[203] < 1,
            "Startup gain did not fade in from zero to its target");
        for (int i = 3; i < 405; i += 2)
            Require(ramp[i] == ramp[i + 1] && float.IsFinite(ramp[i]) && ramp[i] is >= 0 and <= 1
                && (i == 3 || ramp[i] >= ramp[i - 2]), "Startup ramp broke stereo frame consistency or monotonicity");
        smooth.SetVolume(0.5f);
        float[] reduced = Enumerable.Repeat(-1f, 66).ToArray();
        Require(smooth.Read(reduced, 2, 62) == 62 && reduced[1] == -1 && reduced[64] == -1,
            "Gain change wrote outside the requested sample range");
        Require(reduced[2] == 1 && reduced[4] < 1 && reduced[4] > 0.5f && reduced[62] == 0.5f,
            "Target gain change jumped instead of ramping smoothly");
        for (int i = 2; i < 64; i += 2)
            Require(reduced[i] == reduced[i + 1] && (i == 2 || reduced[i] <= reduced[i - 2]),
                "Changed gain broke stereo frame consistency or monotonicity");
        smooth.SetVolume(0);
        float[] frame = [-1f, -1f];
        Require(smooth.Read(frame, 0, 2) == 2 && frame.All(value => value == 0), "Mute did not take effect on the next frame");
        foreach (float invalidGain in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            smooth.SetVolume(invalidGain);
            Require(smooth.Read(frame, 0, 2) == 2 && frame.All(value => value == 0), "Invalid gain was not clamped to silence");
        }
        smooth.SetVolume(3f);
        float[] limited = new float[62];
        Require(smooth.Read(limited, 0, limited.Length) == limited.Length
            && limited.All(value => float.IsFinite(value) && value is >= 0 and <= 1) && limited[^1] == 1,
            "Excessive target gain was not clamped to one");
        foreach (float initialGain in new[] { float.NaN, -1f, 3f })
        {
            var initial = new SmoothGainProvider(samples, initialGain);
            float[] initialRamp = new float[402];
            Require(initial.Read(initialRamp, 0, initialRamp.Length) == initialRamp.Length
                && initialRamp.All(value => float.IsFinite(value) && value is >= 0 and <= 1)
                && initialRamp[^1] == (initialGain == 3f ? 1f : 0f), "Initial gain was not clamped safely");
        }
        var partialInput = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(1000, 2)) { ReadFully = false };
        partialInput.AddSamples(bytes, 0, 12);
        var partial = new SmoothGainProvider(partialInput.ToSampleProvider(), 1f);
        float[] shortRead = Enumerable.Repeat(-1f, 10).ToArray();
        Require(partial.Read(shortRead, 2, 6) == 3 && shortRead[1] == -1 && shortRead[5] == -1
            && shortRead[2] == shortRead[3] && shortRead[4] > 0, "Partial source read damaged frame or buffer boundaries");
        partial.SetVolume(0);
        partialInput.AddSamples(bytes, 0, 4);
        Require(partial.Read(frame, 1, 1) == 1 && frame[1] == shortRead[4], "Mid-frame mute changed the right channel gain");
        partialInput.AddSamples(bytes, 0, 8);
        Require(partial.Read(frame, 0, 2) == 2 && frame.All(value => value == 0), "Mute did not begin after the partial frame");
        float[] end = [-1f, -1f];
        Require(partial.Read(end, 0, 2) == 0 && end.All(value => value == -1), "End-of-stream modified the output buffer");
    }

    private static object TestRelay(Endpoint input, Endpoint output)
    {
        using (var invalid = new AudioRelay())
        {
            try { invalid.Start(input, input, 0, 100, 1); throw new Exception("Feedback route accepted"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("真实耳机")) { }
        }
        using var enumerator = new MMDeviceEnumerator();
        using var physical = enumerator.GetDevice(output.Id);
        using var cable = enumerator.GetDevice(input.Id);
        using var observer = new WasapiLoopbackCapture(physical);
        observer.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(output.SampleRate, 2);
        var observed = new BufferedWaveProvider(observer.WaveFormat)
        { BufferDuration = TimeSpan.FromSeconds(5), ReadFully = false };
        Exception? error = null;
        observer.DataAvailable += (_, e) => observed.AddSamples(e.Buffer, 0, e.BytesRecorded);
        observer.RecordingStopped += (_, e) => { if (e.Exception != null) error = e.Exception; };
        using var relay = new AudioRelay();
        relay.Failed += ex => error = ex;
        observer.StartRecording();
        relay.Start(input, output, 0, 100, 0.2f);
        float peak = 0;
        using (var tone = new WasapiOut(cable, AudioClientShareMode.Shared, true, 100))
        {
            var generator = new SignalGenerator(input.SampleRate == 48000 ? 44100 : 48000, 2)
            { Frequency = 997, Gain = 0.02, Type = SignalGeneratorType.Sin };
            tone.Init(generator.ToWaveProvider()); tone.Play();
            for (int tick = 0; tick < 16; tick++)
            { Thread.Sleep(100); peak = Math.Max(peak, relay.InputPeak); }
            tone.Stop();
            Thread.Sleep(200);
        }
        relay.Dispose(); observer.Dispose();
        Require(error == null, "Audio engine error: " + error);
        Require(relay.BytesCaptured > 0, "Cable loopback received no audio");
        Require(peak > 0.001f, "Input level meter did not detect the test tone");
        // The physical device's loopback must contain our 997 Hz tone, not just bytes.
        var samples = observed.ToSampleProvider();
        float[] buffer = new float[4096];
        double real = 0, imaginary = 0;
        long frames = 0;
        int read;
        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i + 1 < read; i += 2, frames++)
            {
                double angle = 2 * Math.PI * 997 * frames / output.SampleRate;
                real += buffer[i] * Math.Cos(angle); imaginary += buffer[i] * Math.Sin(angle);
            }
        }
        double amplitude = frames == 0 ? 0 : 2 * Math.Sqrt(real * real + imaginary * imaginary) / frames;
        Require(amplitude > 0.0001, $"Tone did not reach physical output; amplitude={amplitude}");
        // Re-opening the same endpoints catches stale COM clients and shutdown races.
        using var restarted = new AudioRelay();
        restarted.Start(input, output, 0, 60, 0);
        Thread.Sleep(200);
        return new { Source = input.Name, Target = output.Name, Passed = true, input.Channels, CapturedBytes = relay.BytesCaptured, ToneAmplitude = amplitude, InputPeak = peak, Restarted = true };
    }
}
