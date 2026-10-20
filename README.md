# EAgentX · 新 UI 版

日间浅色 / 夜间深色双主题、卡片式界面的 EAgentX。功能沿用测试版，只是把主窗口、设置、命令确认和发送间隔
对话框重新做了一遍，去掉了主界面上「工具 / 技能 / 窗口置入 / 防风控」那排功能横幅，日常使用更清爽。

- 稳定版：`..\EAgentX\`，保持原样，没有改动。
- 测试版：`..\EAgentX-测试版\`，保持原样，可以和新 UI 版同时运行。
- 新 UI 版：本目录，独立配置、日志和浏览器 profile。

当前构建：`dist\EAgentX-UI.exe`，614,400 字节，SHA-256
`D06A97497D388294F03ED5FD767AD4D5BD713D48B8CA711A0A2A7FC3CBC9BED6`（0 警告 0 错误；`--selftest` 22/22）。

日间主界面：

![日间界面](docs/ui-theme-light.png)

夜间主界面：

![夜间界面](docs/ui-theme-dark.png)

设置窗口：[日间](docs/ui-theme-settings-light.png) / [夜间](docs/ui-theme-settings-dark.png)；
发送间隔弹窗（含遮罩）：[夜间](docs/ui-theme-delay-dark.png)。

主题 / 美化实现细节、改动清单和"怎么改配色"见 [`docs/UI美化升级说明.md`](docs/UI美化升级说明.md)。

## 与其它版本隔离

| 项目 | 新 UI 版 |
|---|---|
| 可执行文件 | `dist\EAgentX-UI.exe` |
| 配置 / 日志 | `%APPDATA%\EAgentX-UI` |
| 浏览器 profile | `%APPDATA%\EAgentX-UI\edge-profile` |
| CDP 默认端口 | `9555` |
| 单实例 Mutex | `EAgentX.UI.SingleInstance.5C8A72` |

稳定版仍是 `9222`，测试版仍是 `9444`，三个版本互不占用同一个浏览器 profile。

## 界面变化

- **双主题**：日间浅色（冷灰底 + 米白卡片）/ 夜间深色（深灰蓝底 + 深灰卡片），右上角一键切换，
  280ms 淡入淡出，选择自动存到 `%APPDATA%\EAgentX-UI\theme.json`，下次启动沿用；首次启动默认浅色。
- 两套配色都避开纯黑纯白，降低大面积对比，适合长时间挂机看数据。
- 圆角 16 / 10 / 6 三档统一；间距 4/8/12/16/24/32 统一；字号 27/19/15/14/13/12/11 分级。
- 卡片带柔和落影，三层视觉层次：页面底 → 卡片 → 悬浮弹窗。
- 按钮四种状态（常态/悬浮/按下/禁用）都有补间过渡；主 / 次要 / 危险三种配色分开。
- 输入框改成圆角描边，聚焦变强调色、校验失败变红边；数字框、下拉框、复选框、页签、滚动条全部自绘。
- 线性图标（月亮/太阳、文件夹、齿轮、加号、停止）跟随主题变色。
- 弹窗居中 + 半透明遮罩，点遮罩即取消；系统 MessageBox 换成跟随主题的自绘消息框。
- 悬浮提示气泡替代系统黄色 tooltip；日志长行悬浮可看整行。
- 全尺寸按 DPI 等比缩放（100% / 125% / 150% 都不错位），窗口初始尺寸受屏幕工作区约束。
- 顶部只保留品牌、副标题、主题切换、工作目录和设置；功能操作放下方工具栏。

## 功能

- 网页版 DeepSeek 驱动：连接浏览器里的网页会话，不需要 API Key；也支持 OpenAI 兼容 API。
- PowerShell 执行、截图 / 文件 / 鼠标 / 键盘工具、技能库、浏览器嵌入、随机发送间隔。
- 默认每条命令都要确认；可在设置里关闭确认或关闭扩展工具。

## 运行

```powershell
cd "C:\Users\T8pro\Documents\默认项目\EAgentX-新UI版"
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\EAgentX-UI.exe
```

首次启动会用新的浏览器 profile，需要重新登录 DeepSeek。若想复用测试版的登录状态，先退出两个版本的浏览器，
再把 `%APPDATA%\EAgentX-Test\edge-profile` 复制到 `%APPDATA%\EAgentX-UI\edge-profile`。

## 构建与自检

```powershell
# Windows 自带 C# 5 编译器
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Csc "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

.\dist\EAgentX-UI.exe --selftest
.\dist\EAgentX-UI.exe --shotcheck
```

源码保持 C# 5，Windows 自带 `csc.exe` 和 Roslyn 两条链路都能编译，零警告。详细说明见
[`docs/UI版说明.md`](docs/UI版说明.md)，技能清单见 [`docs/技能包清单.md`](docs/技能包清单.md)。

## License

本项目参考 [YaoGaoChaoLe/EAgent](https://github.com/YaoGaoChaoLe/EAgent)（GPL-3.0）的思路重写，
同样以 **GPL-3.0** 发布，仅供学习交流，不提供任何担保。完整条款见 [LICENSE](LICENSE)。
