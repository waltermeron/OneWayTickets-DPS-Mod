# v1.1.1 / 游戏 v0.8.0.3 热修

本版基于 Steam Build 26195956 的官方程序集重新生成 Mod。v1.1 及此前发布包中的 World.dll、Commons.dll 来自 v0.8.0.2，不能覆盖到新版游戏资源上使用。

故障现场中 World.dll、Commons.dll 的 SHA-256 仍与 v1.1 发布包相同，而游戏资源及基础程序集已更新。日志先出现 UiCaravanCamera 组件缺失、CaravanHoveringInfo.Init 空引用，随后 Widget_Blower_Context.tick 持续异常，场景初始化未能完成。热修保留 v0.8.0.3 的完整类型、字段、方法及原游戏调用，再挂接伤害和历史观察。

热修后已实机进入战斗并正常推进到安全区，车辆、场景、武器图标、DPS 与车速均正常，F7 历史记录可用；两类阻断初始化的空引用没有再出现。UiCaravanCamera 的缺失脚本提示仍可能出现在日志中，本次正常加载时也存在，不把这条既有提示本身当作卡死原因或声称已消除所有游戏警告。

保留全部现有功能、历史格式及个人数据。解压本包不会覆盖 SaveData、DPSMod/history 或 settings.json。backup/World.dll 和 backup/Commons.dll 现在是 v0.8.0.3 的官方原文件，卸载恢复到当前游戏版本。旧版备份仅在本机另存，不混入本次发布包。

构建脚本在编译前检查原始备份及基础程序集的校验值。游戏以后更新时，必须重新取得对应官方文件并适配；不要把旧覆盖包再次解压到更新后的游戏目录。

本机故障现场备份：DPSMod/backup/v1.1.1-before-hotfix。包含修复前的存档、历史、设置、相关 DLL 和错误日志；不随压缩包发布。
