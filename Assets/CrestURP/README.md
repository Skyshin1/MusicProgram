# Crest Water 4 URP / OpenXR 移植

本目录是在 **Crest Ocean System 4.23.0**（MIT）模拟核心上实现的 URP 渲染层，目标环境为本项目的 Unity 6000.3.7f1、URP 17.3.0 和 OpenXR。

上游源码：<https://github.com/wave-harmonic/crest>  
本次固定源码提交：`db0658ff0b2e93e4a9e28cc2867509658b0ecc00`  
上游许可证：`Assets/ThirdParty/CrestWater4/LICENSE`

## 已完成的效果

- Crest 多级 LOD 海洋网格与 FFT 波浪（示例：6 级 LOD、256 LOD 数据分辨率、128 FFT）。
- Crest 泡沫模拟、GPU 高度数据和 Rigidbody 浮力示例。
- Crest Dynamic Waves：玩家、五个漂浮物、近水面潜艇和鱼群均使用 `SphereWaterInteraction` 实时向模拟注入速度与位移，可产生尾流、涟漪和物体间叠加扰动。
- URP 水面 Shader：波浪顶点位移、细节法线、环境反射、带波面法线扰动和粗糙度 Mip 的 URP 平面反射、主光高光、软阴影、相机不透明纹理折射、深度吸收、浪尖与浅水泡沫。
- 动态吃水线：全屏 Pass 逐像素采样 Crest 波浪 LOD，并迭代求取视线与动态水面的交点；相机可在水上、水下和半浸没之间连续切换。
- 水下体积：Beer-Lambert RGB 吸收、散射、能见度、扰动、近水面弯月线和前向散射光束。
- 物理焦散：不使用焦散贴图或程序花纹。逐像素读取 Crest FFT + Dynamic Waves 的完整 XYZ 位移，以实际波面切线求法线；按空气到海水的 Snell 定律迭代反解源波面，再把邻域折射光线直接投射至接收平面，以落点映射 Jacobian 行列式倒数计算聚光增益。包含 UV 覆盖驱动的 Cascade 选择、自适应 texel 采样、Fresnel 透射、斜面接收、深度衰减、焦点奇异值钳制以及 Balanced/High 两档质量。
- 集中式海况控制：风速、风向、湍流、浪高、水平尖锐度、方向扩散、传播速度、FFT 分辨率、暂停/时间缩放、Dynamic Waves 和泡沫均可在一个组件中调整；另有 14 个波长频段的能量、Chop 和速度独立控制。
- OpenXR：完整 `XROrigin -> Camera Offset -> Main Camera` 层级、HMD 位置/旋转 Input Action、双眼安全的 `TEXTURE2D_X` 和 stereo 宏。
- PC Renderer 已安装 `Crest URP Underwater + Waterline` Full Screen Renderer Feature。它在透明物体之前处理水下实体，再由透明海面叠加反射和折射。

## 示例场景

打开：`Assets/CrestURP/Samples/CrestURPShowcase.unity`

场景包含水线上下参照物、海床、岩石、珊瑚群、拱门、实时反射探针、URP 平面反射、五个浮力物体、动态波浪潜艇/鱼群、后处理和 XR 相机。该场景已加入 Build Settings，但默认关闭，不会改变现有构建内容。

非 VR 播放时：

- `W/A/S/D`：前后左右
- `Q/E`：下潜/上浮
- 按住鼠标右键：观察
- `Shift`：加速

VR 播放时由 OpenXR 头显驱动相机。移动 `XR Origin - Waterline Test` 根节点可改变整个玩家空间的水深。

## 调参入口

选择 `Crest Ocean 4 - URP Port`，在 `CrestURPWaterController` 中调整光学效果：

- 水色、RGB 吸收、散射颜色、能见度
- 物理焦散开关与 Balanced/High 质量档
- 水折射率、波面采样半径、自适应 texel 半径、反向投影量、焦点响应、最大聚光增益和 Jacobian 最小值
- `Caustic Gain / Wave Slope / Water Depth / Displacement Height` 四种诊断视图
- 吃水线宽度、作用距离、亮度
- 水下光束、各向异性和扰动
- 水面光滑度、细节法线、折射、泡沫阈值与浅水泡沫深度

同一对象上的 `CrestURPWaveController` 是主要波浪调参入口：

- `Glassy / Tropical Lagoon / Open Ocean / Storm` 可编辑预设
- 海平面、风速、风向、湍流、总浪高、Chop、方向扩散、波速和 FFT 64/128/256
- 整套模拟暂停、时间缩放和手动时间，便于镜头、网络同步及效果调试
- Dynamic Waves 的频率、阻尼、Courant 数、浅水衰减、水平位移、钳制和重力
- 白沫消散、白浪强度/覆盖率、岸边泡沫及模拟频率
- 展开 `Detailed 14-Band Spectrum` 可对 0.0625 m 到 1024 m 的每个波长八度分别调整能量、Chop 和传播速度；运行时改值会让现有 FFT Generator 原位刷新

波谱、Dynamic Waves、Animated Waves 和泡沫参数分别保存为 `CrestURP_OceanSpectrum.asset`、`CrestURP_DynamicWaves.asset`、`CrestURP_AnimatedWaves.asset` 和 `CrestURP_Foam.asset`，重建示例时不会覆盖已有资产调参。任意游戏物体可添加 `CrestURPDynamicWaveEmitter`；细长载具建议沿船体放置多个发射器。主相机上的 `CrestURPPlanarReflection` 可调整分辨率、更新间隔、波法线扰动、粗糙度 Mip、裁剪、反射层和阴影。

## 编辑器工具

菜单 `Tools/Crest URP`：

- `Rebuild Complete Showcase Scene`：按当前移植版本重建完整示例。
- `Validate Shaders And Capture Preview`：强制重新导入两支 Shader，检查错误、场景依赖、Renderer Feature 和 XR 层级，并生成验证报告/预览。
- `Run 90-Frame Play Mode Visual Test`：实际运行 90 帧后截图，同时统计 Error、Exception、Assert，并自动检查位移、波面斜率与 Jacobian 增益调试图是否存在足够的空间变化；纯色假通过会直接标记失败。

正式验证结果位于 `Assets/CrestURP/Samples/CrestURPValidationReport.txt`。

## VR 性能建议

当前配置面向 PCVR。移动一体机建议从以下配置开始：

- LOD Data Resolution：`128`
- LOD Count：`5`
- FFT Resolution：`64`
- 关闭实时 Reflection Probe 或改成 Baked
- 平面反射改为每 2 帧更新、分辨率比例 `0.35`，或针对反射层排除小物件
- 物理焦散改为 `Balanced`；关闭焦散会跳过全部波面差分采样
- 若 GPU 仍超预算，先关闭 Crest Foam Sim，再降低水下光束

这里的“物理焦散”是适合实时 VR 的折射光线微分/Jacobian 解，不是离线 Photon Mapping 或 Path Tracing。它的形状和运动完全来自实际模拟波面，但太阳被建模为方向光，未模拟有限太阳圆盘的多光线软化。

原始 Built-in Shader 保留在 `Assets/ThirdParty/CrestWater4/Shaders` 供许可证和算法对照；示例实际使用 `Crest/URP/Ocean` 与 `Crest/URP/Underwater`，不要把原始 Built-in 海洋材质重新赋给 URP 示例。
