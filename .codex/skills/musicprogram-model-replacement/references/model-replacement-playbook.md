# MusicProgram 模型替换流程与验收清单

## 投放与映射

用户把原始模型、贴图、动画放到 `Assets/Art/IncomingModels/`。推荐每个资产使用独立文件夹，例如：

```text
Assets/Art/IncomingModels/
  Flashlight/
    Flashlight.fbx
    Textures/
  BlackBox/
    BlackBox.fbx
  FishA/
    FishA.fbx
  模型替换清单.json
```

优先读取 `模型替换清单.json`。每一项必须写出新资源、目标对象与替换范围：

```json
{
  "scene": "Assets/Scenes/1-VR.unity",
  "items": [
    {
      "source": "Assets/Art/IncomingModels/Flashlight/Flashlight.fbx",
      "target": "Flashlight",
      "kind": "grabbable-tool",
      "operation": "replace-visual-only",
      "notes": "保留 GrabFlashlight、XR Grab、Rigidbody、Collider 与浮力。"
    }
  ]
}
```

`target` 可以是当前场景的对象路径、明确的 Prefab 路径，或已知玩法对象名。若名称重复，必须询问或列出候选，不能任意选择。

## 导入前检查

1. 确认模型格式、网格、材质、贴图和动画 Clip 均已由 Unity 正常导入，无 Console error。
2. 记录模型的比例、正面朝向与 Pivot。Unity 默认角色/道具通常以 `Y` 为上、`Z` 为前；不一致时只在视觉子节点上修正旋转和缩放。
3. 对 URP/Quest 目标，优先使用 URP/Lit、URP/Unlit 或项目确认可用的 Shader。出现粉色表示 Shader 不兼容，应先修正材质，不能当作模型替换完成。
4. 不把用户导入的 FBX 直接改成带玩法组件的唯一来源。优先在场景/玩法 Prefab 下实例化为视觉子节点，保留原始导入资产。

## 推荐替换步骤

1. 打开清单指定场景或 Prefab；检查目标根节点、它的子层级和已有组件。
2. 确认该目标是否已有 `Visual`、`Model`、`Body Mesh` 或等效纯视觉子节点。
3. 把新模型放在该视觉节点下，归零局部位置与旋转后再按实际模型微调。保留旧视觉节点但先禁用，直到测试完成。
4. 用已有 Collider 先测试；仅在明显不包住新模型、抓取射线/手部接触失败、AI 碰撞不正确时，调整 Collider。Collider 应挂在玩法根节点或明确的碰撞子节点，不能误挂到只做装饰的网格上。
5. 新模型带可用动画时，再连接 Animator。检查现有脚本所需参数；当前 DeepSeaFishAI / DeepSeaStalkerController 依赖 Animator 时必须保证引用仍指向有效 Animator。
6. Play Mode 验证，再决定是否删除旧视觉。若旧视觉是用户资产或不确定是否被引用，保持禁用并在交付说明中记录。

## 各类对象的保留组件

| 类型 | 常见关键组件/脚本 | 必测行为 |
|---|---|---|
| 手电筒 | `GrabFlashlight`、`XRGrabInteractable`、Rigidbody、Collider、`BuoyantXRGrabBridge`、Light | 抓取、Trigger 开关、松手后上浮、光束可见 |
| 黑匣子 | `BlackBoxItem`、`XRGrabInteractable`、Rigidbody、Collider | 抓取、下沉、插入播放座、播放事件 |
| 维修工具 | `RepairTool`、`XRGrabInteractable`、Rigidbody、Collider | 抓取、Trigger 维修、QTE 不失效、松手下沉 |
| 电子卡/文档 | `XRGrabInteractable`、Rigidbody、Collider、文档脚本（如有） | 抓取、下沉/阅读逻辑仍可用 |
| 鱼类 | `DeepSeaFishAI`、Animator、AudioSource、Collider | 游泳、声纳惊逃、动画与声音 |
| 敌人 | `DeepSeaStalkerController`、NavMeshAgent、Animator、AudioSource | 随机巡逻、调查声源、警戒提示、攻击/搜寻动画 |
| 维修设备 | `RepairableFacility`、Decal / DecalProjector、Collider | 接近后可维修、QTE、Decal 随修复淡出 |

## 交付验收

每个替换项都要完成以下检查：

- Scene 与 Prefab 均无 Missing Script/Missing Material；Console 无新增 Error。
- 新模型比例、方向和 Pivot 合理；不会遮挡相机、穿进地面或与抓取点明显错位。
- 抓取物在双手、射线/近场交互及松手后水体行为均正常。
- AI 角色能在 NavMesh 上正常移动，动画不报参数错误。
- 水下画面没有粉色材质、左右眼不一致或异常透明。
- 用户可直接在 Inspector 的视觉子节点继续调缩放、局部位置和局部旋转。

## 给 Codex 的交接提示

用户可以直接发送以下句子：

> 使用 `$musicprogram-model-replacement`。读取 `Assets/Art/IncomingModels/模型替换清单.json`，只替换清单目标的视觉模型，保留所有现有玩法组件和引用。完成后在指定场景 Play Mode 验证每项功能并报告未解决的材质或动画问题。
