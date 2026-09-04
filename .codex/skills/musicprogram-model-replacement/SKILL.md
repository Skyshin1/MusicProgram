---
name: musicprogram-model-replacement
description: Safely replace visual models in the MusicProgram deep-sea VR project while preserving XR interaction, water gameplay, sonar, AI, animation, and scene references. Use when incoming FBX, prefab, GLB, or texture assets must replace existing gameplay placeholders.
---

# MusicProgram 模型替换

将新模型接入此项目时，优先保留已有玩法对象与其组件，只替换其视觉子物体。这个项目的交互、浮力、声纳、维修、AI 等功能绑定在旧根节点上；直接删除或替换根节点通常会导致引用丢失。

## 先读取输入

1. 检查 `Assets/Art/IncomingModels/`。如其中有 `模型替换清单.json`，以它为准；否则读取同目录的投放说明并根据文件名、用户说明建立映射。
2. 读取 [替换流程与验收清单](references/model-replacement-playbook.md)，再检查目标对象所在场景或 Prefab 的真实组件，不能只凭名称猜测。
3. 映射、目标场景或“替换/追加”意图不明确时，只列出待确认项，不要猜测覆盖关系。

## 必须保持的边界

- 不删除或整体替换已存在的玩法根节点、XR Origin、Water Volume、Renderer Feature、输入动作、NavMesh 与全局管理器。
- 不移除目标根节点上的脚本、Collider、Rigidbody、XRGrabInteractable、AudioSource、Animator、Decal、浮力或声纳组件。默认让这些组件继续留在根节点上。
- 优先创建或复用名为 `Visual` / `Model` 的子节点，把新网格或模型 Prefab 放到其中；先禁用或移走旧视觉子节点，确认可运行后才清理旧视觉。
- 不能因为更换模型而私自改玩法数值、AI 行为、声纳范围、碰撞层、URP Renderer 或水体质量。确实必须变更时，明确说明原因并只改必要项。
- 维持可回退性：使用 Unity Undo / Prefab override，避免批量破坏性重建；不要覆写用户已有的模型或材质源文件。

## 分类处理

- **可抓取物**（手电筒、黑匣子、电子卡、维修工具、文档）：视觉替换后保留现有 Collider、Rigidbody、XR Grab、浮力桥接和功能脚本；根据新模型尺寸人工检查抓取距离、重心与 Collider 是否仍匹配。
- **鱼类与敌人**：先保留 AI 根节点与 NavMeshAgent。模型带 Animator 时检查 Animator 参数是否能支持现有控制器；没有可用动画则保持现有 Animator/程序移动，不能让新模型破坏 AI。
- **维修目标与 Decal**：保留 RepairableFacility 与 Decal。新模型要提供合适的可投射表面；不要用材质切换替代维修进度。
- **玩家/手套上的视觉**：不改 XR 相机、手部跟踪层级或输入，只在指定的手部或装备挂点替换视觉模型。

## 完成标准

逐一验证替换目标的视觉、缩放、Pivot、碰撞、抓取/动画/AI 功能和水下 URP 材质兼容性。完成后向用户报告：已替换的映射、保留的关键组件、需要用户确认的材质/动画问题，以及在哪个场景或 Prefab 中可继续调整。

对于目标清单格式、导入要求和逐类验收，读取 [替换流程与验收清单](references/model-replacement-playbook.md)。
