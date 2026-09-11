# 完整流程测试 — 现有平台版

施工及测试场景：`Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity`。

不要打开上一版 `DeepSeaInvestigation.unity` 测试本轮改动，也不要运行旧的菜单 `01 Build Isolated Demo`。原始 `Assets/Scenes/1-VR.unity` 保留。

## 重要状态

场景已执行装配并保存，完整任务节点和物品引用通过检查。实际测试结果以 `Reports/1VR-PlaySmoke.txt` 为准；API 自动检查不代表已经通过手柄全流程、头显双眼或性能验收。UniStorm 官方 Render Graph 包仍需单独确认。

## 开始

### 先选择真实头显或模拟器（二选一）

退出 Play，在当前 `_1VR` 场景中使用：

- **真实 Quest 3 / Link：** `Tools > Deep Sea Demo > Input > Real VR - Quest Link`。
- **键盘模拟器：** `Tools > Deep Sea Demo > Input > XR Simulator - Keyboard`。
- **只用鼠标检查菜单：** `Tools > Deep Sea Demo > Input > Desktop UI - Mouse`（不是完整桌面游玩模式）。

选择后按 `Ctrl+S` 保存，再进入 Play。也可在 `Player/Player (2)` 的 `Demo XR Input` 中查看 `Editor Mode`。不要只取消模拟器物体的勾选却仍保留 `XR Simulator` 模式，否则该模式会按配置重新启用它。

Real VR 模式在启动早期关闭已绑定模拟器；配置菜单还会取消 `Simulated Device Lifecycle Manager` 的 `Remove Other HMD Devices`，防止模拟器意外启用时移除真实头显。若此前真实头显已经被模拟器移除，请退出本次 Play 后重新测试，不在同一运行中来回切换。

模式菜单仅修改当前打开的副本并标为未保存，不会重新加载场景或覆盖已有未保存改动。Windows 构建始终不启用模拟器。此模式修复不代替真实头显连线和 OpenXR 运行环境检查。

### Real VR 有声音但头显没画面

检查曾发现工程的 `Initialize XR on Startup` 为关闭状态。仅关闭模拟器并不等于启动 OpenXR。目前已经恢复工程的 Windows XR 自动启动，并移除了旧桌面保护脚本自动关闭/保存该开关的逻辑。Demo 的 `DemoXRSessionBootstrap` 复用 Unity 已启动的 Loader；仅在 Real VR 缺少 Loader 时尝试补充启动。模拟器和鼠标模式不执行此手动启动；需要纯桌面测试时，应明确选择测试配置并检查工程自动启动设置。

退出 Play，等待脚本编译完成，保持 Quest Link / Air Link 已连接，再重新 Play。在 Console 搜索 `[DeepSeaDemo XR]`，或查看工程目录的 `Logs/DeepSeaDemo/XR-Startup.txt`（追加日志，检查最新时间）：

- `Initializing configured XR loader`：正在尝试启动 XR，不代表已成功。
- `XR display stable for 3 seconds`：Unity 的 XR 显示子系统已持续运行三秒，仍需戴头显确认实际画面、双眼和追踪。旧版单帧 `XR display running` 不能作为成功依据。
- `FAILED`：按后面的原因检查 Loader、运行环境或 Link 连接；播放声音不是 XR 启动成功的依据。

2026-09-09 的原生日志发现 `eyegaze/pose` 不支持错误并紧接会话退出；已关闭 Windows OpenXR 的 `Eye Gaze Interaction Profile`，不关闭 Quest 手柄或头部追踪。这是共享 Windows XR 配置。具体证据及尚未验证的项目见 `Reports/1VR-XR-Startup-Diagnosis.md`。

已有 Windows 测试构建早于此次启动修复，不会随脚本修改自动更新。本次应先在 Unity 编辑器测试；验证后再重新构建。

1. 打开上述 `_1VR` 场景，等待脚本编译完成。
2. 进入 Play。选 `New Game`。菜单采用标准 Unity Button，不是物理碰撞方块。
3. `Continue` 读取本场景独立存档 `DeepSeaInvestigation_1VR.checkpoint.json`，不会读取另一版 Demo 的存档。
4. 玩家在原平台的装备准备室开始。不要移动整个平台来对齐新道具。

菜单和阅读卡会跟随头显，位于前方约 1.25m；无需转身寻找菜单。暂停仍保留双手追踪和 UI 输入。

### Windows 测试构建

已生成 `Builds/DeepSea1VRPCVR/DeepSeaInvestigation.exe`。需要整份同名文件夹内的数据与 DLL，不能只复制 EXE。它面向 Quest 3 Link / Air Link 的 Windows PC-VR，不是安装到头显的 APK；构建中已关闭编辑器模拟器。

**当前是带已知问题的测试构建**：Unity 显示 Succeeded，但有 25 条旧插件 Shader 错误和 116 条警告。详见 `Reports/1VR-WindowsBuild-Diagnostics.txt`。本轮没有以真实头显启动和通关，不应将其视为最终发布版本。编辑器现有菜单截图与 21 项 API 测试通过，也不能代替手柄操作和双眼性能验收。

## 模拟器按键

以下按键来自项目当前 XR Interaction Simulator 的输入资产，不是通用 Unity 快捷键：

| 操作 | 模拟器 |
|---|---|
| 选择左／右控制器 | `[` / `]` |
| 选择头部／切换设备 | `H` / `Tab` |
| 操纵模拟设备位置 | `W A S D`，`Q E` 升降 |
| 操纵所选控制器摇杆 | `I J K L` |
| Grip 抓取／松开 | `G` |
| Trigger 使用／点击 | `T`，点击需按下并松开 |
| 菜单 | `M`；Demo 另支持 `Esc` |
| 鼠标操纵开关 | 鼠标右键 |

摇杆玩法：左控制器摇杆移动；右控制器摇杆横向转向、纵向升降。模拟器的 `WASD` 是模拟设备运动，不等同于正式游戏移动。`R` 是模拟器重置，不是该模式下的 QTE 键；QTE 用另一只空手的 `G`。

`Tools > Deep Sea Demo > 13` 输入诊断窗口可检查 Trigger、Grip、摇杆、追踪及持物状态。游戏内标牌、菜单、日志和提示为英文。

## 平台调查

- **Room_Equipment_Preparation（装备准备室）**：手电、穿戴确认站、`Begin Dive` 面板。
- **Room_Log_Monitor（日志监控室，同一甲板另一侧）**：四份日志和报警终端。
- **一楼解析工作台（已由场景布局移动）**：黑匣子插座、水样接收座，无需上二楼。`Room_BlackBox_Analysis` 是原来的上层定位点，不再作为寻找设备的依据。

先阅读 `Routine Voyage Log`：空手指向日志盒，按 Trigger，阅读面板可翻页并关闭。再检查报警终端。其余三份日志可以重复阅读。

回装备室，用 Grip 抓起手电，按 Trigger 开灯。确认 `Equip Suit`。只有首份日志、报警差异、手电和潜水服都完成，主线才允许下潜。

用空手 Trigger 操作 `Begin Dive`，经过淡入淡出到平台外的水面入口。此交互不跳过任务条件。也可以从已有平台通路自行入水。

## 下潜与海床

右摇杆向下下潜；停止升降输入会缓慢下沉；浮到水面后停在水面，继续向下才再次下潜。

- 水下空手 Trigger：从该手位置发声纳。
- 持物手 Trigger：交给物品，不发声纳。另一只空手仍可扫描。
- 水上 Trigger：交互和阅读，不发声纳。
- 手电 Trigger：开关灯，放手后保留开关状态。
- 氧气仅在已装备且水下时消耗，上水补充。

沿管线探索：维修笼有橙色电子锁维修工具、电子卡；排放口有可选水样、传感器日志和证词；另一条管线通向潜艇。投石撞击配置好的结构可发出碰撞声纳，诱导敌鱼；连续主动声纳会提高声学暴露。

## 潜艇维修

1. 用 Grip 拿维修工具。潜艇的实际顶部 `Door` 分组是维修目标。
2. 把手电放到舱门旁的架子，腾出另一只手。
3. 工具靠近锁，持续按工具手的 Trigger。
4. 圆环 QTE 出现时，用**另一只空手 Grip**判定。模拟器切换到另一只控制器再按 `G`。
5. 成功正常继续；完美获得短时 1.5 倍增益；失败／超时倒退维修进度。损坏 Decal 随进度渐隐。
6. 维修完成后离开舱盖扫掠范围，并移开工具或散落物，门才会安全打开。不要一直把手和工具挡在舱盖上。
7. 下到进水舱内，读取最后记录，拿出黑匣子。备用引擎声只触发一次，敌鱼会调查舱外声源。

## 返回与结尾

沿灯标上浮，在平台外橙色 `Board Platform` 面板附近，用任意一只手指向并按 Trigger 登乘。双手分别拿水样和黑匣子时也能操作，两件证据随玩家一起返回。登乘位置有碰撞检查和淡入淡出。

把黑匣子带到平台一楼解析工作台，**实际放入黑匣子插座**（另一个是水样接收座）。仅靠近、查看或点击附近区域不会完成解析。插入后等待录音结束，出现证据分析面板。

水样可放入右侧接收座；传感器日志和证词读取后登记，不要求搬运。缺少可选证据仍能完成主线。

- `Preserve Evidence`：保留证据结局。
- `Upload to Company`：需二次确认，演出游戏内资料删除后进入另一结局。不会删除工程或真实文件。

## 暂停、失败与找回

暂停可继续指向 UI；基础追踪和模拟器时间不被冻结。菜单提供返回安全位置和重试。氧气耗尽／死亡使用章节检查点恢复。手电上浮，维修工具、卡片、石头、黑匣子、水样下沉；抓住时抑制水体力。

关键物品越界会移回对应找回点；持有中或已提交的物品不复制。狭缝不可达区域的完整恢复仍须结合场景实际碰撞验证，不能把越界找回当作所有丢失情况均已解决。

## 配置位置

- 场景：`1VR Complete Investigation Modules/Services` 的 `DemoFlow` 查看四个检查点、任务配置、门、敌人和 UI 绑定。
- 原平台下：`Investigation Interactions - Existing Platform` 查看日志、装备、登乘与解析对象。
- 水体：原 `WebGPU Water`，保留现有水面，扩展副本水域范围使主线路线处于水体内。
- 深海表现：`Settings/1VR/DeepWater.asset`。菜单 `12` 显式应用到当前副本，不会每次编译自动重设。
- 英文内容：`Settings/1VR/EnglishText.asset`。
- 输入：原 XR Origin 上的 `DemoXRInput` 和 `DemoInputRouter`。
- UI 渲染：副本管线 Renderer 0 中的 `Demo UI After Water`，在水下效果后绘制。不要删掉这个 Feature，也不要重新启用旧 `DeepSeaDemo UI Camera`。场景中保留它仅供检查，正式使用的是末尾 UI Pass。
- 检查报告：`Reports/1VR-FlowAssembly.txt`、`1VR-CheckpointSafety.txt`、`1VR-PlaySmoke.txt`。

菜单 `14` 仅首次装配；检测已有模块后只审计，不重建手工布景。菜单 `16` 用于完善现有平台交互引用与安全检查；不创建替代平台。
