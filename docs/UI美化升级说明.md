# EAgentX 界面美化升级说明（日间 / 夜间双主题）

> 本次只升级界面层。业务逻辑、数据结构、变量名、函数入参、返回结构、事件绑定、控件清单全部保持原样，
> 所有原有功能入口、数据展示项、操作按钮都还在原位。界面代码里不再出现任何写死的颜色值。

## 0. 结果速览

| 项目 | 结果 |
|---|---|
| 构建 | `build.ps1` → **0 警告 0 错误**（Roslyn / 系统 csc 两条链路都可以） |
| 产物 | `dist\EAgentX.exe`，**616,960 字节** |
| SHA-256 | `257168088C4D983C693FE2E686CA362F2C42AE68D4179F0DB61523792BED78F8` |
| 自检 | `--selftest` **22/22 通过**（退出码 0） |
| 主题动画 | 280ms 淡入淡出（要求在 200–350ms 之间） |
| 主题配置 | `%APPDATA%\EAgentX-UI\theme.json`（与 config.json 完全分离） |
| 默认主题 | 首次启动无配置 → 浅色日间 |

界面截图（均由最终产物实拍，系统缩放 150%）：

| 场景 | 文件 |
|---|---|
| 主界面 · 日间 | `docs/ui-theme-light.png` |
| 主界面 · 夜间 | `docs/ui-theme-dark.png` |
| 设置 · 日间 | `docs/ui-theme-settings-light.png` |
| 设置 · 夜间 | `docs/ui-theme-settings-dark.png` |
| 发送间隔弹窗 · 夜间（含遮罩） | `docs/ui-theme-delay-dark.png` |
| 强制 100% 缩放（验证等比缩放） | `docs/ui-theme-scale100.png` |

---

# 第一部分 完整源代码

## 1.1 变更文件清单

完整源码就是下面这些文件本身（已就地修改，不是补丁片段）。所有新增代码都带 `//` 中文注释和分区标记。

| 文件 | 性质 | 行数 | 职责 |
|---|---|---|---|
| `src/Ui/UiSpec.cs` | **新增** | 672 | 分区：全局设计规范（缩放 / 间距 / 圆角 / 字号 / 阴影 / 图标 / 动画 / 原生控件主题化） |
| `src/Ui/Theme.cs` | **重写主题层** | 1069 | 分区：配色定义（Light/Dark 各 35 个 token）、主题切换与动画、控件换肤、富文本颜色迁移、标题栏明暗 |
| `src/Ui/ThemeStore.cs` | **重写存储层** | 111 | 分区：本地持久化（读 / 写 / 容错回退） |
| `src/Ui/ModernControls.cs` | **重写控件库** | 2155 | 自绘控件：按钮 / 卡片 / 输入框容器 / 数字框 / 下拉框 / 页签条 / 滚动容器 / 复选框 / 气泡 / 遮罩 / 消息框 |
| `src/Ui/MainForm.cs` | 界面改造 | 1497 | 全部尺寸走缩放、卡片落影、图标按钮、主题按钮、模块标题、提示气泡 |
| `src/Ui/SettingsForm.cs` | 界面改造 | 593 | 分区小标题、输入框圆角描边、自绘下拉、自绘页签 + 自绘滚动条 |
| `src/Ui/DelayForm.cs` | 界面改造 | 129 | 等比排版、圆角输入框、按钮层级 |
| `src/Ui/ApproveForm.cs` | 界面改造 | 93 | 命令区圆角描边、危险/主操作按钮配色 |
| `src/Program.cs` | 仅 2 处提示 | 185 | 单实例提示、致命错误提示改用跟随主题的消息框 |

`src/Core`、`src/Engines`、`src/Net`、`src/Util` 以及 `SelfTest.cs` **一行未动**。

## 1.2 分区 A：主题配色定义（Theme.cs → ThemePalette）

两套配色是**完全独立**的两个字面量集合（`ThemePalette.Light` / `ThemePalette.Dark`），没有互相引用。
两套都刻意避开纯 `#000000` 与纯 `#FFFFFF`。

| token | 日间 Light | 夜间 Dark | 用途 |
|---|---|---|---|
| `Bg` | `#F1F3F8` | `#16181E` | 页面底色 |
| `Panel` | `#FBFCFE` | `#1E2128` | 卡片 / 工具栏 / 状态栏 |
| `PanelAlt` | `#EEF1F7` | `#262A33` | 次级面板、按下态 |
| `Input` | `#F6F8FC` | `#1B1E25` | 输入框 / 下拉框内部 |
| `Hover` | `#F2F3FC` | `#262A34` | 悬浮底色 |
| `Selected` | `#E6E7FA` | `#2F3448` | 选中行 / 选中项 |
| `Browser` | `#14161C` | `#0E1016` | 浏览器嵌入区 |
| `Border` | `#E0E4ED` | `#333846` | 控件边框 |
| `Separator` | `#EAEDF3` | `#2A2E38` | 弱分割线 |
| `Text` | `#222834` | `#E4E7EE` | 主文字 |
| `Dim` | `#6B7488` | `#9AA2B3` | 次要文字 |
| `DisabledText` | `#A6ADBC` | `#666E7E` | 禁用文字 |
| `OnAccent` | `#FCFCFF` | `#F8F9FF` | 强调色上的文字 |
| `Accent` | `#5B5BD6` | `#7C8CF8` | 强调色（主按钮、选中、聚焦） |
| `AccentHover` | `#4A4AC8` | `#96A3FF` | 主按钮悬浮 |
| `AccentPressed` | `#3E3EB2` | `#6C7CE6` | 主按钮按下 |
| `AccentSoft` | `#EEEEFB` | `#2B2F45` | 强调色浅底（段选、标签） |
| `Accent2` | `#8B5CF6` | `#A78BFA` | 顶部渐变副色 |
| `Success` | `#238067` | `#6FD3B5` | 成功提示 |
| `Warning` | `#A06F1C` | `#E0B36A` | 警告提示 |
| `Info` | `#3667B4` | `#8FB4FF` | 普通信息 |
| `Error` | `#C0392B` | `#F08A82` | 错误提示 / 危险按钮 |
| `User` | `#2563EB` | `#8FB4FF` | 日志「你」 |
| `Agent` | `#222834` | `#E4E7EE` | 日志「助手」 |
| `Command` | `#965800` | `#E0B36A` | 日志命令块 |
| `Result` | `#0F765C` | `#6FD3B5` | 日志执行结果 |
| `Overlay` | `rgba(22,26,40,.48)` | `rgba(9,11,16,.72)` | 弹窗遮罩 |
| `Shadow` | `rgba(31,35,48,.12)` | `rgba(6,8,12,.47)` | 浮层落影 |
| `ShadowSoft` | `rgba(31,35,48,.08)` | `rgba(6,8,12,.28)` | 卡片落影 |
| `ShadowStrong` | `rgba(24,28,40,.20)` | `rgba(4,6,10,.59)` | 悬浮弹窗落影 |
| `ScrollTrack` | `#F1F3F8` | `#1B1E25` | 滚动条轨道 |
| `ScrollThumb` | `#C9CFDB` | `#454C5C` | 滚动条滑块 |
| `TooltipBg` | `#2A2F3D` | `#333948` | 提示气泡底 |
| `TooltipText` | `#F6F7FB` | `#EDEFF5` | 提示气泡文字 |
| `ButtonDisabled` | `#EEF0F5` | `#242831` | 禁用按钮底 |

关键实现点（`ThemePalette.Blend`）：用 `typeof(ThemePalette).GetFields(...)` 反射遍历所有 `Color` 字段做线性插值，
**以后新加颜色字段不用改 Blend**，不会出现"漏掉某个颜色没过渡"的问题：

```csharp
public ThemePalette Blend(ThemePalette to, double amount)
{
    ThemePalette p = new ThemePalette();
    for (int i = 0; i < ColorFields.Length; i++)
    {
        FieldInfo field = ColorFields[i];
        if (field.FieldType != typeof(Color)) continue;
        Color from = (Color)field.GetValue(this);
        Color target = (Color)field.GetValue(to);
        field.SetValue(p, Lerp(from, target, amount));   // 逐通道插值，含 alpha
    }
    return p;
}
```

## 1.3 分区 B：主题切换 + 280ms 淡入淡出（Theme.SetMode）

```csharp
/// <summary>切换过渡时长：280ms（要求 200~350ms），淡入淡出、不闪烁。</summary>
private const int AnimationMilliseconds = 280;

public static void Toggle()
{
    SetMode((mode == ThemeMode.Light) ? ThemeMode.Dark : ThemeMode.Light, true);
}

public static void SetMode(ThemeMode targetMode, bool animate)
{
    try
    {
        if (targetMode == mode && !IsAnimating()) return;
        ThemePalette target = (targetMode == ThemeMode.Dark) ? ThemePalette.Dark : ThemePalette.Light;
        ThemePalette from = current;
        mode = targetMode;
        if (!animate || Application.OpenForms.Count == 0) { /* 立即切 + 存盘 */ }
        // 每 15ms 把两套调色板按时间比例混合一帧，所有控件同步重绘
        animationTimer = new Timer();
        animationTimer.Interval = 15;
        animationTimer.Tick += delegate
        {
            double amount = (DateTime.UtcNow - animationStart).TotalMilliseconds / AnimationMilliseconds;
            if (amount >= 1.0) { /* 落到终值 + ApplyOpenForms + 存盘 + RaiseChanged */ }
            current = animationFrom.Blend(animationTo, amount);
            ApplyOpenForms();
        };
        animationTimer.Start();
    }
    catch (Exception ex)
    {
        // 容错：任何异常都回退浅色主题并记日志，主程序不崩
        try { EAgentX.Util.Log.Error(ex); } catch { }
        StopAnimation(); mode = ThemeMode.Light; current = ThemePalette.Light;
        ApplyOpenForms(); ThemeStore.Save(mode); RaiseChanged();
    }
}
```

`Theme.Apply(Control)` 递归遍历所有已打开窗体的每个控件，按"角色"回调，只改颜色 / 边框 / 重绘，
**不触碰任何事件订阅**。覆盖范围：`Form`（含 DWM 标题栏明暗）、`ModernCard`、`ModernSurface`、`ModernButton`、
`ModernField`、`ModernSelect`、`ModernTabStrip`、`ModernScrollHost`、`Label`、`TextBox`、`RichTextBox`、
`ComboBox`、`NumericUpDown`、`CheckBox`、`Panel`、`TableLayoutPanel`、`FlowLayoutPanel`、`TabControl`、`TabPage`。

已输入的文字（日志）走 RTF 颜色表迁移：

```csharp
// RecolorRichText：把旧调色板的 RGB 映射到新调色板的 RGB，切换后历史日志不会"白字白底"
string replaced = Regex.Replace(rtf, @"\\red(\d+)\\green(\d+)\\blue(\d+)", delegate(Match m) { ... });
```

## 1.4 分区 C：本地持久化（ThemeStore.cs 全文要点）

```csharp
internal static class ThemeStore
{
    /// 配置路径：%APPDATA%\EAgentX-UI\theme.json（不碰 config.json）
    public static string FilePath { get { return Path.Combine(Settings.Dir, "theme.json"); } }

    public static ThemeMode Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return ThemeMode.Light;      // 首次启动 → 浅色
            string text = File.ReadAllText(FilePath, Encoding.UTF8);
            Match m = Regex.Match(text, "\"mode\"\\s*:\\s*\"([A-Za-z]+)\"");
            if (m.Success) return string.Equals(m.Groups[1].Value, "dark", StringComparison.OrdinalIgnoreCase)
                                  ? ThemeMode.Dark : ThemeMode.Light;
            return ThemeMode.Light;
        }
        catch { return ThemeMode.Light; }                            // 读失败 → 浅色，不崩
    }

    public static void Save(ThemeMode mode)
    {
        // {"version":1,"mode":"dark","savedAt":"..."}  先写 .tmp 再替换，避免半截文件
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
    }
}
```

写入时机：**每次切换完成**（动画结束帧）、**关闭窗口**（`MainForm.OnClosing` 里的 `Theme.Save()`）。
读取时机：**进程启动、创建窗口之前**（`Program.Main` → `Theme.Initialize()`）。

## 1.5 分区 D：全局设计规范 token（UiSpec.cs）

```csharp
public static class Ui
{
    public static float Scale { get; }          // 96 DPI = 1.0，150% 缩放 = 1.5
    public static int P(double logical);        // 逻辑像素 → 实际像素（所有尺寸都过这里）
    public static Size Sz(double w, double h);  // 设计尺寸 + 屏幕放不下时按工作区收缩
    public static Font Font(double px, FontStyle style);   // 像素字号 + 缓存
}

public static class UiSpace  { Xs=4, Sm=8, Md=12, Lg=16, Xl=24, Xxl=32 }   // 4 的倍数阶
public static class UiRadius { Card=16, Control=10, Small=6 }              // 只有三档圆角
public static class UiFonts  { H1=27B, DialogTitle=19B, H2=15B, Body=13, BodyLarge=14, Small=12, Tiny=11, Mono=13 }
public static class UiShadow { CardSpread=10, Drop(...) }                  // 低透明度多层落影
internal sealed class Tween  { /* 15ms 步进的 0→1 补间，用于 hover / press 平滑过渡 */ }
public enum  ModernIcon      { Sun, Moon, Gear, Folder, Plus, Stop, ChevronDown, ArrowUp, ... }
public static class IconPainter { Draw(...) }                              // 线性图标，线宽统一 1.4
internal static class WindowChrome   { ApplyDarkTitleBar(form, dark) }      // DWM 深色标题栏
internal static class NativeScrollTheme { Apply(control, dark) }            // 原生滚动条切暗色
```

缩放规则：`Program` 保持 `SetProcessDPIAware()`（截图 / 鼠标工具需要物理像素），
因此 UI 层自己按 `DPI/96` 等比放大字号与所有像素值；`Form.AutoScaleMode = None` 关掉 WinForms 自带的二次缩放。
窗口初始尺寸还额外受屏幕工作区约束（`Ui.FitToScreen(1180, 780, 0.92)`），150% 缩放下的小屏也不会超出屏幕。

## 1.6 分区 E：自绘控件清单（ModernControls.cs）

| 控件 | 解决的问题 |
|---|---|
| `ModernButton` | 四态（常态/悬浮/按下/禁用）+ 补间过渡 + 线性图标 + 焦点环；主 / 次 / 危险 / 幽灵四种配色 |
| `ModernCard` | 圆角卡片容器，负责填充、描边、圆角裁剪子控件 |
| `ModernSurface` | 双缓冲容器，可在卡片背后画柔和落影（卡片自己画不到自己外面） |
| `ModernField` | 输入框容器：圆角描边 + 聚焦强调色 + 错误红框三态；内部仍是原 `TextBox` / `NumericUpDown` |
| `ModernSpinButtons` | 覆盖原生灰白上下按钮的自绘箭头（点击仍调用 `UpButton/DownButton`，行为一致） |
| `ModernSelect` + `ModernSelectPopup` | 完全自绘的下拉框与浮层，对外仍是 `Items` / `SelectedIndex` |
| `ModernTabStrip` | 自绘页签条，替代原生 TabControl 的白底页签带 |
| `ModernScrollHost` | 自绘细滚动条容器（滚轮 / 拖动 / 点击轨道），替代原生灰白滚动条 |
| `ModernCheckBox` | 圆角复选框，自定义勾选符号 |
| `Tip` | 跟随主题的悬浮提示气泡（含日志长行悬浮显示整行） |
| `DimLayer` | 模态遮罩层：半透明、不抢焦点、点遮罩关闭弹窗 |
| `MessageDialog` | 替代系统 `MessageBox`（原生弹窗在深色主题下是刺眼纯白），返回值语义不变 |

## 1.7 业务代码保持原样的证据

以下方法 / 字段在 `MainForm` 中**逐字未改**（只调整了它们内部与布局相关的像素常量）：
`SetSelfOpacity`、`ToggleEmbed`、`DetachBrowser`、`MoveEmbeddedWindow`、`StartEmbedTimer`、
`OnLogLine`、`AppendBlock`、`AppendResult`、`AppendBanner`、`TrimLog`、`Indent`、`WireSession`、
`OnStateChanged`、`StateIsActive`、`EnsureSessionAsync`、`NewSessionAsync`、`SendCurrentAsync`、
`ApproveCommand`、`StopWork`、`DiagnoseAsync`、`PickWorkDir`、`EditSendDelay`、`UpdateDelayButton`、
`AttachFilesAsync`、`CopyFilesToWorkDir`、`AppendAttachmentNote`、`ApplyWorkDir`、`OpenSettings`、
`DisposeSession`、`CtsToken`、`UpdateUi`、`OnInputKeyDown`、`OnClosing`。

`SettingsForm` 的 `LoadValues` / `Save` / `BrowseWorkDir` / `BrowseBrowser` 同样逐字保留
（`Save` 里只多了一行"把工作目录外框标红"的纯视觉标记，判断条件一个字没改）。

---

# 第二部分 优化改动清单

## 2.1 新增的类 / 函数

| 位置 | 新增内容 |
|---|---|
| `UiSpec.cs` | `Ui.Scale/P/Pt/Sz/Pad/Font/MonoFont/FitToScreen/SurfaceOf/FillSurface`、`UiSpace.*`、`UiRadius.*`、`UiFonts.*`、`UiShadow.Drop`、`Tween`、`ModernIcon`、`IconPainter.Draw`、`WindowChrome.ApplyDarkTitleBar`、`NativeScrollTheme.Apply/UseClassicStyle` |
| `Theme.cs` | `ThemePalette.Separator/Hover/Selected/DisabledText/TooltipBg/TooltipText/ShadowSoft/ShadowStrong`、`ThemePalette.Lerp/Blend`、`Theme.IsDark/ToggleIcon/Separator/Hover/...`、`Theme.BlendTo`、`UiFontTiny/InputFont/H2Font` |
| `ThemeStore.cs` | `Load` 改为 `"mode"` 字段正则解析 + 裸值兼容；`Save` 改为临时文件 + `File.Replace` 原子写入，并写入 `version` / `savedAt` |
| `ModernControls.cs` | `IThemedControl`、`ModernButtonKind.Ghost`、`ModernButton.Icon/ApplyTheme`、`ModernSurface`、`ModernField`、`ModernSpinButtons`、`ModernSelect`、`ModernSelectPopup`、`ModernTabStrip`、`ModernScrollHost`、`DimLayer`、`Tip`、`MessageDialog` |
| `MainForm.cs` | `LayoutHeader` 增加间距常量；新增 `Tip.Attach` 调用与启动换肤钩子 |
| `SettingsForm.cs` | `AddSectionRow`（分区小标题）、`WrapField`（输入框自动套圆角外框）、`WorkDirField`（错误态外框） |

## 2.2 配色变量定义位置

- 全部颜色：`src/Ui/Theme.cs` → `ThemePalette.Light` / `ThemePalette.Dark`（唯一来源，界面代码零硬编码，已用 `grep Color.FromArgb` 验证）。
- 语义角色 → 颜色的映射：`Theme.ColorFor(ThemeRole)`。
- 颜色过渡：`ThemePalette.Blend` + `Theme.Lerp`。

## 2.3 控件样式改动

| 控件 | 改动 |
|---|---|
| 主按钮 / 次按钮 / 危险按钮 | 圆角 10、补间 hover、焦点环、禁用态统一 `ButtonDisabled` + `DisabledText`；危险按钮改用"红字 + 红边"弱化攻击性 |
| 顶部三个按钮 | 增加线性图标（月亮/太阳、文件夹、齿轮），文字与图标同色跟随主题 |
| 主题按钮 | `IsThemeToggle`，切换时自动换图标与文字（切换夜间 / 切换日间） |
| 输入框 | 由 `BorderStyle.FixedSingle` 的方角改为圆角描边容器，三态：常态 `Border`、聚焦 `Accent`（1.6px）、错误 `Error` |
| 数字框 | 隐藏系统上下按钮，换成自绘箭头；数字左对齐，宽度固定 150 |
| 下拉框 | 原生 `ComboBox` → 自绘 `ModernSelect` + 自绘浮层（选中项用 `Selected` 底色 + `Accent` 文字） |
| 复选框 | 原生方框 → 圆角方框 + 自绘勾选符号 + 悬浮染色 |
| 页签 | 原生 `TabControl` 白底页签带 → `ModernTabStrip` 段选（`AccentSoft` 底 + 下划线） |
| 滚动条 | 原生灰白滚动条 → `ModernScrollHost` 自绘细滚动条（`ScrollThumb` / 拖动时 `Accent`）；文本框内的原生滚动条用 `SetWindowTheme` 切暗色 |
| 卡片 | 圆角 16 + 1px `Border` 描边 + 父容器绘制的柔和落影 |
| 日志区 | 增加"对话记录"模块小标题；历史颜色随主题迁移 |
| 状态栏 / 工具栏 / 标题栏 | 弱化分割线（`Separator`，1px）；标题栏跟随主题明暗（DWM） |
| 提示气泡 | 系统黄色 tooltip → 圆角深色气泡（`TooltipBg`），长日志行悬浮可看整行 |
| 弹窗 | 全部改为等宽高比排版 + 圆角控件；打开时铺 `DimLayer` 半透明遮罩，点遮罩取消 |
| 系统消息框 | `MessageBox` → `MessageDialog`（信息/警告/错误/询问四色图标），返回值一致 |

## 2.4 布局调整位置

| 位置 | 调整 |
|---|---|
| `MainForm.BuildUi` | 所有坐标 / 尺寸改为 `Ui.Pt/Sz/Pad`；标题栏 112、工具栏 64、输入区 218、状态栏 32（逻辑像素）；新增 `ModernSurface` 承载卡片落影 |
| `MainForm.LayoutHeader` | 右侧三个按钮靠右依次排布，间距 `UiSpace.Sm`，右边距 `UiSpace.Xl` |
| `MainForm.LayoutInputArea` | 底部一行（提示 + 复选框）单独计算 `rowTop`，与卡片下边缘留出 18 的空白，避免贴边 |
| `MainForm` 构造函数 | `AutoScaleMode.None` + `ClientSize/MinimumSize` 受屏幕工作区约束 |
| `SettingsForm.BuildUi` | 页签区改为「页签条 + 滚动页」；`AddSectionRow` 增加 5 个分区标题；行高 36/32、行距统一；页脚 66 |
| `DelayForm` / `ApproveForm` | 位置全部改逻辑像素 + 相对右/下边缘计算，窗口尺寸受屏幕约束 |

## 2.5 持久化与容错位置

| 项目 | 位置 |
|---|---|
| 读取主题 | `Program.Main` → `Theme.Initialize()` → `ThemeStore.Load()` |
| 保存主题（切换后） | `Theme.SetMode` 动画结束分支 / 无动画分支 → `ThemeStore.Save(mode)` |
| 保存主题（关闭时） | `MainForm.OnClosing` → `Theme.Save()` |
| 读取失败回退 | `ThemeStore.Load` 的 `catch` → `ThemeMode.Light` |
| 保存失败忽略 | `ThemeStore.Save` 的 `catch`（不影响主程序） |
| 切换异常回退 | `Theme.SetMode` 的 `catch` → 强制浅色 + `Log.Error` |
| 遮罩兜底 | `DimLayer.ShowFor/HideLayer` 全 `try/catch`，`MainForm` 的弹窗调用点用 `try/finally` 收起 |

---

# 第三部分 使用说明

## 3.1 主题切换按钮在哪

主窗口右上角，**「设置」左边第三个按钮**，图标 + 文字：

- 浅色模式下显示 `☾ 切换夜间`
- 深色模式下显示 `☀ 切换日间`

点一下即淡入淡出切换（280ms），松手后立刻把选择写进本地配置；鼠标悬停会显示气泡提示。

## 3.2 主题配置文件在哪

```
%APPDATA%\EAgentX-UI\theme.json
```

即通常的 `C:\Users\<用户名>\AppData\Roaming\EAgentX-UI\theme.json`，内容形如：

```json
{
  "version": 1,
  "mode": "dark",
  "savedAt": "2026-10-20 19:15:41"
}
```

- `mode` 只认 `light` / `dark`；首次启动没有这个文件时默认 `light`。
- 想手动指定，直接把 `mode` 改成 `dark` 或 `light`，下次启动生效。
- 删掉整个文件等同于"恢复默认浅色"。
- 这个文件与 `config.json`（引擎、路径、提示词等业务配置）完全分离，互不影响。

## 3.3 想自己改两套颜色，改哪一段

**只改 `src/Ui/Theme.cs` 里的两个属性**，其它地方不用动：

| 想改什么 | 改这里 |
|---|---|
| 日间配色 | `ThemePalette.Light`（约第 70 行起），35 个 `p.XXX = Color.FromArgb(...)` |
| 夜间配色 | `ThemePalette.Dark`（约第 120 行起） |
| 强调色（按钮、选中、聚焦） | `p.Accent` / `p.AccentHover` / `p.AccentPressed` / `p.AccentSoft` / `p.Accent2` |
| 页面底色 / 卡片底色 | `p.Bg` / `p.Panel` / `p.PanelAlt` |
| 文字深浅 | `p.Text`（主）/ `p.Dim`（次要）/ `p.DisabledText`（禁用） |
| 边框与分割线 | `p.Border` / `p.Separator` |
| 提示色 | `p.Success` / `p.Warning` / `p.Info` / `p.Error` |
| 遮罩与落影 | `p.Overlay` / `p.ShadowSoft` / `p.ShadowStrong` |
| 滚动条 / 气泡 | `p.ScrollTrack` / `p.ScrollThumb` / `p.TooltipBg` / `p.TooltipText` |

示例（把强调色从靛蓝换成墨绿，只改日间那一行）：

```csharp
p.Accent = Color.FromArgb(45, 122, 106);       // 原来是 (91, 91, 214)
p.AccentHover = Color.FromArgb(38, 106, 92);
p.AccentPressed = Color.FromArgb(30, 90, 78);
p.AccentSoft = Color.FromArgb(233, 244, 241);
```

> 只要字段名不变，新增/修改颜色不需要动 `Blend()`、`Apply()`，切换动画会自动跟着过渡。

## 3.4 想改圆角 / 间距 / 字号 / 阴影

全部集中在 `src/Ui/UiSpec.cs`：

| 项目 | 位置 | 说明 |
|---|---|---|
| 圆角 | `UiRadius.Card = 16`、`Control = 10`、`Small = 6` | 三档统一，改一处全局生效 |
| 间距 | `UiSpace.Xs/Sm/Md/Lg/Xl/Xxl`（4/8/12/16/24/32） | 所有外边距、内边距都取这些值 |
| 字号 | `UiFonts.H1/DialogTitle/H2/Body/BodyLarge/Small/Tiny/Mono` | 像素字号，自动乘缩放比 |
| 落影 | `UiShadow.CardSpread = 10` + `Drop(...)` | 扩散半径、透明度都在这里 |
| 动画 | `Theme.AnimationMilliseconds = 280` | 主题切换时长（200–350 之间都符合要求） |
| 整体缩放 | `Ui` 的静态构造 | 默认自动读系统 DPI；开发时可 `set EAGENTX_UI_SCALE=1.25` 强制某比例验证 |

## 3.5 构建、自检、截图

```powershell
cd EAgentX
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\EAgentX.exe --selftest          # 22/22 通过，退出码 0
.\dist\EAgentX.exe

# 开发期截图（可选，不影响程序）：
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\ui-fullshot.ps1 -Out docs\x.png -Theme dark
```

## 3.6 已知取舍

1. **深色标题栏**依赖 DWM（Win10 1809+）。老系统上标题栏仍是系统浅色，程序不会报错，只是边框偏亮。
2. **文本框内的原生滚动条**用 `SetWindowTheme("DarkMode_Explorer")` 切暗色，个别 Win10 旧版本可能不生效。
   设置页、日志区这类我们自己控制的区域用的是自绘细滚动条，不受影响。
3. 系统级的文件选择框（"浏览"按钮弹出的 `OpenFileDialog` / `FolderBrowserDialog`）属于操作系统窗口，
   跟随的是系统主题，不随软件内主题变化。
4. `MessageBox` 已全部替换为自绘消息框；如果有第三方代码（不在本仓库）弹原生 MessageBox，仍是系统样式。

---

# 第四部分 性能与显示问题修复（第二轮）

第一轮交付后反馈了四个问题，均已定位到根因并修复。修复只改 UI 层，业务逻辑未动，`--selftest` 仍是 22/22。

## 4.1 进设置上下滑动卡顿（二次修复）

**第一次修复没解决问题**，于是直接用 `Stopwatch` 实测每条滚轮消息的耗时，定位到了真正的原因。

**实测数据**（设置页，52 个子控件，每步 18px）：

| 实现方式 | 单步耗时 |
|---|---|
| 最初：`viewport.SetBounds` + 内容 `AutoSize` | **23.6 ms** |
| 只移动内容 `Top`（内容仍 `AutoSize`） | 24 ms（几乎没变） |
| 移动视口（视口高度=内容高度），内容仍 `AutoSize` | 61 ms（更差） |
| **移动视口 + 内容关闭 `AutoSize`（最终方案）** | **1.5 ms**（含真实绘制约 5.2 ms） |

**根因**：`TableLayoutPanel` 打开 `AutoSize` 后，**任何一次 bounds 通知都会触发全表重新测量**
（它会遍历所有行列、测量每个子控件）。滚动时不管是移动内容还是移动它的父窗口，都会产生这种通知，
于是每滚一格就等于把 52 个控件重新量一遍 —— 20 ms 起步，必然卡。

**最终修复**（`ModernControls.cs` → `ModernScrollHost`）：

1. **内容关闭 `AutoSize`**，高度只在宽度变化时由 `MeasureContent()` 主动算一次
   （临时打开 `AutoSize` → 量高度 → 立刻关掉并固定尺寸）。滚动期间不再有任何测量。
2. 滚动时移动的是**视口窗口**（视口高度 = 内容高度，底部不会露白），内容的 `Bounds` 全程不变。
3. 视口与内容都使用 `Anchor = Top | Left`。
   ⚠️ 不能用 `Anchor = None`：WinForms 布局引擎会把它**居中**，内容会跑到可视区外（表现为整页空白）。
4. 滚动时只让右侧滚动条槽位失效重绘（`Invalidate(槽位矩形)`），其余由系统按裁剪区重画。
5. 滚轮仍是全局单一过滤器（`WheelGate`），弱引用跟踪，不移除也不泄漏。

**验证方法**：`%APPDATA%\EAgentX-UI\logs\app.log` 里会看到
`bench: moveOnly=46ms, ... realistic=156ms / 30 steps`（即 1.5ms / 5.2ms 每步）。
另外用整屏截图确认过滚动 280px 后的画面内容正确、底部无空白。

## 4.2 鼠标移到按钮上界面闪烁

**根因**：按钮、输入框容器都设置了**非矩形 `Region`**（圆角路径）。Windows 对这种窗口重绘时
需要先擦除旧形状、再由父窗口补背景，hover 每 15ms 重绘一帧，就会出现边缘闪白/闪黑。

**修复**：

1. `ModernButton` / `ModernField` 不再设置 `Region`，改成"**矩形窗口 + 双缓冲**"，
   绘制时先 `Ui.FillSurface()` 铺父容器底色、再画圆角形状，视觉完全一致但不再有形状擦除。
2. `Tween.Jump` 增加"值没变就不重绘"判断，主题切换时不再产生无意义的重绘。
3. 落影层数 10 → 6，单帧绘制开销减半（观感无差别）。
4. 卡片 `ModernCard` 保留 `Region`（它需要裁剪圆角内的子控件），但只对尺寸变化响应，hover 不重绘。

## 4.3 夜间模式切换不顺滑

**根因**：切换动画每 16ms 跑一帧，每帧都会递归遍历所有控件；其中两件事开销极大：

- `DwmSetWindowAttribute`（设置深色标题栏）——每个窗体每帧调一次；
- `SetWindowTheme`（系统滚动条切暗色）——每个文本框/富文本/页签每帧调一次。

一帧里几十次 DWM / UxTheme 调用，UI 线程直接被拖住，动画掉帧。

**修复**（`Theme.cs`）：

1. 新增 `Theme.Animating` 标志；**动画中间帧跳过 DWM 与 UxTheme 调用**，只在首帧/末帧各设置一次。
2. `ApplyTheme` 不再重建 `Region`（那是纯尺寸相关的工作，动画里做毫无意义）。
3. 补间 `Jump` 跳过无变化的重绘（见 4.2）。

## 4.4 嵌入浏览器显示不全

**根因**（三个叠加）：

1. 浏览器窗口如果处于**最大化**状态，`SetParent` 之后 `MoveWindow` 缩不下去，只能看到左上角一块；
2. Chromium 重新挂到父窗口后会**异步再排一次布局**，第一次 `MoveWindow` 的尺寸会被它自己的重排覆盖；
3. 嵌入区宽度固定 520 逻辑像素，窗口小的时候页面被挤得很窄。

**修复**（`MainForm.cs`）：

1. 嵌入前若浏览器是最大化/最小化，先 `ShowWindow(SW_RESTORE)` 还原；
   改样式时同时清掉 `WS_MAXIMIZE`。
2. `MoveWindow` 之后新增 **260ms 延时二次校准**（`ScheduleEmbedRelayout`），
   等浏览器内核排完再按面板客户区尺寸对齐一次；还原独立窗口时取消该定时器。
3. 嵌入区改为「外层容器 + 右侧可拖动分隔条」：默认宽度 560（原 520），
   `MinSize=320`、`MinExtra=420`，用户可以把浏览器拉宽，窗口缩放时也会跟着变。

## 4.5 顺带修掉的小问题

- 设置页脚"配置 / 主题路径"两行文字在窄窗口下换行压到边框，改为单行 + 悬浮气泡显示完整路径。
- 未选中复选框在浅色主题下边框太淡，加了一档对比度。
