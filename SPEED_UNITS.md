# 车速单位核对（游戏 v0.8.0.2）

车速以 **m/s（米/秒）** 显示，计算为 `abs(caravan_velocity.x) × 2`。

距离依据：

- `CaravanMgr.tick` 保存旧 `caravan_pos`，移动后将新旧位置的水平差写入 `caravan_move_delta_dis`（IL_007E–IL_00A2）。
- `ProgressMgr.tick` 将该位移原值传入 `AddProgress`（IL_004D–IL_0052），`Progress.Move` 直接累加至 `m_current_progress`（IL_001F–IL_0026）。
- `ProgressUiView.IProgressView.notify_on_tick` 将剩余进度乘以 2 后作为界面的米制距离显示（IL_004D–IL_0063）。因此每个内部距离单位对应界面 2 米。

时间依据：

- `CaravanMover_Run.do` 按 `velocity += acceleration × 0.008333334` 和 `position.x += velocity.x × 0.008333334` 积分（IL_01A1–IL_01BD、IL_02AE–IL_02C3）。
- `WorldSceneRoot.on_init` 将同一 1/120 秒步长传给计时器。计时器根据 `Time.deltaTime` 累积和推进固定步长，并把同一步长传给 `Physics2D.Simulate`。
- 速度字段已经是内部距离单位/秒，无需再乘以 120；转换为界面米制只需乘以 2。

显示水平速度绝对值，跳跃竖直速度不计入。场景尚未初始化或出现非有限值时显示 `--`。v1.0.1 显示的是未换算的内部速度，v1.0.2 补充了米制换算和单位标签。
