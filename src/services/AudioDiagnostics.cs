using NAudio.CoreAudioApi;

namespace VBAudioMonitor;

internal sealed record DiagnosticReport(string Headline, string Details, bool NeedsAttention);

internal static class AudioDiagnostics
{
    public static DiagnosticReport Run(Endpoint? source, Endpoint? target, int channelOffset,
        int gain, bool isRunning, string? lastError = null)
    {
        var details = new List<string>();
        bool needsAttention = false;
        details.Add(isRunning ? UiText.T("监听状态：已建立监听连接。") : UiText.T("监听状态：当前未建立监听连接，可能尚未开始或正在等待设备恢复。"));
        details.Add(UiText.T("程序监听音量：{0:0.0}%", gain / 10f));
        if (gain <= 0 || gain > 1000)
        {
            needsAttention = true;
            details.Add(gain == 0 ? UiText.T("当前监听已静音。需要听到声音时，请调高音量或取消静音。") : UiText.T("监听音量配置超出范围，请重新调整音量滑块。"));
        }
        if (source == null)
        {
            needsAttention = true;
            details.Add(UiText.T("\n声音来源未选择：请选择与网页扬声器同名的 CABLE。"));
        }
        else if (!source.IsVirtual)
        {
            needsAttention = true;
            details.Add(UiText.T("\n声音来源应选择网页使用的虚拟线路，通常是 CABLE。"));
        }
        if (target == null)
        {
            needsAttention = true;
            details.Add(UiText.T("\n监听设备未选择：连接耳机或音箱后选择一个真实输出设备。"));
        }
        else if (target.IsVirtual || target.Id == source?.Id)
        {
            needsAttention = true;
            details.Add(UiText.T("\n监听到应选择真实耳机或音箱，不能再输出回虚拟线路。"));
        }

        if (source != null || target != null)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                if (source != null) InspectDevice(enumerator, source, true);
                if (target != null) InspectDevice(enumerator, target, false);
            }
            catch (Exception error)
            {
                needsAttention = true;
                details.Add(UiText.T("\n无法读取 Windows 音频设备。请确认 Windows 音频服务正常。\n") + error.Message);
            }
        }

        details.Add(UiText.T("\n网页设置"));
        details.Add(source == null ? UiText.T("网页扬声器和程序声音来源应选择同一个 CABLE。")
            : UiText.T("请确认网页的扬声器选择同一个设备：\n") + source.Name);
        details.Add(UiText.T("网页暂停播放时没有信号属于正常情况；本次检查只读取当前状态，不会播放测试声音或改变设置。"));
        if (!string.IsNullOrWhiteSpace(lastError))
            details.Add(UiText.T("\n最近一次错误（历史记录，不代表当前仍有故障）\n") + lastError);

        string headline = needsAttention ? UiText.T("有几项需要确认")
            : isRunning ? UiText.T("未发现明显的设备或静音问题") : UiText.T("设备可用，当前未建立监听连接");
        return new DiagnosticReport(headline, string.Join(Environment.NewLine, details), needsAttention);

        void InspectDevice(MMDeviceEnumerator enumerator, Endpoint endpoint, bool isSource)
        {
            details.Add("\n" + (isSource ? UiText.T("声音来源") : UiText.T("监听设备")) + "\n" + endpoint.Name);
            try
            {
                using var device = enumerator.GetDevice(endpoint.Id);
                if (device.State != DeviceState.Active)
                {
                    needsAttention = true;
                    string state = device.State switch
                    {
                        DeviceState.Disabled => UiText.T("已在 Windows 中禁用"),
                        DeviceState.Unplugged => UiText.T("未连接"),
                        DeviceState.NotPresent => UiText.T("设备不存在"),
                        _ => UiText.T("暂时不可用")
                    };
                    details.Add(UiText.T("状态：") + state + UiText.T("。请重新连接或启用设备。"));
                    return;
                }
                details.Add(UiText.T("状态：已连接。"));
                using var audioClient = device.AudioClient;
                var format = audioClient.MixFormat;
                details.Add(UiText.T("格式：{0:0.#} kHz · {1} 声道", format.SampleRate / 1000d, format.Channels));
                var volume = device.AudioEndpointVolume;
                details.Add(UiText.T("Windows 音量：{0:0}% · {1}", volume.MasterVolumeLevelScalar * 100, volume.Mute ? UiText.T("已静音") : UiText.T("未静音")));
                if (volume.Mute || volume.MasterVolumeLevelScalar <= 0)
                {
                    needsAttention = true;
                    details.Add(UiText.T("请在 Windows 音量设置中取消此设备的静音，并调高音量。"));
                }
                bool validPair = channelOffset >= 0 && channelOffset % 2 == 0 && channelOffset < format.Channels
                    && (format.Channels == 1 || channelOffset + 1 < format.Channels);
                if (isSource)
                {
                    if (!validPair)
                    {
                        needsAttention = true;
                        details.Add(UiText.T("监听声道不适用于当前设备。请在高级设置中重新选择声道。"));
                    }
                    else details.Add(format.Channels == 1 ? UiText.T("监听声道：单声道送到双耳。") : UiText.T("监听声道：{0} / {1}", channelOffset + 1, channelOffset + 2));
                }
                try
                {
                    var meter = device.AudioMeterInformation;
                    float peak = meter.MasterPeakValue;
                    if (isSource && validPair)
                    {
                        var values = meter.PeakValues;
                        if (channelOffset < values.Count)
                            peak = Math.Max(values[channelOffset], channelOffset + 1 < values.Count ? values[channelOffset + 1] : values[channelOffset]);
                    }
                    details.Add(peak >= 0.0001f ? UiText.T("瞬时电平：{0:0.0} dBFS", 20 * Math.Log10(peak))
                        : isSource ? UiText.T("瞬时电平：当前未检测到信号。若网页正在播放，请核对网页 CABLE 和监听声道。")
                        : UiText.T("瞬时电平：当前未检测到输出声音。"));
                    if (!isSource) details.Add(UiText.T("输出电平也可能包含其他程序的声音，不能单独证明本程序的转发结果。"));
                }
                catch (Exception)
                {
                    details.Add(UiText.T("瞬时电平暂时无法读取；请结合主界面的电平和当前网页播放状态判断。"));
                }
            }
            catch (Exception error)
            {
                needsAttention = true;
                details.Add(UiText.T("设备检查未完成。请重新连接设备，再点击检查音频。\n") + error.Message);
            }
        }
    }
}
