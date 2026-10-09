using System.Globalization;

namespace VBAudioMonitor;

internal sealed record UiLanguage(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal static class UiText
{
    public static readonly UiLanguage[] Languages = [new("zh-CN", "简体中文"), new("en", "English"), new("fr", "Français")];
    public static string Language { get; private set; } = "zh-CN";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language);

    private static readonly Dictionary<string, (string English, string French)> Texts = new()
    {
        ["VB 音频监听"] = ("VB Audio Monitor", "VB Audio Monitor"),
        ["软件更新"] = ("Software updates", "Mise à jour"),
        ["检查更新"] = ("Check for updates", "Vérifier"),
        ["安装更新"] = ("Install update", "Installer la màj"),
        ["立即安装"] = ("Install now", "Installer"),
        ["当前版本：{0}"] = ("Current version: {0}", "Version actuelle : {0}"),
        ["请安装安装版以启用自动更新。"] = ("Install the setup edition to enable automatic updates.", "Installez la version avec installateur pour activer les mises à jour automatiques."),
        ["正在检查更新…"] = ("Checking for updates…", "Recherche de mises à jour…"),
        ["正在下载更新：{0}%"] = ("Downloading update: {0}%", "Téléchargement : {0}%"),
        ["已是最新版本：{0}"] = ("You are up to date: {0}", "Vous êtes à jour : {0}"),
        ["更新 {0} 已准备好，下次启动时自动安装。"] = ("Update {0} is ready and will install on the next launch.", "La mise à jour {0} est prête et sera installée au prochain démarrage."),
        ["更新失败，可稍后重试。\n{0}"] = ("Update failed. You can try again later.\n{0}", "La mise à jour a échoué. Réessayez plus tard.\n{0}"),
        ["后台下载，下次启动自动安装。\n立即安装会保存设置、停止监听并重启程序。"] = ("Downloads run in the background and install on the next launch.\nInstall now saves settings, stops monitoring and restarts the app.", "Téléchargement en arrière-plan, installation au prochain démarrage.\nInstaller sauvegarde les réglages, arrête l’écoute et redémarre l’application."),
        ["设置尚未保存，更新已取消。请检查用户数据目录是否可写。"] = ("Settings have not been saved, so the update was cancelled. Check that the user data folder is writable.", "Les réglages ne sont pas sauvegardés : mise à jour annulée. Vérifiez les droits d’écriture du dossier utilisateur."),
        ["让虚拟线路的声音，回到你的耳机。"] = ("Hear your virtual cable through your headphones.", "Écoutez votre câble virtuel dans votre casque."),
        ["深色模式"] = ("Dark mode", "Mode sombre"),
        ["浅色模式"] = ("Light mode", "Mode clair"),
        ["刷新设备"] = ("Refresh", "Actualiser"),
        ["隐藏到托盘"] = ("Hide to tray", "Masquer"),
        ["退出程序"] = ("Exit app", "Quitter"),
        ["声音路由"] = ("Audio routing", "Routage audio"),
        ["音频路由"] = ("Audio routing", "Routage audio"),
        ["保存方案"] = ("Save preset", "Enregistrer"),
        ["删除"] = ("Delete", "Supprimer"),
        ["删除方案"] = ("Delete preset", "Supprimer un préréglage"),
        ["场景方案"] = ("Presets", "Préréglages"),
        ["当前配置"] = ("Current settings", "Réglages actuels"),
        ["网页扬声器与声音来源请选择同一个 CABLE。"] = ("Use the same CABLE in your browser and audio source.", "Choisissez le même CABLE dans le navigateur et la source."),
        ["声音来源 · 虚拟线路"] = ("Audio source · Virtual cable", "Source audio · Câble virtuel"),
        ["声音来源"] = ("Audio source", "Source audio"),
        ["与网页选项一致"] = ("Match your browser's output device", "Même sortie que le navigateur"),
        ["↓  声音转发到"] = ("↓  Forward audio to", "↓  Transmettre le son vers"),
        ["监听到 · 耳机或音箱"] = ("Monitor output · Headphones or speakers", "Sortie d'écoute · Casque ou enceintes"),
        ["监听目标"] = ("Monitor output", "Sortie d'écoute"),
        ["真实物理输出设备"] = ("Physical output device", "Périphérique de sortie physique"),
        ["输入电平"] = ("Input level", "Niveau d'entrée"),
        ["立体声双轨电平表"] = ("Stereo input level meter", "Vu-mètre stéréo d'entrée"),
        ["高级设置 · 声道与缓冲 ▾"] = ("Advanced · Channels and buffer ▾", "Avancé · Canaux et tampon ▾"),
        ["收起高级设置 ▴"] = ("Hide advanced settings ▴", "Masquer les réglages avancés ▴"),
        ["高级设置"] = ("Advanced settings", "Réglages avancés"),
        ["监听声道"] = ("Monitor channels", "Canaux d'écoute"),
        ["通常选择声道 1 / 2"] = ("Channels 1 / 2 usually work best", "Canaux 1 / 2 recommandés"),
        ["音频缓冲"] = ("Audio buffer", "Tampon audio"),
        ["有卡顿时选 200 毫秒"] = ("Choose 200 ms if audio stutters", "Choisissez 200 ms si le son coupe"),
        ["60 毫秒 · 低延迟"] = ("60 ms · Low latency", "60 ms · Faible latence"),
        ["100 毫秒 · 平衡"] = ("100 ms · Balanced", "100 ms · Équilibré"),
        ["200 毫秒 · 稳定"] = ("200 ms · Stable", "200 ms · Stable"),
        ["单声道 → 双耳"] = ("Mono → Both ears", "Mono → Deux oreilles"),
        ["声道 {0} / {1}"] = ("Channels {0} / {1}", "Canaux {0} / {1}"),
        [" · 推荐"] = (" · Recommended", " · Recommandé"),
        ["{0:0.#} kHz · {1} 声道"] = ("{0:0.#} kHz · {1} channels", "{0:0.#} kHz · {1} canaux"),
        ["{0:0.#} kHz · 物理输出"] = ("{0:0.#} kHz · Physical output", "{0:0.#} kHz · Sortie physique"),
        ["监听控制"] = ("Monitor controls", "Contrôle de l'écoute"),
        ["调整音量只影响你的监听输出"] = ("Volume affects only your monitor output", "Le volume agit uniquement sur votre écoute"),
        ["监听音量"] = ("Monitor volume", "Volume d'écoute"),
        ["静音"] = ("Mute", "Muet"),
        ["取消静音"] = ("Unmute", "Réactiver le son"),
        ["静音切换"] = ("Toggle mute", "Activer ou désactiver le son"),
        ["检查音频"] = ("Check audio", "Vérifier l'audio"),
        ["开始监听"] = ("Start monitoring", "Démarrer l'écoute"),
        ["停止监听"] = ("Stop monitoring", "Arrêter l'écoute"),
        ["停止等待"] = ("Cancel recovery", "Annuler la reprise"),
        ["开始或停止监听"] = ("Start or stop monitoring", "Démarrer ou arrêter l'écoute"),
        ["缓冲时长"] = ("Queued audio", "Audio en attente"),
        ["缓冲重置"] = ("Buffer resets", "Réinitialisations du tampon"),
        ["0 次"] = ("0", "0"),
        ["{0} 次"] = ("{0}", "{0}"),
        ["关闭→托盘 · 退出→停止监听"] = ("Close → tray · Exit → stop and quit", "Fermer → masquer · Quitter → arrêter"),
        ["请选择设备"] = ("Select a device", "Choisissez un périphérique"),
        ["界面语言"] = ("Interface language", "Langue de l'interface"),
        ["选择界面语言，立即生效；不会停止监听。"] = ("Change the interface language without stopping audio.", "Changez la langue sans interrompre l'écoute."),
        ["选择方案会先停止监听，应用后点击开始监听。"] = ("Choosing a preset stops monitoring. Click Start after applying it.", "Un préréglage arrête l'écoute. Cliquez sur Démarrer après son application."),
        ["保存当前设备、声道、缓冲和音量；同名方案会更新。"] = ("Save devices, channels, buffer and volume. An existing name updates that preset.", "Enregistrez les périphériques, canaux, tampon et volume. Un même nom met à jour le préréglage."),
        ["精准监听增益：仅作用于物理耳机，不影响虚拟线路原声。"] = ("Monitor volume affects your headphones, not the original virtual cable audio.", "Le volume d'écoute agit sur votre casque, pas sur le son original du câble virtuel."),
        ["若遇到偶发爆音或卡顿，请尝试切换至 200 毫秒缓冲。"] = ("If audio crackles or stutters, stop and try a 200 ms buffer.", "Si le son crépite ou coupe, arrêtez puis essayez un tampon de 200 ms."),
        ["删除场景方案“{0}”？"] = ("Delete preset “{0}”?", "Supprimer le préréglage « {0} » ?"),
        ["删除场景方案"] = ("Delete preset", "Supprimer un préréglage"),
        ["保存场景方案"] = ("Save preset", "Enregistrer un préréglage"),
        ["保存当前线路、声道、缓冲与监听音量。\n名称限 1–24 个字符，同名方案会更新。"] = ("Save devices, channels, buffer and monitor volume.\nUse 1–24 characters. An existing name updates that preset.", "Enregistrez périphériques, canaux, tampon et volume.\n1–24 caractères. Un même nom remplace le préréglage."),
        ["方案名称"] = ("Preset name", "Nom du préréglage"),
        ["取消"] = ("Cancel", "Annuler"),
        ["关闭"] = ("Close", "Fermer"),
        ["方案名称无效"] = ("Invalid preset name", "Nom de préréglage non valide"),
        ["请输入 1 到 24 个字符，名称不能包含换行等控制字符。"] = ("Enter 1–24 characters without line breaks or control characters.", "Saisissez 1 à 24 caractères, sans saut de ligne ni caractère de contrôle."),
        ["当前配置无法保存"] = ("Cannot save these settings", "Impossible d'enregistrer ces réglages"),
        ["确认设备可用并选择有效声道后，再保存方案。"] = ("Select available devices and valid channels before saving.", "Choisissez des périphériques disponibles et des canaux valides avant d'enregistrer."),
        ["方案已保存"] = ("Preset saved", "Préréglage enregistré"),
        ["“{0}”已保存，可从场景方案或托盘菜单切换。"] = ("“{0}” is saved. Select it from Presets or the tray menu.", "« {0} » est enregistré. Sélectionnez-le dans les préréglages ou le menu de notification."),
        ["方案配置无效"] = ("Invalid preset settings", "Préréglage non valide"),
        ["此方案的设备或参数无效，请重新保存一个有效方案。"] = ("This preset contains invalid devices or settings. Save a valid preset again.", "Les périphériques ou paramètres de ce préréglage sont non valides. Enregistrez un nouveau préréglage."),
        ["方案已应用"] = ("Preset applied", "Préréglage appliqué"),
        ["“{0}”已就绪，确认输出设备后点击开始监听。"] = ("“{0}” is ready. Confirm the output device, then click Start monitoring.", "« {0} » est prêt. Vérifiez la sortie puis démarrez l'écoute."),
        ["方案声道不可用"] = ("Preset channels unavailable", "Canaux du préréglage indisponibles"),
        ["此设备已不支持保存的声道，请在高级设置中重新选择。"] = ("The saved channels are no longer supported. Choose another pair in Advanced settings.", "Les canaux enregistrés ne sont plus disponibles. Choisissez une autre paire dans les réglages avancés."),
        ["原设备已连接，暂时无法打开。每 5 秒重试，可点击停止等待取消。"] = ("The original devices are connected but cannot open yet. Retrying every 5 seconds; cancel recovery to stop.", "Les périphériques sont reconnectés mais ne peuvent pas encore s'ouvrir. Nouvelle tentative toutes les 5 secondes ; annulez la reprise pour arrêter."),
        ["请与网页选择同一个 CABLE 输入；关闭其他软件的独占模式后再开始。"] = ("Use the same CABLE input as your browser. Disable exclusive mode in other apps, then retry.", "Choisissez la même entrée CABLE que le navigateur. Désactivez le mode exclusif des autres applications puis réessayez."),
        ["确认设备已连接，点击刷新设备，再检查网页与这里的 CABLE 名称一致。"] = ("Reconnect the devices and refresh. Match the browser's CABLE name to the audio source.", "Reconnectez les périphériques et actualisez. Vérifiez que le nom du CABLE correspond à celui du navigateur."),
        ["无法开始监听"] = ("Cannot start monitoring", "Impossible de démarrer l'écoute"),
        ["原设备重新连接后会自动恢复监听；点击停止等待可取消。"] = ("Monitoring resumes when the original devices reconnect. Cancel recovery to stop waiting.", "L'écoute reprend dès que les périphériques d'origine sont reconnectés. Annulez la reprise pour arrêter l'attente."),
        ["监听已中断"] = ("Monitoring interrupted", "Écoute interrompue"),
        ["请重新连接设备，点击刷新设备后，再开始监听。"] = ("Reconnect the devices, refresh, then start monitoring again.", "Reconnectez les périphériques, actualisez puis redémarrez l'écoute."),
        ["等待恢复监听"] = ("Waiting to reconnect", "En attente de reconnexion"),
        ["原声道不可用"] = ("Original channels unavailable", "Canaux d'origine indisponibles"),
        ["设备格式发生变化，请重新选择监听声道后开始。"] = ("The device format changed. Choose valid monitor channels before restarting.", "Le format du périphérique a changé. Sélectionnez des canaux valides avant de redémarrer."),
        ["无法恢复连接"] = ("Cannot restore the connection", "Impossible de rétablir la connexion"),
        ["请点击检查音频，确认设备和 Windows 音频服务后重新开始。"] = ("Check audio, confirm the devices and Windows audio service, then restart.", "Vérifiez l'audio, les périphériques et le service audio Windows, puis redémarrez."),
        ["等待音频服务恢复"] = ("Waiting for the audio service", "En attente du service audio"),
        ["每 5 秒重试原连接；点击停止等待可取消。"] = ("Retrying the original connection every 5 seconds. Cancel recovery to stop.", "Nouvelle tentative de connexion toutes les 5 secondes. Annulez la reprise pour arrêter."),
        ["音频设备已断开。"] = ("The audio device disconnected.", "Le périphérique audio a été déconnecté."),
        ["扬声器静音"] = ("Output device muted", "Sortie audio muette"),
        ["请在系统任务栏解除耳机静音"] = ("Unmute your headphones in Windows.", "Réactivez le son de votre casque dans Windows."),
        ["虚拟线路静音"] = ("Virtual cable muted", "Câble virtuel muet"),
        ["请核查网页播放音量并取消静音"] = ("Check the browser volume and unmute it.", "Vérifiez le volume du navigateur et réactivez le son."),
        ["输出已静音"] = ("Monitor output muted", "Écoute muette"),
        ["请推高右侧增益推子即可听到声音"] = ("Raise the monitor volume to hear audio.", "Augmentez le volume d'écoute pour entendre le son."),
        ["等待网页声音"] = ("Waiting for audio", "En attente de son"),
        ["播放网页声音，并确认网页扬声器与声音来源选择同一个 CABLE。"] = ("Play audio and use the same CABLE in your browser and audio source.", "Lancez le son et choisissez le même CABLE dans le navigateur et la source."),
        ["监听已连接"] = ("Monitor connected", "Écoute connectée"),
        ["线路已连接，开始播放网页声音即可。"] = ("The cable is connected. Start playing audio in your browser.", "Le câble est connecté. Lancez le son dans votre navigateur."),
        ["实时监听中"] = ("Monitoring audio", "Écoute en cours"),
        ["声音已稳定转发至耳机，保持后台运行"] = ("Audio is being forwarded to your headphones. Keep the app running.", "Le son est transmis à votre casque. Laissez l'application ouverte."),
        ["\n设置未能保存，请检查设置目录是否可写。"] = ("\nSettings could not be saved. Check that the settings folder is writable.", "\nRéglages non enregistrés. Vérifiez les droits d’écriture du dossier des réglages."),
        ["声音来源不可用"] = ("Audio source unavailable", "Source audio indisponible"),
        ["未找到 VB-CABLE。确认已安装并启用后，点击刷新设备。"] = ("VB-CABLE was not found. Install and enable it, then refresh devices.", "VB-CABLE est introuvable. Installez-le et activez-le, puis actualisez."),
        ["请选择与网页扬声器同名的 CABLE；重新连接设备后可点击刷新。"] = ("Select the CABLE used by your browser. Refresh after reconnecting a device.", "Choisissez le CABLE utilisé par le navigateur. Actualisez après une reconnexion."),
        ["监听设备不可用"] = ("Output device unavailable", "Sortie audio indisponible"),
        ["连接耳机或扬声器后点击刷新设备，或重新选择一个真实输出设备。"] = ("Connect your headphones or speakers and refresh, or choose another physical output.", "Connectez un casque ou des enceintes puis actualisez, ou choisissez une autre sortie physique."),
        ["监听声道不可用"] = ("Monitor channels unavailable", "Canaux d'écoute indisponibles"),
        ["请展开高级设置，重新选择此设备支持的监听声道。"] = ("Open Advanced settings and select channels supported by this device.", "Ouvrez les réglages avancés et choisissez les canaux pris en charge."),
        ["监听音量已静音"] = ("Monitor volume muted", "Volume d'écoute muet"),
        ["可开始监听；需要听到声音时，请调高音量或点击取消静音。"] = ("Monitoring can start. Raise the volume or unmute when you want to hear audio.", "Vous pouvez démarrer l'écoute. Augmentez le volume ou réactivez le son pour l'entendre."),
        ["准备就绪"] = ("Ready to monitor", "Prêt pour l'écoute"),
        ["确认网页选择同一个 CABLE，然后点击开始监听。"] = ("Use the same CABLE in your browser, then click Start monitoring.", "Choisissez le même CABLE dans le navigateur puis démarrez l'écoute."),
        ["确认设备后点击开启监听"] = ("Confirm your devices, then start monitoring", "Vérifiez les périphériques puis démarrez l'écoute"),
        ["读取设备失败"] = ("Could not read devices", "Lecture des périphériques impossible"),
        ["请确认 Windows 音频服务和设备可用，再点击刷新设备。"] = ("Check the Windows audio service and devices, then refresh.", "Vérifiez le service audio Windows et les périphériques puis actualisez."),
        ["需处理"] = ("Attention", "À vérifier"),
        ["就绪"] = ("Ready", "Prêt"),
        ["● 就绪"] = ("● Ready", "● Prêt"),
        ["已保存"] = ("Saved", "Enregistré"),
        ["等待设备"] = ("Waiting", "En attente"),
        ["等待恢复"] = ("Recovering", "Reprise"),
        ["已静音"] = ("Muted", "Muet"),
        ["等待声音"] = ("No audio", "Sans signal"),
        ["已连接"] = ("Connected", "Connecté"),
        ["中继中"] = ("Live", "En cours"),
        ["显示主界面"] = ("Show window", "Afficher la fenêtre"),
        ["快速静音"] = ("Mute", "Couper le son"),
        ["切换场景方案"] = ("Switch preset", "Changer de préréglage"),
        ["退出"] = ("Exit", "Quitter"),
        ["尚未保存方案"] = ("No saved presets", "Aucun préréglage enregistré"),
        ["VB 音频监听 (等待设备)"] = ("VB Audio Monitor (waiting for devices)", "VB Audio Monitor (attente des périphériques)"),
        ["VB 音频监听 (运行中)"] = ("VB Audio Monitor (running)", "VB Audio Monitor (en cours)"),
        ["VB 音频监听 (就绪)"] = ("VB Audio Monitor (ready)", "VB Audio Monitor (prêt)"),
        ["音频检查"] = ("Audio diagnostics", "Diagnostic audio"),
        ["诊断结果"] = ("Diagnostic result", "Résultat du diagnostic"),
        ["音频检查详情"] = ("Audio diagnostic details", "Détails du diagnostic audio"),
        ["监听状态：已建立监听连接。"] = ("Monitoring: the connection is active.", "Écoute : connexion active."),
        ["监听状态：当前未建立监听连接，可能尚未开始或正在等待设备恢复。"] = ("Monitoring: no active connection. Monitoring may be stopped or waiting for device recovery.", "Écoute : aucune connexion active. L'écoute est arrêtée ou attend la reconnexion des périphériques."),
        ["程序监听音量：{0:0.0}%"] = ("Monitor volume: {0:0.0}%", "Volume d'écoute : {0:0.0} %"),
        ["当前监听已静音。需要听到声音时，请调高音量或取消静音。"] = ("Monitoring is muted. Raise the volume or unmute to hear audio.", "L'écoute est muette. Augmentez le volume ou réactivez le son."),
        ["监听音量配置超出范围，请重新调整音量滑块。"] = ("The monitor volume is out of range. Adjust the volume slider.", "Le volume d'écoute est hors limites. Réglez le curseur de volume."),
        ["\n声音来源未选择：请选择与网页扬声器同名的 CABLE。"] = ("\nNo audio source selected: choose the CABLE used by your browser.", "\nAucune source sélectionnée : choisissez le CABLE utilisé par le navigateur."),
        ["\n声音来源应选择网页使用的虚拟线路，通常是 CABLE。"] = ("\nThe audio source must be the virtual cable used by your browser, usually CABLE.", "\nLa source doit être le câble virtuel utilisé par le navigateur, généralement CABLE."),
        ["\n监听设备未选择：连接耳机或音箱后选择一个真实输出设备。"] = ("\nNo monitor output selected: connect headphones or speakers and choose a physical output.", "\nAucune sortie sélectionnée : connectez un casque ou des enceintes et choisissez une sortie physique."),
        ["\n监听到应选择真实耳机或音箱，不能再输出回虚拟线路。"] = ("\nChoose physical headphones or speakers for monitoring. Do not route audio back to the virtual cable.", "\nChoisissez un casque ou des enceintes physiques. Ne renvoyez pas le son vers le câble virtuel."),
        ["\n无法读取 Windows 音频设备。请确认 Windows 音频服务正常。\n"] = ("\nCannot read Windows audio devices. Check that the Windows audio service is running.\n", "\nLecture des périphériques audio Windows impossible. Vérifiez le service audio Windows.\n"),
        ["\n网页设置"] = ("\nBrowser settings", "\nRéglages du navigateur"),
        ["网页扬声器和程序声音来源应选择同一个 CABLE。"] = ("Your browser output and this app's audio source must use the same CABLE.", "La sortie du navigateur et la source de l'application doivent utiliser le même CABLE."),
        ["请确认网页的扬声器选择同一个设备：\n"] = ("Set your browser's output to this device:\n", "Choisissez ce périphérique comme sortie du navigateur :\n"),
        ["网页暂停播放时没有信号属于正常情况；本次检查只读取当前状态，不会播放测试声音或改变设置。"] = ("No signal is normal when playback is paused. This check only reads the current state; it does not play sounds or change settings.", "L'absence de signal est normale quand la lecture est en pause. Ce diagnostic lit uniquement l'état actuel, sans jouer de son ni modifier les réglages."),
        ["\n最近一次错误（历史记录，不代表当前仍有故障）\n"] = ("\nLast error (history; it may already be resolved)\n", "\nDernière erreur (historique ; elle peut déjà être résolue)\n"),
        ["有几项需要确认"] = ("Some items need attention", "Certains points sont à vérifier"),
        ["未发现明显的设备或静音问题"] = ("No obvious device or mute issues found", "Aucun problème apparent de périphérique ou de son muet"),
        ["设备可用，当前未建立监听连接"] = ("Devices available; monitoring is not connected", "Périphériques disponibles ; écoute non connectée"),
        ["监听设备"] = ("Monitor output", "Sortie d'écoute"),
        ["已在 Windows 中禁用"] = ("Disabled in Windows", "Désactivé dans Windows"),
        ["未连接"] = ("Disconnected", "Déconnecté"),
        ["设备不存在"] = ("Device not present", "Périphérique absent"),
        ["暂时不可用"] = ("Temporarily unavailable", "Temporairement indisponible"),
        ["状态："] = ("State: ", "État : "),
        ["。请重新连接或启用设备。"] = (". Reconnect or enable the device.", ". Reconnectez ou activez le périphérique."),
        ["状态：已连接。"] = ("State: connected.", "État : connecté."),
        ["格式：{0:0.#} kHz · {1} 声道"] = ("Format: {0:0.#} kHz · {1} channels", "Format : {0:0.#} kHz · {1} canaux"),
        ["Windows 音量：{0:0}% · {1}"] = ("Windows volume: {0:0}% · {1}", "Volume Windows : {0:0} % · {1}"),
        ["未静音"] = ("Not muted", "Son activé"),
        ["请在 Windows 音量设置中取消此设备的静音，并调高音量。"] = ("Unmute this device in Windows volume settings and raise its volume.", "Réactivez le son de ce périphérique dans les réglages Windows et augmentez le volume."),
        ["监听声道不适用于当前设备。请在高级设置中重新选择声道。"] = ("The selected channels are invalid for this device. Select another pair in Advanced settings.", "Les canaux sélectionnés ne sont pas valides pour ce périphérique. Choisissez une autre paire dans les réglages avancés."),
        ["监听声道：单声道送到双耳。"] = ("Monitor channels: mono to both ears.", "Canaux d'écoute : mono vers les deux oreilles."),
        ["监听声道：{0} / {1}"] = ("Monitor channels: {0} / {1}", "Canaux d'écoute : {0} / {1}"),
        ["瞬时电平：{0:0.0} dBFS"] = ("Instant level: {0:0.0} dBFS", "Niveau instantané : {0:0.0} dBFS"),
        ["瞬时电平：当前未检测到信号。若网页正在播放，请核对网页 CABLE 和监听声道。"] = ("Instant level: no signal detected. If your browser is playing, check its CABLE device and monitor channels.", "Niveau instantané : aucun signal. Si le navigateur joue du son, vérifiez son CABLE et les canaux d'écoute."),
        ["瞬时电平：当前未检测到输出声音。"] = ("Instant level: no output audio detected.", "Niveau instantané : aucun son de sortie détecté."),
        ["输出电平也可能包含其他程序的声音，不能单独证明本程序的转发结果。"] = ("Output levels may include audio from other apps and cannot alone confirm this app's relay output.", "Le niveau de sortie peut inclure le son d'autres applications ; il ne suffit pas à confirmer le relais audio de cette application."),
        ["瞬时电平暂时无法读取；请结合主界面的电平和当前网页播放状态判断。"] = ("Instant levels are unavailable. Compare the main window's meter with your browser playback.", "Le niveau instantané est indisponible. Comparez le vu-mètre principal avec la lecture du navigateur."),
        ["设备检查未完成。请重新连接设备，再点击检查音频。\n"] = ("Device check incomplete. Reconnect the device, then check audio again.\n", "Diagnostic incomplet. Reconnectez le périphérique puis vérifiez l'audio à nouveau.\n"),
        ["监听已经启动。"] = ("Monitoring has already started.", "L'écoute est déjà démarrée."),
        ["监听目标请选择真实耳机或扬声器，不能选回虚拟线路。"] = ("Choose physical headphones or speakers as the output, not a virtual cable.", "Choisissez un casque ou des enceintes physiques comme sortie, pas un câble virtuel."),
        ["音频格式必须包含有效的声道和采样率。"] = ("The audio format must have valid channels and a sample rate.", "Le format audio doit contenir des canaux et une fréquence d'échantillonnage valides."),
        ["VB 音频监听已经在运行中，请查看任务栏或右下角系统托盘。"] = ("VB Audio Monitor is already running. Check the taskbar or notification area.", "VB Audio Monitor est déjà ouvert. Consultez la barre des tâches ou la zone de notification."),
        ["程序已在运行"] = ("Already running", "Application déjà ouverte"),
        ["程序发生未处理异常：\n\n"] = ("An unexpected error occurred:\n\n", "Une erreur inattendue s'est produite :\n\n"),
        ["运行错误"] = ("Application error", "Erreur de l'application")
    };

    public static void SetLanguage(string? language) => Language = Languages.Any(item => item.Code == language) ? language! : "zh-CN";

    public static string T(string source, params object?[] arguments)
    {
        string text = Texts.TryGetValue(source, out var translations)
            ? Language switch { "en" => translations.English, "fr" => translations.French, _ => source } : source;
        return arguments.Length == 0 ? text : string.Format(Culture, text, arguments);
    }

    public static void Bind(Control control, string source)
    {
        control.Tag = source;
        control.Text = T(source);
    }

    public static void Apply(Control control)
    {
        if (control.Tag == null && control is Form or Label or Button or BadgeBox && Texts.ContainsKey(control.Text))
            control.Tag = control.Text;
        if (control.Tag is string source && Texts.ContainsKey(source)) control.Text = T(source);
        if (!string.IsNullOrEmpty(control.AccessibleName))
        {
            if (string.IsNullOrEmpty(control.Name)) control.Name = control.AccessibleName;
            control.AccessibleName = T(control.Name);
        }
        foreach (Control child in control.Controls) Apply(child);
        control.Invalidate();
    }

    internal static IReadOnlyDictionary<string, (string English, string French)> Translations => Texts;
}
