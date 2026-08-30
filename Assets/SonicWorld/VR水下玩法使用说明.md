# VR 水下玩法快速使用说明

> 水面漂浮、文档、维修 QTE、物品浮沉和 AI 警戒的最新说明见：[水面漂浮、文档与维修QTE使用说明](水面漂浮、文档与维修QTE使用说明.md)。

## 第一次安装

1. 等 Unity 编译完成。
2. 打开要使用的场景。
3. 点击 **Tools > VR Water Gameplay > Install Complete Water Gameplay**。
4. 场景会生成 **Water Gameplay Props**，把它移动到水上平台。
5. 在 Quest 3 运行前确认相机使用 Mobile_Renderer 或 PC_Renderer。

安装器会在 Assets/SonicWorld/Prefab/WaterGameplay 创建手电筒、黑匣子和播放座预制体。

## 玩家操作

| 操作 | 功能 |
|---|---|
| 左摇杆 | 按头部水平方向移动 |
| 右摇杆左右 | 转向 |
| 右摇杆上下 | 上潜 / 下潜 |
| 空手 Trigger | 从按键的那只手发出声纳 |
| Grip | 抓取物品 |
| 持有手电筒时 Trigger | 开关手电筒 |
| 持有维修工具时 Trigger | 持续修复 |

只有正在持物的那只手会屏蔽声纳。另一只空手仍可以按 Trigger 发声纳。

## 调整玩家移动和下沉

选择 XR Origin 上的 **Quest Left Stick Locomotion**：

- Move Speed：水平速度，默认 2。
- Vertical Swim Speed：上潜/下潜速度，默认 1.5。
- Underwater Sink Acceleration：松开摇杆后开始下沉的快慢，默认 0.8。
- Maximum Sink Speed：最终最大下沉速度，默认 0.6。
- Air Gravity：出水后的重力，默认 -9.81。
- Use Snap Turn：勾选为瞬间转向，取消为平滑转向。

玩家使用 CharacterController，不要再给 XR Origin 添加动态 Rigidbody。

## 调整水面判断

XR Origin 上的 **Water Surface State Tracker**：

- Enter Depth：头部深入水面多少后判定为水下。
- Exit Height：头部高出水面多少后判定为出水。

两项默认都是 0.1m，用于防止波浪导致状态快速闪烁。

## 头灯

选择 XR Origin 上的 **Sonar Fog Lantern**：

- Radius：头灯清晰区域半径，默认 1m。
- Forward Offset：区域向玩家前方移动的距离。
- Height / Bottom Offset：圆柱灯区高度。
- Edge Fade Width：边缘渐变宽度。
- Visibility Strength：清除 Water Volume 水雾的强度。

## 手电筒

选择手电筒预制体上的 **Grab Flashlight**：

- Range：照射与水雾穿透距离。
- Spot Angle：光锥角度。
- Intensity：Spot Light 强度。
- Beam Opacity：可见光束 Mesh 的透明度。
- Water Fog Clear Strength：光锥内减少水雾/深度变暗的程度。
- Cast Shadows：Quest 默认关闭，打开会明显增加消耗。
- Turn Off On Drop：勾选后松手自动熄灭；默认不勾选，可把亮灯放在场景里。

## 给新物品添加浮力

1. 选中一个或多个可抓取物体。
2. 点击 **Tools > VR Water Gameplay > Configure Selected Props For Water Buoyancy**。
3. 根节点应有 Rigidbody、Collider、XR Grab Interactable、Water Buoyancy、Water Splash 和 Buoyant XR Grab Bridge。
4. 可见 Mesh 节点应有 Water Interactable 和 Water Membership。

Held Force Scale 为 0 时抓住后完全暂停浮力；设为 0.1 到 0.3 会保留轻微水阻。松手后自动恢复完整浮力。

维修工具保持 Decal 渐隐修复逻辑，不会移动或切换模型。

## 黑匣子和播放座

1. 把黑匣子带到水上平台。
2. 放入 Black Box Playback Dock 的 Socket。
3. 插入后自动播放；拔出会停止；再次插入可以重播。
4. 首次完整播放结束只触发一次 On First Task Completed。

正式录音拖到 Black Box Item 的 Recording。没有录音时，播放座会使用临时提示音。

## 水下环境音

XR Origin 上的 **Underwater Ambience Controller**：

- Underwater Loop：拖入正式海流循环 AudioClip。
- Maximum Volume：最大音量。
- Fade In / Fade Out Seconds：进出水淡入淡出。
- Flow Volume Influence 和 Pitch Range：水流速度对声音的影响。

未指定 AudioClip 时会生成临时低频水流声；指定正式素材后自动改用正式素材。

## 出水屏幕水痕

安装器会把 Water Exit Lens Droplets Renderer Feature 加到 Mobile 和 PC Renderer。

XR Origin 上的 **Water Exit Lens Effect** 可调整 Duration、Edge Width、Droplet Density、Fall Speed 和 Distortion。效果仅在玩家头部从水下穿出水面时执行，平时不会增加全屏 Pass。

## 声纳与投掷物

- 玩家声纳由 XR Hand Sonar Input 控制，可修改强度与冷却。
- 声纳发射位置取按下 Trigger 的实时手部位置；手部追踪失效才回退到 Main Camera。
- 物体撞击声纳继续使用 Volumetric Fog Collision Pulse、碰撞 Layer、地面 Tag、速度阈值和碰撞组。
- F 键只作为编辑器调试入口，默认关闭；需要时勾选声纳发射器的 Enable Keyboard Test。

## Renderer Feature 顺序

1. WebGPU Water Underwater Fog：Before Rendering Post Processing。
2. Sonar White Outlines：After Rendering Post Processing。
3. Water Exit Lens Droplets：After Rendering Post Processing，并由安装器放在描边之后。

如果更换了新的 URP Renderer，请再次运行完整安装器。
