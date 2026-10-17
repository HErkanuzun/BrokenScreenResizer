using System.Collections.Generic;
using System.Globalization;

namespace EkranKurtar
{
    // Dil desteği: Türkçe, English, 中文, Русский
    // Kodda metinler L.T("Türkçe", "English") olarak yazılır; Çince ve Rusça İngilizce metin anahtarıyla aşağıdaki tablolardan bulunur.
    // Yeni dil eklemek için: Codes/Names dizilerine ekle ve İngilizce anahtarlı yeni bir tablo yaz.
    static class L
    {
        public static readonly string[] Codes = { "tr", "en", "zh", "ru" };
        public static readonly string[] Names = { "Türkçe", "English", "中文", "Русский" };

        public static string Lang = Detect();

        public static string Detect()
        {
            string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (var k in Codes) if (k == c) return c;
            return "en";
        }

        public static void Set(string code)
        {
            foreach (var k in Codes) if (k == code) { Lang = code; return; }
            Lang = Detect();
        }

        public static string T(string tr, string en)
        {
            if (Lang == "tr") return tr;
            Dictionary<string, string> d = Lang == "zh" ? Zh : Lang == "ru" ? Ru : null;
            string v;
            if (d != null && d.TryGetValue(en, out v)) return v;
            return en;
        }

        public static string FontName
        {
            get { return Lang == "zh" ? "Microsoft YaHei UI" : "Segoe UI"; }
        }

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            { "Keep changes", "保留更改" },
            { "Revert", "还原" },
            { "Is the desktop now shown only on the working part?\n\nReverting in {0} seconds unless you confirm.", "桌面现在是否只显示在完好的区域？\n\n如果不确认，将在 {0} 秒后还原。" },
            { "success", "成功" },
            { "access denied (5)", "访问被拒绝 (5)" },
            { "driver error (31)", "驱动程序错误 (31)" },
            { "not supported (50)", "不支持 (50)" },
            { "invalid parameter (87)", "参数无效 (87)" },
            { "bad configuration (1610)", "配置无效 (1610)" },
            { "error code ", "错误代码 " },
            { "(unnamed monitor)", "（未命名显示器）" },
            { "Could not read monitor modes.", "无法读取显示器模式。" },
            { "Your display driver does not expose the desktop image region.", "你的显卡驱动不提供桌面图像区域设置。" },
            { "Could not read display config: ", "无法读取显示配置：" },
            { "Monitor not found.", "未找到显示器。" },
            { "Trying: desktop ", "尝试：桌面 " },
            { "image ", "图像 " },
            { "region ", "区域 " },
            { "Scaling ", "缩放 " },
            { " validate: ", " 验证：" },
            { " apply: ", " 应用：" },
            { "The driver rejected this setting. Nothing was changed.", "驱动程序拒绝了此设置。未做任何更改。" },
            { "Requested: ", "请求的区域：" },
            { "Applied: ", "实际应用：" },
            { "The driver overrode the position; driver mode does not work on this system.", "驱动程序改变了位置；驱动模式在此系统上无法使用。" },
            { "No cropped monitors.", "没有被裁剪的显示器。" },
            { "Hide the broken part of your monitor from Windows", "让 Windows 忽略显示器的损坏部分" },
            { "Monitor:", "显示器：" },
            { "Left", "左" },
            { "Top", "上" },
            { "Right", "右" },
            { "Bottom", "下" },
            { "Broken edge:", "损坏的边缘：" },
            { "Area to hide (px):", "隐藏区域（像素）：" },
            { "Show the hidden area in red (while adjusting)", "用红色显示隐藏区域（调整时）" },
            { "Driver mode (recommended): Windows sees a smaller monitor, everything incl. games works", "驱动模式（推荐）：Windows 将其视为更小的显示器，包括游戏在内一切正常" },
            { "Software mode: covers the broken area, keeps windows and mouse out", "软件模式：遮盖损坏区域，使窗口和鼠标远离该区域" },
            { "Keep the mouse out of the broken area", "禁止鼠标进入损坏区域" },
            { "Push overflowing windows into the working area", "将超出的窗口推回完好区域" },
            { "Method:", "方式：" },
            { "Apply", "应用" },
            { "Status:", "状态：" },
            { "Start with Windows", "开机自动启动" },
            { "Emergency: reset all monitors", "紧急情况：重置所有显示器" },
            { "Driver mode active. Windows sees this monitor as a smaller screen. Stays even if the app is closed.", "驱动模式已启用。Windows 将此显示器视为更小的屏幕。关闭程序后设置仍然保留。" },
            { "Software mode active. The app must keep running in the tray.", "软件模式已启用。程序需要在系统托盘中保持运行。" },
            { "This monitor was cropped earlier. Press Revert to restore full screen.", "此显示器之前已被裁剪。按“还原”恢复全屏。" },
            { "Not active. Adjust until the red area covers the broken part, then press Apply.", "未启用。调整直到红色区域覆盖损坏部分，然后按“应用”。" },
            { "You can try software mode instead.", "你可以改用软件模式。" },
            { "Could not save: ", "无法保存：" },
            { "Could not revert: ", "无法还原：" },
            { "You can set the recommended resolution in Windows Display Settings.", "你可以在 Windows 显示设置中选择推荐的分辨率。" },
            { "The configured monitor is not connected.", "已配置的显示器未连接。" },
            { "Could not start software mode.", "无法启动软件模式。" },
            { "Display setting re-applied.", "显示设置已重新应用。" },
            { "Could not re-apply. Open the app and try again.", "无法重新应用。请打开程序再试一次。" },
            { "Ekran Kurtar keeps running in the tray.", "Ekran Kurtar 将继续在系统托盘中运行。" },
            { "Open", "打开" },
            { "Exit", "退出" },
            { "Ekran Kurtar is already running (tray icon).", "Ekran Kurtar 已在运行（托盘图标）。" },
            { "Language:", "语言：" },
                    { "Broken", "损坏" },
            { "What Windows will see", "Windows 看到的屏幕" },
            { "Active", "已启用" },
            { "Monitor", "显示器" },
            { "Broken edge", "损坏的边缘" },
            { "Area to hide (px)", "隐藏区域（像素）" },
            { "Show the hidden area in red on screen", "在屏幕上用红色显示隐藏区域" },
            { "Method", "方式" },
            { "Driver mode", "驱动模式" },
            { "Recommended", "推荐" },
            { "Windows sees a smaller monitor. Fullscreen and games only use the working area.", "Windows 将其视为更小的显示器。全屏和游戏只使用完好的区域。" },
            { "Software mode", "软件模式" },
            { "Covers the broken area and keeps windows and the mouse out. Works everywhere.", "遮盖损坏区域，使窗口和鼠标远离。适用于所有系统。" },
            { "Does it look right?", "显示正确吗？" },
            { "Attention", "注意" },
            { "OK", "确定" },
            { "HOW IT WORKS", "工作原理" },
            { "The mouse skips the broken area", "鼠标跳过损坏区域" },
            { "Fullscreen = working area", "全屏 = 完好区域" },
            { "Monitors join seamlessly", "显示器无缝衔接" },
            { "Windows stay out", "窗口不会进入" },
            { "Everything is reversible", "一切都可撤销" },
            { "The cursor goes straight from one monitor's edge to the working part of the next. No dead zone in between.", "光标从一个显示器的边缘直接进入下一个显示器的完好部分，中间没有死区。" },
            { "Maximized windows, videos and games only fill the working part.", "最大化的窗口、视频和游戏只占用完好的部分。" },
            { "Windows ignores the broken part and places the monitors edge to edge.", "Windows 忽略损坏部分，并将显示器边缘对齐。" },
            { "In software mode, windows that spill into the broken area are pushed back automatically.", "在软件模式下，进入损坏区域的窗口会被自动推回。" },
            { "Every change needs confirmation. Without it, it reverts within 15 seconds.", "每次更改都需要确认。若不确认，将在 15 秒内还原。" },
            { "Fullscreen", "全屏" },
            { "Reverted", "已还原" },
            { "Open source · MIT license", "开源 · MIT 许可证" },
        };

        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            { "Keep changes", "Сохранить" },
            { "Revert", "Отменить" },
            { "Is the desktop now shown only on the working part?\n\nReverting in {0} seconds unless you confirm.", "Рабочий стол теперь отображается только на исправной части?\n\nБез подтверждения изменения будут отменены через {0} с." },
            { "success", "успешно" },
            { "access denied (5)", "доступ запрещён (5)" },
            { "driver error (31)", "ошибка драйвера (31)" },
            { "not supported (50)", "не поддерживается (50)" },
            { "invalid parameter (87)", "неверный параметр (87)" },
            { "bad configuration (1610)", "неверная конфигурация (1610)" },
            { "error code ", "код ошибки " },
            { "(unnamed monitor)", "(монитор без имени)" },
            { "Could not read monitor modes.", "Не удалось прочитать режимы монитора." },
            { "Your display driver does not expose the desktop image region.", "Драйвер видеокарты не поддерживает область изображения рабочего стола." },
            { "Could not read display config: ", "Не удалось прочитать конфигурацию дисплеев: " },
            { "Monitor not found.", "Монитор не найден." },
            { "Trying: desktop ", "Попытка: рабочий стол " },
            { "image ", "изображение " },
            { "region ", "область " },
            { "Scaling ", "Масштабирование " },
            { " validate: ", " проверка: " },
            { " apply: ", " применение: " },
            { "The driver rejected this setting. Nothing was changed.", "Драйвер отклонил эту настройку. Ничего не изменено." },
            { "Requested: ", "Запрошено: " },
            { "Applied: ", "Применено: " },
            { "The driver overrode the position; driver mode does not work on this system.", "Драйвер изменил положение; режим драйвера не работает на этой системе." },
            { "No cropped monitors.", "Нет обрезанных мониторов." },
            { "Hide the broken part of your monitor from Windows", "Скрыть повреждённую часть монитора от Windows" },
            { "Monitor:", "Монитор:" },
            { "Left", "Слева" },
            { "Top", "Сверху" },
            { "Right", "Справа" },
            { "Bottom", "Снизу" },
            { "Broken edge:", "Повреждённый край:" },
            { "Area to hide (px):", "Скрываемая область (px):" },
            { "Show the hidden area in red (while adjusting)", "Показывать скрываемую область красным (при настройке)" },
            { "Driver mode (recommended): Windows sees a smaller monitor, everything incl. games works", "Режим драйвера (рекомендуется): Windows видит монитор меньшего размера, всё работает, включая игры" },
            { "Software mode: covers the broken area, keeps windows and mouse out", "Программный режим: закрывает повреждённую область, не пускает туда окна и мышь" },
            { "Keep the mouse out of the broken area", "Не пускать мышь в повреждённую область" },
            { "Push overflowing windows into the working area", "Сдвигать выходящие окна в исправную область" },
            { "Method:", "Способ:" },
            { "Apply", "Применить" },
            { "Status:", "Состояние:" },
            { "Start with Windows", "Запускать вместе с Windows" },
            { "Emergency: reset all monitors", "Аварийный сброс всех мониторов" },
            { "Driver mode active. Windows sees this monitor as a smaller screen. Stays even if the app is closed.", "Режим драйвера включён. Windows видит этот монитор как экран меньшего размера. Настройка сохраняется и после закрытия программы." },
            { "Software mode active. The app must keep running in the tray.", "Программный режим включён. Программа должна работать в трее." },
            { "This monitor was cropped earlier. Press Revert to restore full screen.", "Этот монитор уже был обрезан. Нажмите «Отменить», чтобы вернуть полный экран." },
            { "Not active. Adjust until the red area covers the broken part, then press Apply.", "Не активно. Настройте так, чтобы красная область закрыла повреждённую часть, затем нажмите «Применить»." },
            { "You can try software mode instead.", "Попробуйте программный режим." },
            { "Could not save: ", "Не удалось сохранить: " },
            { "Could not revert: ", "Не удалось отменить: " },
            { "You can set the recommended resolution in Windows Display Settings.", "Можно выбрать рекомендуемое разрешение в параметрах дисплея Windows." },
            { "The configured monitor is not connected.", "Настроенный монитор не подключён." },
            { "Could not start software mode.", "Не удалось запустить программный режим." },
            { "Display setting re-applied.", "Настройка дисплея применена заново." },
            { "Could not re-apply. Open the app and try again.", "Не удалось применить заново. Откройте программу и попробуйте ещё раз." },
            { "Ekran Kurtar keeps running in the tray.", "Ekran Kurtar продолжает работать в трее." },
            { "Open", "Открыть" },
            { "Exit", "Выход" },
            { "Ekran Kurtar is already running (tray icon).", "Ekran Kurtar уже запущен (значок в трее)." },
            { "Language:", "Язык:" },
            { "Broken", "Повреждено" },
            { "What Windows will see", "Экран, который увидит Windows" },
            { "Active", "Активно" },
            { "Monitor", "Монитор" },
            { "Broken edge", "Повреждённый край" },
            { "Area to hide (px)", "Скрываемая область (px)" },
            { "Show the hidden area in red on screen", "Показывать скрываемую область красным на экране" },
            { "Method", "Способ" },
            { "Driver mode", "Режим драйвера" },
            { "Recommended", "Рекомендуется" },
            { "Windows sees a smaller monitor. Fullscreen and games only use the working area.", "Windows видит монитор меньшего размера. Полноэкранный режим и игры используют только исправную область." },
            { "Software mode", "Программный режим" },
            { "Covers the broken area and keeps windows and the mouse out. Works everywhere.", "Закрывает повреждённую область и не пускает туда окна и мышь. Работает везде." },
            { "Does it look right?", "Всё выглядит правильно?" },
            { "Attention", "Внимание" },
            { "OK", "ОК" },
            { "HOW IT WORKS", "КАК ЭТО РАБОТАЕТ" },
            { "The mouse skips the broken area", "Мышь перескакивает повреждённую область" },
            { "Fullscreen = working area", "Полный экран = исправная область" },
            { "Monitors join seamlessly", "Мониторы стыкуются без зазора" },
            { "Windows stay out", "Окна не заходят в зону" },
            { "Everything is reversible", "Всё можно отменить" },
            { "The cursor goes straight from one monitor's edge to the working part of the next. No dead zone in between.", "Курсор переходит от края одного монитора сразу к исправной части следующего. Мёртвой зоны нет." },
            { "Maximized windows, videos and games only fill the working part.", "Развёрнутые окна, видео и игры занимают только исправную часть." },
            { "Windows ignores the broken part and places the monitors edge to edge.", "Windows игнорирует повреждённую часть и ставит мониторы вплотную." },
            { "In software mode, windows that spill into the broken area are pushed back automatically.", "В программном режиме окна, заходящие в повреждённую область, автоматически сдвигаются обратно." },
            { "Every change needs confirmation. Without it, it reverts within 15 seconds.", "Каждое изменение нужно подтвердить. Иначе оно отменится через 15 секунд." },
            { "Fullscreen", "Полный экран" },
            { "Reverted", "Отменено" },
            { "Open source · MIT license", "Открытый код · лицензия MIT" },
        };
    }
}
