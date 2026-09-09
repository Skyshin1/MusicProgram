# 1-VR 副本实施记录 — 2026-09-09（早期历史记录）

后续已在 Unity 中装配并保存完整流程，使用用户指定的现有平台。最新状态见 `1VR-Current-Status.md`；本文件以下内容保留为早期实施记录，不再代表当前进度。

## 当前结论

尚未交付完整、验收通过的 Demo。本轮完成了保存场景副本、输入与菜单代码修复、英文文本基础及显式配置工具；配置工具尚未在 Unity 内执行。

## 原场景保护

- 复制前 Unity 的 `1-VR` 窗口没有未保存星号。
- 原文件：`Assets/Scenes/1-VR.unity`。
- 副本：`Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity`。
- 复制时两者 SHA-256：`5D464BC9807B94609374DD8FC62766F278C60908862377F497003D2FCFB4BB44`。
- 没有删除场景物体；没有调用上一版空场景生成器；没有覆盖上一版 Demo 场景。
- 本轮未修改原场景文件、水体共享材质、TerrainData、Renderer 或画质资产。新配置工具将在执行时建立自己的依赖副本。
- 旧玩法脚本增加的是 DemoXRInput 存在时的兼容分支，未安装该组件的旧场景仍使用原输入方式。Demo 专属运行时脚本的菜单文字已改为英文。

## 已写入的代码

- DemoXRInput：统一 InputActionReference 的 Trigger、Grip、摇杆、追踪和菜单输入。
- 移动、手套、声纳、文档、QTE 的 Demo 输入分支；XR Simulator 模式隔离 R/L/Q 桌面测试按键冲突。
- DemoUI：Button/onClick、XRUIInputModule、TrackedDeviceGraphicRaycaster；菜单不再使用 BoxCollider，新增按钮颜色反馈和日志翻页。
- DemoInputRouter：使用选择过滤器拦截暂停/QTE 时的世界抓取，不禁用交互器本身；UI 优先于声纳和道具 Trigger。
- DemoTextCatalog：英文菜单、目标、提示、结局、四篇英文日志；日志和存档 ID 不变。
- DemoDeepWaterProfile：仅面向水体的消光、散射、焦散、头灯与手电参数。
- 显式工具：复制及依赖隔离、绑定副本输入、应用深海参数、只读输入诊断。

## 已完成的检查

- 离线 Roslyn 编译（Runtime + Editor）：通过，仅现有序列化字段 CS0649 警告。
- 静态输入/UI/文本引用契约检查：见 Tools/Test-1VRInputContract.ps1。它不是运行时测试。
- 项目原有 YAML/材质有尾随空格，未为了清理格式改写用户资产。

## 阻塞与尚未完成

Windows 窗口工具在读取和恢复 Unity 窗口时持续返回 `foreground window did not report a process id`。按 computer-use 技能的恢复规则，已停止继续尝试窗口输入。没有使用其他 UI 自动化绕过。

因此以下均未验收，也没有据此声称完成：

- Unity 实际导入和编译、执行配置器、把新的服务完整绑定到副本。
- 标准菜单左右手真实悬停、点击、暂停可操作；特别需要验证模拟器在 Time.timeScale=0 时的姿态操作。
- 从 New Game 到两个结局的非跳关模拟器通关。
- 源场景已有文档/标牌/第三方 UI 的全部英文迁移。四篇新日志已写好，但尚未放置绑定。
- 路线布景、地形起伏、潜艇顶部舱盖净空、敌鱼导航与关键物品不可达区恢复。
- Renderer/Quality/Volume/Terrain/材质的完整副本隔离与配置后的再次验证。
- 同位置工具关闭/手电/声纳三组实际截图。
- Windows PC-VR 构建、Quest 3 双眼、72Hz CPU/GPU 性能。
- UniStorm 官方 5.4+ Render Graph 支持包仍需独立确认，不能标记完成。

## 接续点

保持桌面解锁，将 MusicProgram 的 Unity 6000.3.7f1 窗口置于前台；先恢复窗口操作能力。随后导入脚本、执行菜单 10/11，检查输入绑定，再继续正式关卡装配，不应直接把当前磁盘副本当作完成版。
