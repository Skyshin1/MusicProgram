# 音效接口与占位声音

在 Unity Project 中选中 `Assets/DeepSeaDemo/Resources/DeepSeaAudio.asset`，展开 Cues，替换相应 Clip 即可。每项有音量、3D 混合、最近/最远距离；Master Volume 控制总音量，Output 可接 Audio Mixer Group。将某项 Volume 设为 0 可静音。

| Sound | 触发位置 |
| --- | --- |
| RepairLoop | 持续维修时循环，松开、离开检测区或进入 QTE 时停止 |
| QteStart | 维修圆环出现 |
| RepairSuccess | QTE 成功或完美判定 |
| RepairFailure | QTE 失败或超时 |
| RepairComplete | 设施实际修复完成；读取存档不触发 |
| HatchOpen | 舱门开始转动，只播放一次 |
| FishSwim | 普通鱼低音量游动循环 |
| FishFlee | 普通鱼受到声纳惊吓，开始逃游 |
| EnemyChase | 敌人开始追逐，连续咬击之间不重复触发 |
| EnemyBite | 敌人实际咬中玩家 |

占位 WAV 在 `Assets/DeepSeaDemo/Audio/Placeholders`。它们是项目内程序合成的单声道音效，可自由替换；不含外部录音。生成代码为 `Assets/DeepSeaDemo/Editor/DemoAudioSetup.cs`，菜单 Tools > Deep Sea Demo > Audio > Create Missing Placeholder Sounds 只补缺少的文件，不覆盖已有音效和配置。

普通鱼原有的 Swim Loop Clip / Flee Clip 插槽仍优先于公共配置。其余物体需要单独配置时，可添加 DemoAudioEmitter 并指定本地 Bank；没有本地 Bank 时使用公共配置。脚本接口：DemoAudioEmitter.Play(component, DemoSound.事件名)。

音效是听觉反馈，不会额外向 AI 的 NoiseSystem 发出新的声纳刺激。暂停会暂停播放，维修循环随维修状态停止。

水面透视度由 DemoConfig 的 Water Surface Absorption Scale 调节：默认 3，值越大，从水上越难看清深处；不会改变水下视角的深度衰减或声纳参数。
