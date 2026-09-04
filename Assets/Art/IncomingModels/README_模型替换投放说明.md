# 模型替换投放区

把待替换的 FBX、Prefab、GLB、贴图和动画放在此目录。建议每个物品一个子文件夹，并填写同目录的 `模型替换清单.template.json` 后改名为 `模型替换清单.json`。

然后把下面这句话发送给另一个 Codex：

> 使用 `$musicprogram-model-replacement`，读取 `Assets/Art/IncomingModels/模型替换清单.json`，按清单替换视觉模型；保留原有玩法根节点、XR、Collider、Rigidbody、Grab、浮力、脚本、AI 与音频引用。

不要直接删除场景中的旧玩法对象；模型替换应优先发生在其 `Visual` / `Model` 等视觉子节点中。
