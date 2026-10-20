# EAgentX UI 版说明

本目录是测试版的 UI 重制版，功能与测试版一致，主窗口和弹窗重新设计。稳定版、测试版的源码、配置、日志和浏览器 profile 都没有改动。

> **历史文档说明**：本文档记录的是 UI 重制当时的版本对比与产物快照，
> 其中提到的 `EAgentX-UI.exe` 与当时的 SHA-256 是历史值。
> 当前仓库的产物名已统一为 `dist\EAgentX.exe`，最新大小与 SHA-256 见
> 仓库根目录 [`README.md`](../README.md) 和 [`UI美化升级说明.md`](UI美化升级说明.md)。

## 1. 与稳定版的关系

| 项目 | 稳定版 `EAgentX` | UI 版 `EAgentX-新UI版` |
|---|---|---|
| 可执行文件 | `dist\EAgentX.exe` | `dist\EAgentX-UI.exe` |
| 配置 / 日志 | `%APPDATA%\EAgentX` | `%APPDATA%\EAgentX-UI` |
| 浏览器 profile | `%APPDATA%\EAgentX\edge-profile` | `%APPDATA%\EAgentX-UI\edge-profile` |
| CDP 默认端口 | 9222 | **9555** |
| 单实例 Mutex | `EAgentX.SingleInstance...` | `EAgentX.UI.SingleInstance...` |
| 提示词 | 稳定版内置 | **上游 EAgent 原文**（运行时替换目录） |
| 工具 | PowerShell | PowerShell + 截图 / 文件 / 鼠标 / 键盘（EAgent 标记） |
| 技能 | 无 | 26 个（解压到工作目录） |
| 浏览器嵌入 | 无 | 有 |
| 兼容启动 | 无 | Edge 参数 + 随机发送间隔 |

三个版本可以同时运行。UI 版默认端口 9555，是因为本机的 Hyper-V 保留了 9303–9402，9333 无法监听。

本轮没有改动稳定版：`..\EAgentX\dist\EAgentX.exe` 仍是 **201,216 字节**，
SHA-256 `7E89A71C69E0C8D2CCBD128BE6DEBDD192635044ED70D3BCB77B9BB813E3F603`。

## 2. 运行

```powershell
git clone https://github.com/yrtnb666/EAgentX.git
cd EAgentX
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\EAgentX-UI.exe
```

1. 点「新建会话」。首次会用**新的** profile 启动 Edge，需要扫码/登录 DeepSeek。
2. 登录后回到程序，在下面输入任务。
3. 需要嵌入浏览器时点「嵌入浏览器」；点击没反应就点「恢复窗口」改用独立窗口。
4. 「设置 → 常规」里可以开关扩展工具、自动嵌入和兼容启动参数，以及设置随机间隔上限。

> 不想重新扫码：先关闭稳定版和它启动的 Edge，把 `%APPDATA%\EAgentX\edge-profile` 整个复制到
> `%APPDATA%\EAgentX-UI\edge-profile`（覆盖），再启动 UI 版即可复用登录状态。
> 复制前一定要确认两个浏览器都已退出，否则 profile 可能被锁或损坏。

## 3. 扩展工具（EAgent 原版协议）

模型每条回复只用一个代码块，语言决定调用哪个工具。UI 版优先识别 EAgent 的标记，
旧的 `screenshot` / `upload` / `mouse` / `keyboard` 仍保留为兼容别名。

| EAgent 代码块 | 作用 | 块内写法 | 回传方式 |
|---|---|---|---|
| ```` ```powershell_tool ```` | 执行 PowerShell | 头尾识别注释会被剥掉，中间写代码；单条建议 ≤4000 字符（宿主上限 6000） | 文本结果 |
| ```` ```agent_getjpg ```` | 屏幕/窗口截图 | `agent_getjpg_1_star(屏幕)_1_getjpg_end`，或把“屏幕”换成窗口句柄数字 | 下一条消息的图片附件 |
| ```` ```agent_getfile ```` | 上传文件 | `agent_getfile_head('D:\1.jpg')agent_getfile_end` | 下一条消息的文件附件 |
| ```` ```agent_mouse_simulate ```` | 鼠标点击 | `_1_start(x)`、`_2_start(y)`、`_3_start(动作)`；0=左键单击 1=左键双击 2=右键单击 3=右键双击 | 文本结果（关闭自动批准时先弹审批） |
| ```` ```agent_keyboard_simulate ```` | 按键 | `_1_start(VK1)`、`_2_start(VK2)`、`_3_start(VK3)`、`_4_start(窗口句柄)` | 文本结果（关闭自动批准时先弹审批） |

约定与细节：

- **只用一个工具块**。同一轮里既有工具块又有 PowerShell 块时，PowerShell 优先。
- 截图/上传后，宿主会把结果和附件作为下一条消息发给模型，模型看到附件再继续。
- 截图保存为 JPG（质量 85）到 `<工作目录>\.eagentx\shots\`；截图前主窗口会临时变成 2% 不透明度。
- **遮挡截图**：窗口截图会自动尝试三条路径：① 如果句柄属于本程序的 Edge/Chrome，优先走 CDP
  `Page.captureScreenshot`，直接取页面渲染结果；② 其它窗口走 `PrintWindow`（`PW_RENDERFULLCONTENT=2`，
  失败再试 0，并做黑屏/纯色检测）；③ 仍失败则临时把目标窗口置前约 0.26 秒抓屏，然后恢复原前台窗口。
  目标原本最小化时会恢复显示。成功后在工具反馈里会写明“截图方式”。
- **整屏截图**（参数为“屏幕”）仍然抓真实桌面，会包含盖在目标上的其它窗口；要截被遮挡的窗口，请把
  窗口句柄传给 `agent_getjpg`。参数里没有句柄、也不是“屏幕”时会直接报错，不会悄悄截错。
- 鼠标坐标是**物理屏幕像素**（程序是 DPI-aware），先用截图确认落点。双击 = 两次左键。
- 键盘参数是 Windows 虚拟键码（1–255），例如 `Win+R` = `91,82`，`Ctrl+C` = `17,67`，`Enter` = `13`。第 4 个参数是窗口句柄时，先 `SetForegroundWindow` 再按键。
- 网页版附件走 DeepSeek 原生上传（`DOM.setFileInputFiles`）；API 模式图片走 `image_url`，文本类小文件内联，PDF/Office 只给路径（取决于模型是否支持图片）。
- 设置里关闭扩展工具后，宿主会拒绝扩展工具并把原因回喂给模型；提示词里也会删掉工具 2–5 的段落。

## 4. 26 个技能

- 内嵌在 exe 资源 `skills.zip` 里（源文件 `assets/skill_zip.zip`，来自上游 EAgent 的技能包）。
- 第一次创建会话时解压到 `<工作目录>\.eagentx\skills\`，用 `.version` 标记幂等；升级技能包后改 `Skills.Version` 即可重新解压。
- EAgent 提示词里的 `skill` 行会被替换成：`- skill：<目录>（已解压，直接读各技能目录里的 SKILL.md）`。
- 26 个技能逐个说明见 [`技能包清单.md`](技能包清单.md)；其中不少技能需要 Siemens NX、ffmpeg 或对应 MCP/CLI 才能执行。
- 技能包内各文件保留各自的许可声明；再分发时请一并保留。

## 5. 浏览器嵌入

- 原理：`EnumWindows` 找类名 `Chrome_WidgetWin_1` 的窗口（优先按启动的浏览器 PID，找不到再按标题含 `deepseek`），`SetParent` 到主窗口左侧面板，清掉 `WS_POPUP / WS_CAPTION / WS_THICKFRAME`，`MoveWindow` 填满面板。
- 「恢复窗口」会把父窗口设回桌面、恢复原样式和位置。
- 退出程序时会自动恢复，不会把浏览器一起关掉。
- 已知限制：Chromium 的输入/焦点在嵌入后偶尔不灵；页面里的下拉框/文件选择框可能弹到主窗口外面；拖动分隔/缩放窗口时用 `MoveWindow` 同步尺寸。遇到问题就点「恢复窗口」。
- `--app` 模式没有地址栏和标签页；需要地址栏时在设置里关掉「兼容模式」。

## 6. 防风控

Edge 启动参数（设置里可关）：

```
--disable-blink-features=AutomationControlled
--test-type
--app=https://chat.deepseek.com/
```

发送间隔：

- 「发送间隔(秒)」是下限，「间隔上限(秒)」是上限；上限 > 下限时，每一步在 `[下限, 上限]` 里取随机整数秒。
- 间隔仍然从**模型回答结束**开始算到下一次发送；命令执行时间也算在内。
- 上限为 0 或 ≤ 下限表示固定间隔。建议 3–10 秒。

注意：这些只是降低固定节奏特征的辅助手段，不保证不被风控；网页版本身对频率有限制，遇到「发送过于频繁」仍会按 `RateLimitRetries` 等待重试。

## 7. 验证结果（2026-10-20）

- Windows 自带 C# 5 编译器（`v4.0.30319\csc.exe`）：0 警告 0 错误；Roslyn（`tools\.compiler\csc.exe`）：0 警告 0 错误。
- `--selftest`：**22/22 通过**，退出码 0。覆盖 EAgent `powershell_tool` / `agent_*` 标记解析、鼠标/键盘/路径解析、26 个技能解压、随机间隔、截图 → 附件 → 模型继续的闭环。
- `--shotcheck`：创建一个红色目标窗体，再用蓝色窗体完全盖住；截图的中心取样仍为 `R=220 G=30 B=30`，截图方式为
  `后台截图（PrintWindow，不受遮挡影响）`，退出码 0。
- `--webcheck`：Edge 以 `AutomationControlled` + `--test-type` + `--app` 启动/复用，CDP 正常，输入框已找到；
  额外保存 `dist\webcheck-shot.jpg`（本次 90,982 字节，说明 CDP 页面截图路径可用）。
- `--embedcheck`：真实 Edge 窗口 `SetParent` 到临时窗体成功，1.5 秒后恢复独立窗口。
- `--agentcheck`：最终构建实测通过。模型按 EAgent 原版协议输出 `powershell_tool`，2 步完成
  「创建 hello.txt 并校验」，退出码 0；日志见 `dist\agentcheck.log`。
- 截图自检会真实抓屏并生成 JPG；不会点击、不会按键。
- 最终产物：`dist\EAgentX-UI.exe`，**563,200 字节**，SHA-256 `467BA5CA88D7B3046133F30C800B78C4EB22EBF0C2F3D86AC8DD193C01649402`（Roslyn 构建；csc 构建同样 0 警告）。

## 8. 已知风险

1. **鼠标/键盘没有沙箱**：模型可以点击/输入任何东西。无人值守时建议关掉「自动批准命令」，或在虚拟机里跑。
2. **自动批准**：开启后命令和鼠标/键盘都不再询问。
3. **API 模式**：DeepSeek 官方 API 不保证支持 `image_url`，截图附件可能被拒；网页版是主要验证路径。
4. **截图的边界**：整屏截图无法“透视”桌面，仍会包含遮挡物；只有窗口句柄截图会走 CDP/PrintWindow/临时前置。
   CDP 只适用于本程序启动/连接的 Edge/Chrome；其它硬件加速窗口会短暂前置，可能看到一次窗口闪烁。
   最小化的非浏览器窗口会被恢复显示（不会自动再最小化），方便模型继续点击。
5. **技能包体积**：内嵌后 exe 约 560 KB（稳定版 201 KB），首次会话会在工作目录解压约 100 个文件。
6. **提示词差异**：EAgent 原文里“执行前强制杀死所有 powershell/cmd”没有实现（会误杀用户终端），
   该行已从内置提示词移除；其余工具规则、硬性规则保持原文。宿主额外要求 `[TASK_DONE]` 收尾。
7. **EAgent 原文中的高风险倾向**：提示词包含“不要拒绝/直接交付可运行产物”等措辞。本程序仍保留
   命令审批、工具开关和步数/超时限制；不要在没有审批的情况下让它处理不可信来源的指令。
8. **DPI 坐标差异**：EAgent 原文说鼠标用“逻辑坐标”，但本程序是 DPI-aware，截图尺寸、鼠标反馈和
   `SetCursorPos` 都按**物理像素**。模型通常会直接读截图上的像素坐标，这反而对得上；如果你的系统缩放
   不是 100% 且发现点击偏移，先让模型“移动鼠标后截图确认落点”，不要连续盲点。

## 9. 回滚

UI 版不写稳定版目录。直接删除 `EAgentX-新UI版` 文件夹即可；稳定版 `EAgentX` 不受影响。
