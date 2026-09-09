# 本轮装配中断与崩溃记录

第一次装配在手套引用迁移处抛出托管异常，没有保存场景。用户确认期间没有手工修改，并允许重新载入已保存副本。

随后自动重载进入 `EditorSceneManager.OpenScene`，Unity 原生调用栈在 `GameObject::ActivateAwakeRecursivelyInternal / Deactivate / DestroyWorldObjects / RestoreSceneBackups` 崩溃。此崩溃确实发生在本轮装配恢复操作中，不能当作与本轮无关的随机问题。

已采取的措施：

- 停用自动重载恢复命令，禁止重复走这条路径。
- 引用映射排除 Unity 内部的 `m_GameObject`、`m_Script`、Prefab 所属信息；只映射用户脚本引用。此前通用遍历可能触碰对象所属引用，是需消除的风险，不声称已单凭堆栈证明唯一根因。
- 缺少的组件使用 `CopySerializedManagedFieldsOnly`，不复制 Unity 原生所属信息。
- 改为从已有 Prefab 在不激活状态下装配所需模块，不再同时加载另一整张水体场景。
- 不修改或重新生成原平台，交互物直接放进已存在的房间。
- 重启后的装配成功保存，随后第一轮完整 API Play Smoke 成功结束，没有再次发生该原生崩溃。

测试中途修改脚本会重载测试状态，并可能触发 XRI Socket 的临时协程初始化错误。后续应先完成编译再运行测试；被脚本重载打断的测试必须记录 INTERRUPTED，不作为通过证据。
