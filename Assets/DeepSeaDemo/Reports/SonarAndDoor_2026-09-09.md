# 声纳与装备房门口修复记录

目标场景：`Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity`。

## 本轮修改

- 声纳命中一个组合碰撞体后，逐个检查子 Renderer 的球壳相交范围，不再直接显露整个建筑层级。
- 排除 Water 插件自身材质、非网格 Renderer、地面及被忽略层。水下声波不显露水面以上的建筑；跨水面的支柱按水面高度裁切描边。
- 缓存碰撞体对应层级与 Renderer 列表，合并同一波更新中的重复层级遍历，减少重复查找及材质数组分配。
- 描边 RenderGraph 不再把 resolved cameraDepthTexture 当成深度附件；改用采样深度判断遮挡，避免与颜色附件的格式／MSAA 不一致。描边宽度使用世界单位。
- 新增编辑器声纳错误与帧间隔日志，便于验证剩余闪烁问题；不等同 GPU 性能测试。
- 在当前打开的场景中，仅调整装备房 South_Wall_00、South_Wall_02 的碰撞。保留模型，以左右门框和顶部横梁代替整块挡门碰撞，不提高全局跨台阶高度。

## 已完成检查

- Editor C# 编译检查通过，仍有已有的未赋值字段警告。
- 球壳到达前、相交、离开后、包含原点四组边界测试通过。
- 描边 Shader 请求编译 mono、STEREO_INSTANCING_ON、INSTANCING_ON + STEREO_INSTANCING_ON 变体，无报告的编译错误。
- 两个门洞各 11 个位置的胶囊碰撞采样通过。碰撞开口宽约 1.2m、高约 2.3m，视觉门槛未移除。

## 尚需实际运行确认

- 当前场景已有用户未保存修改，因此没有自动保存或重新加载场景。门口改动留在当前场景中，并支持 Undo；检查后请手动保存。
- 未完成最新修改后的 Play 全流程、Quest 双眼画面或 GPU 帧时间测试；不能声称闪烁、卡顿或实机卡门已全部消除。
- 建议先在设备关闭手电时只发一道声纳，观察水面、平台与海床，然后再检查多波重叠。强烈闪烁时退出 Play，不反复触发。
- 运行后查看 `Logs/DeepSeaDemo/Sonar-Render.txt`；门口和 Shader 检查分别见 `Doorway-Repair.txt` 与 `Sonar-Render-Regression.txt`。

完整模拟器操作和剧情路线见 `Assets/DeepSeaDemo/模拟器游玩手册.md`。
