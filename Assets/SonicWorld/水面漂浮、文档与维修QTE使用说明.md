# 水面漂浮、文档、维修 QTE 与 AI 使用说明

## 一次安装

打开目标场景后，执行 **Tools > VR Water Gameplay > Install Complete Water Gameplay**。安装器会：

- 给 XR Origin 添加水面状态、漂浮移动、文档阅读、声纳和环境音组件；
- 生成 `Water Gameplay Props`（手电筒、黑匣子、播放座、电子卡）；
- 生成 `Surface Documents` 三份临时任务文档；
- 生成 `Deep Sea Repair Demo`，里面有维修工具和 `QTE Repair Console`。

把 `Water Gameplay Props`、`Surface Documents` 和播放座移动到你的水上平台。彩色盒子都是占位物，直接替换它们的可见 Mesh 即可；根节点的 Rigidbody、Collider 和脚本请保留。

## 玩家状态与按键

| 状态 | 左摇杆 | 右摇杆 | Trigger | 桌面键盘 |
|---|---|---|---|---|
| 水下 | 水平移动 | X 转向，Y 上潜/下潜 | 空手发声纳；持物交给工具 | Q 下潜；F 维修；R QTE |
| 水面漂浮 | 水平移动 | X 转向；向下退出漂浮并下潜 | 空手阅读文档，不发声纳 | L 阅读；Q 下潜 |

当头显升至水面附近，`Quest Left Stick Locomotion` 会进入 **Is Surface Floating**：垂直速度归零，并将头显平滑贴到水面上方。此时仍可移动、抓取、开灯、维修和操作播放座，不会再自动沉入水底。

调整 XR Origin 上的 **Quest Left Stick Locomotion**：

- `Surface Eye Height`：头部相对水面的高度。
- `Surface Snap Speed`：贴近水面的速度。
- `Surface Capture Distance`：多远开始进入漂浮。
- `Dive Input Threshold` 与 `Desktop Dive Key`：离开漂浮、重新下潜的输入。
- `Underwater Sink Acceleration` / `Maximum Sink Speed`：水下松开升降输入后的下沉感。

## 水面文档

每个文档物体上有 **Surface Document**，可直接在 Inspector 填：

- `Document Title`：标题。
- `Document Body`：正文。
- `Hide World Object When Read`：读完是否隐藏场景里的这份纸。

水面、空手、靠近文档后按 **L** 或该空手 **Trigger** 打开；同一输入再次关闭。阅读卡会跟着 HMD。范围与 Layer Mask 位于 XR Origin 的 **Surface Document Reader**。

## 维修与 QTE

1. 抓住 `Repair Tool`，靠近 `QTE Repair Console`，按住工具手 Trigger 维修。
2. 红色 Decal 会随进度渐隐，模型和材质不会被替换。
3. 随机出现圆环后，桌面按 **R**；VR 用**另一只没有拿工具的手**按 Grip。
4. 绿色成功区：正常继续；白色完美区：默认 2 秒 1.5 倍速度；按错或超时：默认回退总进度的 10%。

维修物上的 **Repairable Facility** 管理目标工具 ID、总维修时长和 Decal。工具上的 **Repair Skill Check Controller** 可调整出现间隔、时限、成功/完美区宽度、回退比例、完美倍率和持续时间。

桌面测试中，**F** 是持续维修，**R** 只用于 QTE 判定，避免两个功能抢同一个按键。

## 物品浮沉

- 手电筒：松手后保留浮力并缓慢浮到水面。
- 黑匣子、电子卡、维修工具：松手后 `Released Force Scale = 0`，只保留刚体重力，因此下沉。

这个选项在物品根节点的 **Buoyant XR Grab Bridge** 上。`Held Force Scale` 决定抓住时浮力强度，通常保持 0，避免 XRI 速度追踪与水体力互相拉扯。

## AI：添加、巡逻与提示

敌人需要：`NavMeshAgent`、`CapsuleCollider`、`DeepSeaStalkerController` 和 `DeepSeaStalkerConfig`。在控制器的 `Patrol Points` 数组放入已烘焙 NavMesh 上的空物体。

- 每轮会把全部巡逻点随机洗牌；不会连续走同一个点。
- 听到声纳后，调查/搜索速度 = `Patrol Speed × Investigate Speed Multiplier`，默认 1.5。
- 调查、搜索、归队时头顶显示黄橙色 `!` 脉冲；追踪玩家时变为更快的红色脉冲。

提示组件会自动添加为 **Deep Sea Stalker Alert Indicator**。若要隐藏，取消其 `Show Indicator`；高度、颜色和脉冲频率都能修改。

后续建议：为真实障碍配置 `Sight Blockers`；为不同声音设权重；增加最后目击位置搜查、多个敌人的声音优先级与协作行为。

## 环境白噪

XR Origin 的 **Underwater Ambience Controller** 现在使用：

- `Surface Volume`：水面/水上最大声；
- `Deep Water Minimum Volume`：深水最低声，永不静音；
- `Depth For Minimum Volume`：达到最低声量的深度。

可把正式循环声拖入 `Underwater Loop`；为空时会生成临时低频水流声用于测试。
