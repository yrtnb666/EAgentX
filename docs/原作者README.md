# EAgentX

> 一个用 **C# / .NET Framework** 重写的 Windows 桌面智能体，思路来自开源项目 **EAgent**（易语言版）。
> **不使用易语言**，零第三方依赖，编译产物是**单个 exe**（约 180 KB）。

EAgentX 会把你的电脑交给 DeepSeek：你在窗口里写任务，模型给出 PowerShell 命令，
程序执行后把**真实输出**回喂给模型，形成 `思考 → 执行 → 观察 → 再思考` 的闭环。

```
你：把下载文件夹里的图片按拍摄日期重命名
EAgentX ▸ 计划：先列出图片文件，再按 LastWriteTime 重命名……
⚡ 执行命令
   Get-ChildItem "$env:USERPROFILE\Downloads" -Include *.jpg,*.png -Recurse
↳ 退出码 0 · 0.3 秒
   ...真实文件列表...
（把结果发回模型，继续下一步，直到 [TASK_DONE]）
```

---

## 1. 功能

| 能力 | 说明 |
| --- | --- |
| 免费网页版引擎 | 连接你已登录的 DeepSeek 网页版，**不需要 API Key**、不反代、不逆向，只用 CDP 做页面自动化 |
| API 引擎 | 支持任意 OpenAI 兼容接口（DeepSeek / 通义 / 本地 Ollama…），更稳定 |
| 命令审批 | 模型给出的每条命令默认弹窗确认；也可以显式开启「自动批准」（有风险） |
| 发送间隔 | 顶部「发送间隔」按钮可设置每一步之间等待几秒，降低网页版限流概率 |
| 上传文件 | 「上传文件」按钮会把文件附加到 DeepSeek 输入框；API 模式自动复制到工作目录 |
| 长命令少分段 | 提示词要求单条命令 6000 字符以内一次写完；超过才分段，每段 2000–6000 字符 |
| 回复完成判定 | 等消息底部动作栏出现、停止按钮消失后才取回复，避免把半截命令当成完成 |
| 命令完整性检查 | 引号 / 括号 / here-string 未闭合，或以 `-`、`\|`、`=` 结尾的命令会被拒绝执行，让模型重写 |
| 命令执行 | 在指定工作目录里用 PowerShell 执行，带超时、进程树清理、输出截断 |
| 闭环反馈 | 退出码 + 标准输出 / 错误输出回喂模型，模型据此决定下一步 |
| 会话轨迹 | 自动保存到 `<工作目录>\.eagentx\session-时间戳.md`，可审计 |
| 页面自检 | 「检测页面」按钮 / `--webcheck` 会显示输入框、发送按钮、回复节点、选择器等状态 |
| 选择器可配 | 页面改版时在设置里改 CSS 选择器即可，不必重新编译 |
| 单实例 + 深色界面 | 原生 WinForms，启动快，无浏览器外壳、无运行时依赖 |

> 与原版 EAgent 的差异：EAgent 用易语言 + 精易模块；EAgentX 用 C# 重写，并补上了命令审批、
> 会话轨迹、API 备用引擎、页面结构自检和更稳的回复提取（见第 8 节）。

---

## 2. 快速开始

1. 双击 `dist\EAgentX.exe`（需要管理员权限的任务，请右键「以管理员身份运行」）。
2. 点右上角 **工作目录**，选一个让智能体干活的文件夹。
3. 点 **连接 / 新对话**：
   - 首次会启动一个**独立配置目录**的 Edge（`%APPDATA%\EAgentX\edge-profile`），打开 <https://chat.deepseek.com/>；
   - 在浏览器里登录 DeepSeek；
   - 回到 EAgentX，点 **检测页面**，看到「输入框: 已找到」即可。
4. 在下方输入任务，例如「统计当前目录有多少个 .txt 文件」，**Enter 发送**。
5. 需要给模型看文件时点 **上传文件**：网页版会附加到 DeepSeek 输入框，API 模式会复制到
   `<工作目录>\.eagentx\uploads\`，并在输入框里写好路径。
6. 顶部 **发送间隔** 可设置每一步之间等待几秒（默认 3 秒），网页版频繁发送被限流时建议调到 10 秒。
7. 模型给出命令后会弹窗，确认无误点 **允许执行 (Enter)**。
8. 想从头开始聊，点 **连接 / 新对话**；它会让浏览器回到起始页并重新注入脚本。

> 提示：输入框里 `Enter` 发送、`Shift+Enter` 换行；窗口底部会显示引擎、工作目录和会话轨迹路径。

![界面](docs/screenshot.png)

---

## 3. 两种引擎

| | DeepSeek 网页版（默认） | API 模式 |
| --- | --- | --- |
| 费用 | 免费（用你网页版的额度） | 按 token 计费 |
| 配置 | 登录一次浏览器即可 | Base URL + API Key + 模型名 |
| 稳定性 | 依赖页面结构，可能随改版失效 | 稳定 |
| 风险 | 可能违反 DeepSeek 用户协议，存在账号风险 | 无额外风险 |

在 **设置 → 引擎** 里切换。API 模式的 Base URL 会补全为 `<base>/v1/chat/completions`
（如果 Base 已以 `/v1` 或 `/chat/completions` 结尾则直接使用）。

---

## 4. 命令协议

系统提示词要求模型：

- 每条回复**最多一条**命令，放在 ` ```powershell ` 代码块里；
- 收到「【执行结果】退出码 …」后再继续；
- 任务完成时输出 `[TASK_DONE]` 并总结。

宿主侧规则（`CommandParser`）：

1. 优先取**带 shell 语言标记**的代码块（`powershell/ps1/pwsh/cmd/bat/shell` 等）；
2. 只有一个无标记代码块时也当作命令（兼容模型偷懒）；
3. 回复里出现 `[TASK_DONE]` 且没有新命令 → 本轮结束；
4. 命令会剥掉 `$ `、`PS> ` 之类提示符前缀。

回喂格式：

```
【执行结果】退出码: 0
【耗时】340 ms
【输出】
```text
hello-eagentx
```
请根据以上真实输出决定下一步……
```

---

## 5. 配置

配置文件：`%APPDATA%\EAgentX\config.json`（设置窗口里改，保存即生效）。

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Engine` | `web` | `web` 网页版 / `api` 接口 |
| `WorkDir` | 用户主目录 | 命令执行目录，也是会话轨迹保存位置 |
| `ApiBase` / `ApiKey` / `ApiModel` | DeepSeek 官方 | API 模式用 |
| `CdpPort` | `9222` | 浏览器远程调试端口 |
| `EdgePath` | 自动探测 | 自定义 msedge.exe / chrome.exe |
| `StartUrl` | `https://chat.deepseek.com/` | 起始页，也用于「新对话」 |
| `ReplyTimeoutSec` | `300` | 等模型回复的超时 |
| `CommandTimeoutSec` | `120` | 单条命令执行超时（超时会杀进程树） |
| `MaxSteps` | `30` | 单次任务最多几步，防止死循环 |
| `SendIntervalSec` | `3` | 两次发送之间的最小间隔（秒），网页版限流时建议调到 10 |
| `AutoApprove` | `false` | 自动批准所有命令（危险） |
| `SystemPrompt` | 空 | 留空使用内置提示词 |
| `Sel` | 见下 | 网页 CSS 选择器 |

默认选择器：

```
Input      textarea#chat-input, textarea[placeholder], div[contenteditable='true']
Send       button[type='submit'], div[role='button'][aria-label*='发送'], ...
Reply      .ds-markdown, .markdown, [class*='markdown']
Generating div[role='button'][aria-label*='停止'], ...
```

页面改版导致「找不到输入框」时：打开 **设置 → 网页选择器**，按「检测页面」输出里的
「按钮候选 / 相关类名」调整即可。

---

## 6. 自检命令

```powershell
.\dist\EAgentX.exe --selftest    # 12 项离线自检：JSON、命令解析、命令完整性、stderr 解码、配置、提示词、PowerShell、发送间隔、浏览器定位、界面控件
.\dist\EAgentX.exe --webcheck    # 启动/复用浏览器，连接 CDP，注入脚本，输出页面状态（不要求登录）
.\dist\EAgentX.exe --chatcheck   # 真的发一条 "请回复 OK"，验证发送与回复提取
.\dist\EAgentX.exe --agentcheck  # 在临时沙箱里完整跑一遍：建文件 → 执行 → 回喂 → [TASK_DONE] → 校验产物
.\dist\EAgentX.exe --longcheck   # 新对话里让模型写 3000+ 字符 HTML，验证长命令一次写完、不被截断
.\dist\EAgentX.exe --uploadcheck # 真的往当前 DeepSeek 输入框附加一个测试文件，验证网页附件
```

报告写到 exe 同目录的 `selftest.log` / `webcheck.log` / `chatcheck.log` / `agentcheck.log` /
`longcheck.log` / `uploadcheck.log`。

本次交付前的实测结果（Windows + Edge 143 + DeepSeek 网页版）：

- `--selftest`（v1.0.4）：12/12 通过（发送间隔实测 1007 ms；新增命令完整性、stderr 中文解码检查）
- `--uploadcheck`：成功把 `eagentx-upload-test.txt` 附加到 DeepSeek 输入框，页面出现附件卡片
- `--webcheck`：找到输入框 `TEXTAREA 给 DeepSeek 发送消息`
- `--chatcheck`（v1.0.4）：成功发送并收到回复 `OK`
- `--agentcheck`（v1.0.1）：模型给出 `Set-Content` 命令 → 沙箱执行 → 校验 `hello.txt` 内容为 `hello-eagentx` → 通过
- `--longcheck`（v1.0.4）：新对话里模型**一次输出 5486 字符**的 here-string 命令，沙箱生成
  5127 字符的 `long.html`；命令次数 1，不完整告警 0，产物含 `<!DOCTYPE html>` 与
  `EAGENTX-LONG` 标记 → 通过
- v1.0.2 复测备注：DeepSeek 服务端曾延迟约 3 分钟才返回完整回复，旧测试的 180 秒超时先触发；
  页面随后出现的 `Set-Content` 命令完整、未被截断。测试超时已提高到 300 秒。
- v1.0.4 现场验证：长回复生成过程中页面右侧始终是**停止方块**，完成后才出现消息底部动作栏；
  另一次新对话里模型一次输出 5519 字符的 `Set-Content` 命令，均符合新的完成判定条件。

> v1.0.1 修复：在已有对话里发送追问时，DeepSeek 的回复节点数量可能保持不变，
> 旧逻辑会一直等待。现在改为按「最后一条用户消息之后的最新回复」取内容，并校验消息确实提交。
>
> v1.0.2 新增：**发送间隔**按钮（每一步之间至少等待 N 秒）和**上传文件**按钮（网页版原生附件，
> API/上传失败时自动复制到 `<工作目录>\.eagentx\uploads\`）。同时用右侧主按钮的**停止方块图标**
> 判断「正在生成」，识别「消息发送过于频繁」提示。
>
> v1.0.3 调整：模型之前把 HTML 拆成很多 300–500 字符的 `Add-Content`，导致来回几十次。
> 现在系统提示词明确：**单条命令 6000 字符以内一次写完**；只有超过 6000 字符才分段，
> 且每段尽量 2000–6000 字符、标注「第 n/m 段」。如果你在设置里填了**自定义提示词**，
> 它会覆盖内置规则，需要把这段要求也加进去。
>
> v1.0.4 修复：网页版长回复在生成中途会有短暂停顿，旧逻辑只看「停止按钮暂时消失 + 文本稳定
> 1.6 秒」，可能把半截命令当成完成；下一条消息又会打断模型，最终只剩 `$a`、`(`、
> `Add-Content ... '` 这类残片。现在同时要求**消息底部动作栏出现**、**停止按钮消失**、
> **文本稳定 1.6 秒**才取回复；如果一直没抓到动作栏，则要求稳定 6.4 秒。
> 另外新增命令完整性检查，残片不会被送进 PowerShell；宿主收尾代码和用户命令拆成两个文件，
> 不会再出现「收尾代码被半截括号吞掉」的语法错误；stderr 改为 UTF-8 优先、GBK 兜底解码，
> 语法错误也不会再显示成乱码。

---

## 7. 从源码构建

要求：Windows 10/11，系统自带 .NET Framework 4.x（`csc.exe`），**不需要联网、不需要 NuGet**。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

产物：`dist\EAgentX.exe`。图标由 `tools\make_icon.py` 生成（纯标准库，无 Pillow 依赖）。

### 目录结构

```
EAgentX/
├─ src/
│  ├─ Program.cs                 入口 / 单实例 / 全局异常
│  ├─ SelfTest.cs                六种命令行自检
│  ├─ AssemblyInfo.cs            版本信息
│  ├─ Util/                      JSON、日志、配置（含选择器）
│  ├─ Core/
│  │  ├─ AgentSession.cs         智能体主循环（发送→解析→审批→执行→回喂）
│  │  ├─ CommandParser.cs        从回复中提取命令 / [TASK_DONE]
│  │  ├─ CommandShape.cs         引号/括号/here-string 完整性检查
│  │  ├─ PromptBuilder.cs        系统提示词与回喂格式
│  │  └─ PowerShellRunner.cs     带超时、输出截断与编码兜底的执行器
│  ├─ Net/
│  │  ├─ CdpClient.cs            WebSocket + JSON-RPC 的极简 CDP 客户端
│  │  └─ EdgeLauncher.cs         启动 Edge、发现页面、必要时导航
│  ├─ Engines/
│  │  ├─ IChatEngine.cs          引擎抽象
│  │  ├─ WebEngine.cs            网页版：注入脚本、真实回车、轮询回复
│  │  └─ ApiEngine.cs            OpenAI 兼容接口
│  └─ Ui/                        MainForm / SettingsForm / ApproveForm / DelayForm / Theme
├─ docs/设计说明.md               架构与关键实现
├─ tools/make_icon.py            生成 icon.ico
├─ build.ps1                     一键编译
└─ dist/EAgentX.exe              构建产物
```

---

## 8. 关键实现（为什么能跑通）

1. **CDP 连接**：`--remote-debugging-port=9222 --user-data-dir=<独立目录>` 启动 Edge，
   用 `http://127.0.0.1:9222/json/list` 找页面，再用 `ClientWebSocket` 说 JSON-RPC。
2. **输入**：通过 `Runtime.evaluate` 调用原生 `HTMLTextAreaElement.prototype.value` 的 setter
   并派发 `input` 事件，React 才会更新状态。
3. **发送**：不依赖按钮（DeepSeek 的发送键是无文字 `div.ds-button--primary`），
   直接用 `Input.dispatchKeyEvent` 发**真实 Enter 按键**，再确认消息节点增加 / 开始生成；
   不能只看输入框是否清空，否则会把「清空但没提交」误判成发送成功。
4. **取回复**：只取**最后一条用户消息之后**的助手回复，避免节点合并或旧消息造成误判；
   关键点是 **innerText 不含 Markdown 围栏**，所以会把 DOM 里每个 `<pre>` 反向序列化成
   ` ```语言 …``` ` 再交给解析器。完成判定同时看三件事：消息底部**动作栏**（复制/重新生成）
   是否出现、右侧**停止方块**是否消失、文本是否稳定 1.6 秒——避免把生成中途的停顿当成完成。
5. **稳定性**：多级选择器 + 邻近度评分找按钮 + 页面不在 DeepSeek 时自动 `Page.navigate`
   + 分阶段写日志（`%APPDATA%\EAgentX\logs\app.log`）。

更多细节见 [docs/设计说明.md](docs/设计说明.md)。

---

## 9. 常见问题

**找不到输入框 / 一直提示登录**
先在自动打开的 Edge 里登录，再点「检测页面」。仍失败就改选择器（设置 → 网页选择器）。

**等待回复超时 / 消息发送过于频繁**
「消息发送过于频繁，请稍后重试」是 DeepSeek 的**账号级网页限流**，不是程序崩溃。
把顶部「发送间隔」调到 10 秒，停止几分钟后再试；频繁重试可能延长限制。
程序会立即识别这条提示并结束本轮，不会把它当成模型回复继续空等。

**上传文件后模型没反应**
网页版附件会出现在 DeepSeek 输入框上方；若网页上传失败，程序会把文件复制到
`<工作目录>\.eagentx\uploads\`，并把本地路径写进输入框，模型可以用 PowerShell 读取。
API 模式没有网页附件，总是走本地复制。

**模型总是把文件拆成很多小段**
v1.0.3 已在内置提示词里要求「单条 6000 字符以内一次写完」，v1.0.4 又修掉了「半截回复被当成完成」
的问题。若仍频繁分段，先检查 **设置 → 自定义提示词**是否填过内容——非空时会完全覆盖内置提示词，
请把「长命令与分段」「命令完整性」要求也加进去。如果当前对话已经因为多次截断变得“谨慎”，
点 **连接 / 新对话** 开一个干净对话再试；实测新对话里模型可以一次写出 5000+ 字符的命令。
也可以把「发送间隔」调大，避免分段时触发限流。

**浏览器没起来 / 端口被占用**
改设置里的 CDP 端口；确认 9222 没被别的调试浏览器占用；检查安全软件拦截。

**杀毒软件报毒**
exe 没有代码签名，且会调用 PowerShell，容易被启发式误报。源码全在 `src/`，可自行编译。

**中文乱码**
编译脚本已指定 `/codepage:65001`；PowerShell 执行时也强制 UTF-8 输出。v1.0.4 起，
stdout/stderr 都按原始字节读取，先尝试 UTF-8，失败再按系统 ANSI（中文系统为 GBK）解码，
所以 PowerShell 语法错误这类“发生在设置编码之前”的输出也不会再变成乱码。

**能替代原版 EAgent 吗**
思路一致：工作目录、自动开浏览器、登录、开始新对话、执行 PowerShell、回喂结果。
EAgentX 额外提供命令审批、轨迹、API 备用引擎和自检，但**网页自动化本身的风险相同**。

---

## 10. 安全与合规

- 模型生成的命令**可能有害**。默认每条都要你确认；开启「自动批准」等于把电脑交给模型。
- 破坏性操作（删除、格式化、改注册表、结束进程）请在审批窗口里仔细看，再决定。
- 网页自动化 + 自动发消息可能违反 DeepSeek 用户协议，**存在账号被限制的风险**，请自行评估。
- 程序只会在你选择的工作目录执行命令；但 PowerShell 本身没有沙箱，命令仍可访问全盘。
- 除 DeepSeek 网页版 / 你配置的 API 外，本程序不向任何第三方上传数据；日志与会话轨迹只存本机。

## License

参考 [YaoGaoChaoLe/EAgent](https://github.com/YaoGaoChaoLe/EAgent)（GPL-3.0）的思路重写，
本实现同样以 **GPL-3.0** 发布，仅供学习交流，不提供任何担保。
