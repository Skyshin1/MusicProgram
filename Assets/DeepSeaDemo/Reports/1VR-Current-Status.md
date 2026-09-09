# 当前交付状态：现有平台完整流程

日期：2026-09-09。目标为用户明确确认的 `Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity`。

## 已实际写入场景

- 保留原平台 `__OilRigGenerated`，世界坐标 `(1.30,-1.04,-1.50)`、单位比例不变，保留其原有碰撞。没有替代平台或缩放原平台。
- 使用现有装备准备室、日志监控室、黑匣子解析室，配置装备、四份英文日志、报警核对、下潜入口、返回登乘和证据插座。
- 保留源 XR 玩家与水体；补齐相机引用、统一 InputAction 适配、UI 射线绑定、暂停保护和独立的末尾 UI 绘制 Pass。
- 场景内有手电、维修工具、电子卡、投石、水样、黑匣子。具备抓取、浮沉、物品 Trigger 与声纳输入优先级。
- 海床管线分支连接维修笼、排放口和潜艇。源 SubMarine 实例及实际 Door 已绑定 QTE、Decal 维修、舱盖铰链、开门阻挡检查、舱内记录和黑匣子。
- 黑匣子取走触发一次性引擎噪声，回到实际插座并播放完成后进入解析；可选证据不阻塞两个结局。
- 1 条敌鱼、两种普通鱼共 10 条、导航和巡逻点已装配。声纳、碰撞声纳、敌人听觉与暴露服务已绑定。
- 副本专用配置、Renderer、Quality、英文文本和独立存档名称已设置。
- 测试方块、旧占位鱼、僵尸测试敌人和旧道具服务只在副本内停用，仍可恢复。

## 已运行检查

1. Unity 内场景装配及引用审计通过，报告 `1VR-FlowAssembly.txt`。
2. 四个检查点胶囊空间检查通过，报告 `1VR-CheckpointSafety.txt`。出生点在原房间内小幅避开遮挡，不搬动平台。
3. 最终 API Play Smoke（2026-09-09 12:34，北京时间）全部通过：菜单跟随头显并处于正前方 1.25m、标准按钮回调、暂停/设置/返回、日志/报警交互、装备推进、维修回退与开门、黑匣子插座、播放后的解析、两个结局、章节重试、氧气恢复和门状态恢复。报告中没有 FAIL 或 RUNTIME ERROR。
4. 主菜单与保留证据结局已通过实际渲染截图检查。截图在 `Logs/DeepSeaDemo/Previews/MainMenu.png` 和 `PreserveEnding.png`。此前没有 UI 的截图和中断报告不是最终结果。
5. 本工程独立 Overlay 相机路径未能绘制运行时 UGUI，因此副本改用 URP 标准 Render Objects Feature：`Demo UI After Water`，AfterRendering / 1000，UI Layer 单独绘制、深度 Always、不写深度。主相机包含 UI culling layer，普通透明/不透明 Pass 排除 UI，避免提前绘制再被水雾覆盖。旧 UI 相机保留但停用，不在堆栈中。
6. Runtime + Editor 离线编译及静态输入契约检查通过。Unity 已导入并编译装配器。
7. Windows PC-VR Development 构建已生成：`Builds/DeepSea1VRPCVR/DeepSeaInvestigation.exe`，Unity 结果 Succeeded，2165718568 bytes，耗时 25 分 23 秒。但构建同时报告 **25 errors / 116 warnings**，不是无错误验收版本。

### 构建错误范围

完整明细见 `1VR-WindowsBuild-Diagnostics.txt`，不是只看编辑器 Console 推测：

- 旧 `Hidden/VolumetricFog`：14 条 D3D11 编译器 Invalid Bytecode 错误。
- 旧 `Hidden/Post FX/*`：8 条深度纹理采样器错误。
- UniStorm Built-in Atmospheric Fog、Sun Shafts、Lightning Bolt：3 条 XR/深度纹理错误。
- 本次错误明细没有列出新 Water Volume 或 UI Shader；这不等于已验证构建中的所有材质、天气或左右眼效果。
- 没有通过删掉原插件、强制隐藏错误或替换天气系统来制造“零错误”。正式天气的官方 URP / Render Graph 支持前置条件仍未验收。
- EXE 已生成但尚未启动并完成头显实测。交付整份 `DeepSea1VRPCVR` 文件夹，不能只复制 EXE。

原始 `Assets/Scenes/1-VR.unity` SHA256 仍为：
`5D464BC9807B94609374DD8FC62766F278C60908862377F497003D2FCFB4BB44`。
该文件此前已有用户 Git 修改，本轮没有用版本库状态覆盖它。

## 明确尚未验收

- 实际模拟器/双手控制器从 New Game 到结尾不跳步通关；API 调用不能代替。
- 菜单双手射线悬停/点击、换手、真实 QTE 按键、潜艇舱盖进出和走道净空。菜单显示与 Button 回调通过不等同于这些输入已实测。
- 深水关闭工具/手电/声纳三组同位置效果、双眼一致性和 72Hz 帧时间。
- 所有关键物品掉入复杂缝隙的找回（目前越界保护不等于完整不可达区检测）。
- 官方 UniStorm 5.4+ Render Graph 支持前置条件与雷雨头显效果。
- Windows PC-VR 构建的上述旧插件 Shader 错误、116 条警告及实际运行验证仍待处理；不能因 Succeeded 字样跳过。

## 使用入口

详细操作顺序：`Assets/DeepSeaDemo/README_完整流程测试.md`。

- 菜单 13：输入诊断。
- 菜单 14：显式首次装配；已装配则不重新生成。
- 菜单 15：保存场景引用审计。
- 菜单 16：现有平台交互/检查点配置。
- 菜单 17：Windows PC-VR 构建，仅 `_1VR` 场景。

不要运行旧的 `01 Build Isolated Demo` 来配置本场景。
