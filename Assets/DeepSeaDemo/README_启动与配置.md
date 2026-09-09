# 深海调查 PC-VR Demo：启动与配置

## 先看交付状态

这是正在集成的 Demo，不是已经通过 Quest 验收的成品。

- 已生成独立场景 `Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation.unity`，以及 `Prefabs` 下的完整装配体、玩家、双手、敌鱼、普通鱼群和物品预制体。
- 第一轮生成检查：1 个 XR Origin、4 份日志、物品 ID 不重复、缺失脚本 0、4 个检查点未检测到重叠障碍物。
- **最近的 UI 分层、Decal、剧情声音和测试脚本修订尚需重新导入并运行生成器。不要把早一轮生成报告当作最新代码的运行验收。**
- 尚未完成：官方 UniStorm Render Graph 天气集成、VR 手柄全流程、手套姿态视觉校准、潜艇内部净空核验、Windows 构建、Quest 双眼及 72Hz 性能实测。
- Unity 刷新曾被安全检查拦截，需要用户先保存当前场景修改，再继续编辑器导入和测试。
- 旧 AI 自动安装器曾在脚本刷新时重建并保存 `1-VR` 的测试 AI。为避免再次发生，旧 AI／天气／水玩法／Crest 安装器的自动入口已关闭；手动菜单保留。没有执行 Git 整场景回退。

## 1. 文件在哪里

| 目录／文件 | 内容 |
| --- | --- |
| `Scenes/DeepSeaInvestigation.unity` | 独立调查场景，不是原来的 `1-VR` |
| `Prefabs/CompleteDemoAssembly.prefab` | 带内部绑定关系的整套装配体 |
| `Prefabs/VRPlayer.prefab` | 玩家、指定皮手套、移动与输入、氧气表 |
| `Prefabs/LeatherGloveLeft.prefab`、`LeatherGloveRight.prefab` | 独立左右手模型及骨骼姿态驱动 |
| `Prefabs/EnemyFish.prefab`、`FishSchool1.prefab`、`FishSchool2.prefab` | 敌鱼与两种普通鱼群 |
| `Prefabs/flashlight.prefab`、`locktool.prefab`、`blackbox.prefab` 等 | 可抓取物品 |
| `Settings/DemoConfig.asset` | 移动、氧气、声纳、暴露、碰撞参数 |
| `Settings/EnemyConfig.asset` | 敌人视线、速度、听觉和搜索 |
| `Settings/LeatherGlove*.asset` | 每根手指骨骼的弯曲轴和角度 |
| `Settings/DemoPCVRPipeline.asset`、`DemoPCVRRenderer.asset` | Demo 专用水体与声纳渲染配置 |
| `Runtime` | 任务、检查点、UI、输入、舱盖及物品脚本 |
| `Editor` | 显式运行的生成器、验证器、API 流程测试 |
| `Reports` | 实际生成与验收记录，请留意生成时间 |

## 2. 安全启动顺序

1. 在 Unity 手动保存你当前想保留的修改。重要场景可以先另存备份；不要让自动工具替你决定是否丢弃。
2. 等 Unity 完成脚本编译；Console 出现红色编译错误时不要进入下一步。
3. 首次或需要更新生成结果时，运行 `Tools → Deep Sea Demo → 01 Build Isolated Demo`。
4. 生成器只创建 Demo 场景和 Demo 自有资产。它会添加尚未占用的 `DeepSeaWorld / DeepSeaGround / DeepSeaProp / DeepSeaUI / DeepSeaPlayer` 层，不覆盖现有层名。
5. 打开 `DeepSeaInvestigation.unity`。确认只有一套完整装配体；**不要再额外拖入 Player、Noise System 或旧声纳测试系统。**
6. 运行 `02 Validate Saved Demo`，查看 `Reports/Validation.txt`。
7. 连接 Quest 3 的 Link / Air Link，确认电脑上的 OpenXR 运行时和手柄可用，再进入 Play。主菜单使用射线指向按钮、Trigger 选择。

`01 Build` 是重新生成命令：会更新 Demo 自有生成场景、预制体和配置。手工调整关卡前，先复制场景或把变更写进生成器；不要在手工精修后无意重跑生成器。

## 3. 手柄操作

| 输入 | 操作 |
| --- | --- |
| 左摇杆 | 按头部水平朝向移动 |
| 右摇杆 X | 默认 45° 瞬转；设置中可切换平滑转向 |
| 右摇杆 Y | 水下上潜／下潜；水面向下拨才重新下潜 |
| Grip | 近距离抓取，或有遮挡检测的短距离抓取；默认距离上限 3m |
| 持手电时 Trigger | 开关手电；松手保留开关状态 |
| 持工具时 Trigger | 靠近有效维修目标，持续维修 |
| 水下空手 Trigger | 从当下手部位置发射声纳；每次按下只发一次 |
| 水上空手 Trigger | 指向文档、装备站、登乘点并交互，不发声纳 |
| QTE 时另一只空手 Grip | 圆环判定；该手的普通抓取暂时被屏蔽 |
| 左手菜单键 | 暂停／返回游戏 |

菜单和阅读界面优先，避免点按钮时误发声纳。持物手不发声纳，另一只空手仍可发声纳。维修时若另一只手还拿着手电，请先把手电放到门外架子上。

## 4. 完整流程怎么走

1. **平台**：阅读《例行航海记录》，检查橙色报警终端；拿起手电，再对潜水服确认站按 Trigger。其余三份日志可以重复阅读。
2. **下潜**：走到没有栏杆的下水侧。手电只能照近处；空手 Trigger 发声纳。没有升降输入时会缓慢下沉。上浮到水面后维持漂浮。
3. **海床枢纽**：沿管道和青色灯标走，维修笼中拿取橙色电子锁维修工具。同一工具兼容舱盖维修。
4. **可选排放口**：读取传感器和幸存者转写，水样需要用手搬回平台对应接收区。读取文档登记的是资料，不是隐藏背包。
5. **潜艇**：当前实际模型的 `Door` 是顶部舱盖。靠近 QTE 锁区，持续用工具 Trigger 维修，另一只空手 Grip 判定。失败回退进度，完美有短暂 1.5 倍增益。维修完成后退开，给舱盖留出安全开启空间。
6. **黑匣子**：舱内仍然进水。读取最后记录，拿走橙色 Cube 黑匣子，备用电源和引擎声触发一次。尽快返回平台。
7. **登乘与解析**：在水面接近返回登乘点，用 Trigger 登上平台。将黑匣子放进解析座并松手，等待播放结束。可选水样放在旁边接收区。
8. **结局**：缺少可选证据不妨碍主线结局。选择“保留证据”或确认“上传公司”。上传与删除仅是游戏剧情，不发送或删除工程文件。

15–20 分钟是目标节奏，目前没有经过首次玩家计时验证，也没有用强制等待延长流程。

## 5. 参数怎么改

选中 `Settings/DemoConfig.asset`：

- `Move Speed`：标准水平速度，默认 2m/s。
- `Swim Speed`：升降速度，默认 1.5m/s。
- `Oxygen Seconds`：满氧水下时长，默认 1200 秒。
- `Refill Seconds`：水上补满氧所需时间，默认 5 秒。
- `Sonar Radius / Speed / Width / Cooldown`：默认 25m、12m/s、0.5m、1.5 秒。
- `Exposure Per Pulse`：主动声纳每次增加 20；停发 8 秒后以每秒 3 衰减。
- `Impact Speed / Cooldown / Radius`：石头撞击最低 1.5m/s、冷却 0.6 秒、12m 半径。

部分字段是**生成器的初始配置**，不是对所有现有组件的实时广播。修改配置后，重新生成 Demo，或同步调整场景里相应组件。声纳输入冷却、氧气和暴露由运行时直接读取配置。

玩家 `QuestLeftStickLocomotion`：水下下沉加速度、最大下沉速度、水面眼睛高度、吸附速度、胶囊尺寸、瞬转角度。玩家根节点保持 `(1,1,1)`，不要把 XR Origin 拉伸缩放。

玩家 `SonarFogLantern`：头灯半径默认 1m，可改高度、边缘和前移距离。

手电 `GrabFlashlight`：范围、角度、亮度、颜色、光锥透明度和水雾穿透强度；默认不投实时阴影。

维修工具 `RepairSkillCheckController`：首次间隔、随机间隔、成功／完美弧度、失败回退比例、完美倍率和持续时间。维修目标 `RepairableFacility`：总维修秒数、工具 ID、损坏 Decal 列表。

环境音 `UnderwaterAmbienceController`：替换 `Underwater Loop`；表面音量、最低音量、达到最低音量的深度和淡变时长都可调。默认临时水流声；深水不会完全静音。

黑匣子 `BlackBoxItem`：正式录音槽位。没有录音时解析座使用临时提示音，剧情内容由文字界面呈现。

## 6. 添加可抓取物

建议复制最接近的 Demo 物品预制体，而不是从空物体重复搭建。

1. 功能根节点保留 `Rigidbody`、Collider、`DemoGrabInteractable`、`DemoProp`。
2. 水体组件保留 `WaterMembership`、`WaterInteractable`、`WaterBuoyancy`、`WaterSplash`、`BuoyantXRGrabBridge`。
3. 只替换 `Visual` 下模型，根节点不缩放。
4. `DemoProp.id` 必须唯一。关键物品的 `Recovery Point` 指向场景里的安全放置点。独立预制体里该引用故意留空，避免引用测试场景。
5. 手电 `Released Force Scale = 1` 上浮；黑匣子、工具、石头、水样、卡片设为 `0` 下沉。抓住时 `Held Force Scale = 0`。
6. 碰撞声纳只给需要发声的投掷物添加。接收目标需要 `SonarCollisionGroup`，地面用 `DeepSeaGround`，不要给手或水体辅助碰撞体加响应组。

## 7. 增加 AI 与巡逻点

1. 拖入 `EnemyFish.prefab`，位置放在已烘焙的海床导航面附近。
2. 保留 `NavMeshAgent` 和 `DeepSeaStalkerController`；模型只放在 Visual 下。
3. 创建数个空节点作为巡逻点，放在海床导航面上，不放在水面、岩石内部或艇壳内。
4. 给控制器指定 `EnemyConfig.asset`、巡逻点数组、玩家和玩家复活控制器。正式装配体通过 `DemoSceneBindings` 绑定。
5. 巡逻随机洗牌；调查速度是巡逻的 1.5 倍。开启真实障碍层视线判断，不能把水体辅助 Collider 当墙。
6. 增加第二条敌鱼时，需要分别配置其控制器。现有 `DemoAcoustics` 的运行时压力配置只自动绑定装配体里的主敌鱼；不要假定新增敌人自动共享全部暴露调节。

建议后续增加声音类型权重、多敌人互相通报、最后已见位置搜索、更清楚的声学暴露音效提示。当前依旧是海床导航，不是完整三维鱼类追击。

## 8. 保存、重试与物品找回

检查点：平台出发、获得维修工具、舱盖打开、返回解析室。存档只写入 `Application.persistentDataPath/DeepSeaInvestigation.checkpoint.json`，替换时保留 `.bak`。

手中物品在检查点快照中记录到指定找回点，避免复活时残留选择状态。重试会清理声纳、QTE、暴露和敌人追击，并补充氧气。

当前物品找回检测实现了关卡边界兜底；**复杂的“物品卡在不可达内腔”检测仍待补完**。测试中遇到这种情况，暂停并重试检查点，不要复制一个同 ID 的物品。

## 9. 天气和渲染

当前项目检测到的旧 UniStorm 不能视作已经符合计划要求。需要导入已购 **UniStorm 5.4+ 及官方 URP Render Graph 支持包**，随后核验包版本、Feature 和双眼渲染。缺包时天气根节点关闭，保留接入位置；不会换成另一套天气或自动购买资源。

最新生成器为 Demo 配置两个 Renderer：主 Renderer 负责水体、声纳描边和水痕；UI Overlay Renderer 不带水下雾。UI 是 World Space，不是头显不兼容的 Screen Space Overlay。

正式天气接入后仍需验证室内／水下雨粒子抑制、阴天到雷雨过渡和闪电舒适性；不能只勾 `Weather Ready` 就视为验收完成。

## 10. 验证与构建

- `02 Validate Saved Demo`：静态引用、数量、材质、检查点障碍物报告。
- `04 Run API Play Smoke`：脚本调用流程接口，测试任务连接和结局，不模拟玩家手柄技能。不会写正式 Demo 存档。
- `03 Build Windows PCVR`：只构建 Demo 场景，输出到 `Builds/DeepSeaDemoPCVR/DeepSeaInvestigation.exe`。当前尚未成功执行这一步。

必须人工完成：用 Quest 手柄从开局到两种结局；检查左右眼、手套握持、舱盖上下潜、贴墙／蹲下／转向、换手、物品遗失、各章节死亡恢复。72Hz 对应约 13.89ms 帧预算，需要记录实际 CPU/GPU 时间；编辑器编译成功不代表达到这个目标。

字体采用 Noto Sans CJK SC，许可在 `Fonts/OFL.txt`。原第三方模型、音乐和天气仍受各自资源许可约束，不随本教程变成可公开分发的免费资源。
