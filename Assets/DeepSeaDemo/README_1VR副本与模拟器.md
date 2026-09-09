# 1-VR 副本：本轮工具与模拟器说明

> 当前是实施中的版本。副本已创建，但新配置工具尚未在 Unity 中执行。菜单实测、关卡装配和全流程验收未完成。详情见 `Reports/1VR-Implementation-Status.md`。

## 文件与安全边界

- 保留原场景 `Assets/Scenes/1-VR.unity`。
- 新副本 `Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity`。
- 不要运行旧的 `01 Build Isolated Demo` 来更新新副本：它仍是上一版空场景生成流程。
- 不要将当前副本误认为已经加入全部新菜单/任务的完成场景。

## 新增菜单

Unity 导入完成后，在 `Tools > Deep Sea Demo` 下可见：

| 菜单 | 用途 | 是否改场景 |
| --- | --- | --- |
| 10 Copy saved 1-VR (non-destructive) | 原文件不存在副本时才复制；已有副本不会覆盖；隔离 TerrainData/VolumeProfile 并输出基线记录 | 仅副本 |
| 11 Configure 1-VR Copy Input | 给已有 XR Origin 添加统一输入；绑定现有左右手 UI Press；配置标准 EventSystem | 仅副本，保留移动及追踪参数 |
| 12 Apply Deep Water Profile to Active Copy | 将深海参数应用到当前打开的副本；修改后留待视觉复查与保存 | 仅当前副本；支持 Undo |
| 13 Input Diagnostics (read-only) | 查看设备、Trigger、Grip、摇杆、悬停、持物和任务状态 | 不修改；不跳关 |

菜单 10、11 会在任一打开的场景未保存时拒绝继续。请先自行保存或另存备份。

## 两种测试模式

选择副本中的 XR Origin（包含 Quest Left Stick Locomotion 的节点），找到 `Demo XR Input`：

- `XR Simulator`：默认模式。使用模拟手柄完整测试；菜单不接收鼠标直接点击。
- `Desktop UI`：只为鼠标测试 UI。启动时关闭配置过的模拟器根物体，避免争抢鼠标。它不能代替双手抓取/QTE/通关验收。

切换模式后重新进入 Play。Windows 构建中 `Application.isEditor` 为 false，模拟器与 Desktop UI 调试入口不会开启。

## 当前项目的实际模拟器按键

以下来自项目内 XRI 3.3.1 的三个 `.inputactions` 文件，不是推测的通用按键。配置过其他方案时，以模拟器屏幕提示为准。

| 按键 | 模拟器动作 |
| --- | --- |
| `Tab` | 循环选择当前操作设备 |
| `[` / `]` | 切换操作左手 / 右手 |
| `H` | 切换操作头部 |
| 鼠标右键 | 切换鼠标操作；鼠标移动提供旋转输入 |
| `W/A/S/D` | 模拟器设备平移，不等于游戏左摇杆 |
| `Q/E` | 模拟器设备升降，部分模式也作用于另一手摇杆 |
| `I/J/K/L` | 模拟所选手柄二维摇杆：上/左/下/右 |
| `G` | 模拟 Grip，用于抓取；QTE 使用另一只空手 |
| `T` | 模拟 Trigger，菜单点击、道具使用或空手声纳 |
| `M` | 模拟手柄 Menu；游戏另支持编辑器 Escape |
| `R` | 模拟器重置，不要在 XR Simulator 模式当作 QTE 判定键 |
| `Shift` | Left Device Actions 修饰动作，具体取决于模拟器当前选择状态 |

需要测试玩家移动时，先选择左控制器，再用 I/J/K/L 模拟左摇杆。要测试上潜/下潜则选择右控制器，用 I/K；右摇杆 J/L 用于转向。

旧桌面 L 读文档、Q 下潜、R 判定 QTE 在 Demo 的 XR Simulator 模式中已隔离。通过 T/G 和模拟摇杆测试正式交互。

## 输入排查顺序

1. 打开菜单 13 的诊断面板，进入 Play，检查设备列表是否出现模拟 XR 设备。
2. 左手/右手 `Tracked` 应为 true。若为 false，先检查模拟器设备选择和源场景的姿态输入，不要移动声纳发射器来补偿。
3. 按 T，所选手 Trigger 应从 0 变为接近 1；按 G，Grip 同理。
4. 按 I/J/K/L，所选手 Stick 应变化。若诊断没变化，先解决 InputAction 或模拟器模式，不要调整移动速度。
5. 菜单应通过 Near/Far 或 XR Ray Interactor 的 `UI Press`、XRUIInputModule、TrackedDeviceGraphicRaycaster 到达 Button.onClick。
6. 指向按钮时应有高亮，T 按下/松开应触发点击。水下点 UI 不应顺便发波。
7. 暂停时应能继续指向菜单。该项目前尚未实测；尤其需检查模拟器的时间缩放行为。

## 英文文本

`DemoTextCatalog` 集中保存英文界面文字，稳定键例如 `runtime.026`（New Game）、`log01.title`、`log01.body`。

正式装配时，DemoConfig 的 `Text` 槽应引用 `Settings/1VR/EnglishText.asset`。未绑定时运行时采用内置英文默认值，不影响任务/存档的稳定 ID。

四篇日志已写好，但现有源场景全部标牌和文档的迁移/摆放尚未完成。日志窗口每页最多 720 字符，通过 `<` / `>` 翻页。

## 深海参数

执行菜单 12 后，会创建 `Settings/1VR/DeepWater.asset`。编辑参数后再次显式执行菜单 12 应用；不会每次编译自动覆盖你的修改。

- `Fog Color`：远处水雾最终颜色，默认近黑蓝绿。
- `Fog Extinction × Fog Density`：按视距衰减，越高可见距离越短。
- `Depth Extinction × Depth Strength`：随实际水深变暗，独立于水面平台曝光。
- `Scatter Ambient / Sun / Intensity`：水体散射亮度，避免整个海床泛亮。
- `Caustic Intensity / Depth Fade`：焦散强度及深处衰减。
- `Headlamp Radius`：默认 0.65m，仅近身区域；`Flashlight Range` 默认 5m。
- 三个 Test Depth 是截图验收位置，不是控制曝光的开关。

应用不会修改全局曝光或太阳灯强度。最终值需要以 5m、15m、22m 的实景测试调整；当前数值只是待验证的初始方案。

## 仍待完成

不能用“脚本编译成功”替代：菜单双手点击、日志翻页、工具使用、QTE、黑匣子实际插座播放、两种结局、死亡重试、继续存档、舱盖净空、三组深海截图以及 Quest 双眼与性能。
