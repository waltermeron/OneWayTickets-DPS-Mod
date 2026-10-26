# 伤害口径与漏记审计

依据本地游戏 v0.8.0.2 的 World.dll IL，参考既有 `world-il.txt` 原始程序集转储：

- `World.Enemys.Enemy::World.ITarget.hurt` 将 `Dmg_Data.dmg` 从 `Enemy.hp` 直接扣除（IL_0069–IL_0079），允许结果为负；随后将同一 `Dmg_Data` 交给飘字。
- `World.VFXs.Damage_PopUps.Damage_PopUp_Mono::init` 将 `Dmg_Data.dmg` 原值传入 `PopInit`（IL_005c–IL_0068）。`PopInit` 对该整数逐位取模、绘制数字，没有将伤害除以 10、100 或 1000。
- 游戏 `make_damage_count` 累加原始 `Dmg_Data.dmg`，包括溢出；Mod 自始保留“实际生命值下降”口径，扣除溢出。因此剩余 40 血的敌人飘出 300 时，有效累计只增加 40，并不是单位缩小。
- 发现实际漏记：枚举 `WorldEnum.Faction` 为 player=0、neutral=1、opposite=2；`Adherent.get_Faction` 固定返回 neutral。`Adherent.tick` 以自身作为 `ITarget.hurt` 的 source，而原攻击者存放在 `_source`。旧版只判断 source.Faction==player，导致玩家来源的附着持续伤害被过滤。

修复只对 Adherent 沿 `_source` 链判定所有者，最多 16 层；Projectile 的显式 faction 保持权威，避免把被敌人反射的玩家弹丸误算回玩家。无来源、敌方来源和循环附着链不会纳入。原 `hurt` 仍只执行一次，返回结果和伤害计算不改变。

`CombatStats.ReportedDamage` 记录符合来源及命中条件的原始伤害和，`Total.Total` 保留有效伤害。`DifferenceDamage = max(0, ReportedDamage - Total.Total)`，用于显示“差额（含溢出）”；对特殊目标该差额也可能含伤害上限等原因，不将全部差额断言为过量伤害。

独立回归程序 `tests/DamageAccountingTests.cs` 共 56 项断言，覆盖一般命中、300 飘字/40 实际扣血、玩家/敌人来源的中立附着伤害、嵌套来源、反射弹丸、循环链、空来源、未命中/免疫/治疗、原函数执行次数、原结果保留以及诊断重置。
