# 物理焦散与波浪控制说明

## 焦散计算

水下全屏 Pass 对接收点执行以下实时计算：

1. 从 Crest Animated Waves Texture Array 读取 FFT、Dynamic Waves 与其他波形输入合成后的 XYZ 位移。
2. 以位移后的 X/Z 切线重建真实水面法线，而不只读取高度曲线。
3. 用空气/水折射率执行 Snell 折射，并迭代反解能命中当前接收点的波面源位置。
4. 把源位置周围的折射光线直接与接收平面求交，对落点映射作有限差分，得到完整二维 Jacobian；水平 Chop、高度、法线和斜向太阳均自然包含在映射内。
5. 使用 `1 / abs(det(J))` 作为辐照度聚光增益，再应用 Fresnel 透射、表面朝向、深度衰减和数值奇点钳制。

因此焦散条纹的拓扑、移动、合并与断裂均来自实时 FFT 波和物体产生的 Dynamic Waves。Shader 中不再存在独立的正弦/Voronoi 焦散花纹。

全屏 Pass 根据实际 UV 覆盖选择最精细的 Crest Cascade，而不是假定接收点属于某个网格 LOD；采样半径还会按该 Cascade 的 texel 自动设下限，避免远处/VR 视图出现亚像素平坦信号。`High` 使用三步逆映射，`Balanced` 使用两步；移动 VR 设备优先使用 `Balanced`。

## 波浪控制层级

- `CrestURPWaveController`：面向策划和运行时 UI 的集中控制。
- `CrestURP_OceanSpectrum.asset`：14 个波长八度的精细能量、Chop 和传播速度。
- `CrestURP_DynamicWaves.asset`：交互涟漪的传播、阻尼、稳定性和水平位移。
- `CrestURP_Foam.asset`：白浪与岸边泡沫的生成/消散。
- `CrestURPScaledTimeProvider`：暂停、倍速和手动时间。

运行时脚本可以直接修改 `CrestURPWaveController` 的公开字段，然后调用 `ApplySettings(true)`。改变 14 段波谱后，控制器会让已有 FFT Generator 原位重新初始化，避免重复分配大量生成器。
