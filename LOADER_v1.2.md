# v1.2 独立加载器迁移

版本：v1.2（文件版本 1.2.0.0）。日期：2026-10-26。

采用 BepInEx 5.4.23.6 Windows x64，启动时通过 Mono.Cecil 对当前游戏程序集做内存挂接。World.dll、Commons.dll，以及可选音箱层涉及的 Camp.dll，均保持官方磁盘原文件。本次不改变伤害统计口径、功能、快捷键、窗口设置、奇物、车速及历史格式。

## 安装与迁移

退出游戏，把压缩包内容解压到与 `One Way Tickets.exe` 同级的根目录，再双击 `DPSMod/安装或迁移.cmd`。本机已迁移完成，可正常启动。

首次迁移会识别旧版 DPS 和受支持的音箱 v1.3.0 安装记录，校验旧挂接文件及对应原始备份，先备份再还原当前官方 DLL，移除 Managed 中的旧 OneWayDps.dll/OneWaySpeaker.dll，然后安装独立加载器。未知混装或备份损坏时停止，不会随意使用旧版备份覆盖游戏更新。

迁移工具不依赖 .NET 8；使用 Windows 自带 .NET Framework 运行随包兼容检查工具。只有重新编译源码、运行全部开发回归时，才需要编译器及 .NET 8 或更新运行时。

包内不含游戏原程序集、存档、个人历史、窗口设置、音箱歌曲或音箱美术。已安装的音箱层可沿用本机已有资源与运行库。迁移快照保存在 `DPSMod/backup/loader-install-*`；本机迁移前完整现场另存 `DPSMod/backup/v1.2-before-migration`。

## 文件位置

- `winhttp.dll`、`doorstop_config.ini`：BepInEx 的独立启动入口。
- `BepInEx/core`：加载器原版依赖。
- `BepInEx/patchers/OneWayDps.Preloader.dll`：无 Unity/游戏静态引用的预加载器，先准备并验证全部目标，再交给 BepInEx 加载。
- `BepInEx/plugins/OneWayDps`：统计运行库和引导插件。
- `BepInEx/plugins/OneWaySpeaker/OneWaySpeaker.dll`：本机可选音箱运行库；不存在时不挂接音箱。
- `BepInEx/cache/OneWayDps/<内容哈希>/OneWayDps.dll`：Mod 自身运行库缓存，用于保持 Mono 单一程序集身份；不是游戏 DLL，也不覆盖 Managed。World/Commons/Camp 的改动只在内存。
- `BepInEx/LogOutput.log`：加载器、兼容性和插件日志。
- `DPSMod/settings.json`、`DPSMod/history`：继续使用既有个人数据，无需转格式。

源代码：`src/PatchWorld.cs` 是磁盘开发工具和内存加载器共用的挂接实现；`loader/Preloader.cs` 实现预检查与独立加载，`loader/SpeakerMemoryPatch.cs` 保留可选音箱挂接。统计运行代码仍在 src/，仅更新版本标识。

## 游戏更新之后

每次启动读取当时的官方 DLL，不再依赖上一次安装生成的修改版游戏 DLL。只改变游戏内部实现、且所需接口保持兼容的小更新，通常无需再次覆盖文件。启动检查不以固定 Steam Build 或整文件哈希拒绝新版本。

加载前验证 Mod 所用游戏方法和字段、全部伤害调用覆盖、游戏类型与字段保留、原生存档调用顺序及候选程序集编码。若所需接口被移除、旧静态挂接残留或验证失败，整组准备阶段停用 DPS，并在日志记录原因。不能保证所有未来更新都兼容：方法签名、伤害入口、计时语义或数据格式变化仍需人工适配。保留源码回归与当前原始备份，不要使用 v1.1.2 及更早的覆盖包。

`DPSMod/修复更新.cmd` 现在用于检查/恢复独立加载器文件，不再生成静态游戏 DLL 补丁。只检查可运行 `Install.ps1 -CheckOnly`。更新加载器后若其他 Mod 已提供不同版本的共享 BepInEx 文件，安装器会停止，避免覆盖别人的加载环境。

## 卸载

存在音箱层时，先运行 `SpeakerMod/卸载.cmd`，只停用音箱插件；再运行 `DPSMod/Uninstall.cmd` 停用 DPS 插件和预加载器。保留共享 BepInEx 文件，以免影响其他插件。卸载不回写游戏 DLL，不回退游戏版本，不删除存档、设置和历史。

## 中文路径兼容

此 Unity Mono 在非 ASCII 工作目录加载内存程序集时可触发 CodeBase 编码异常。预加载阶段临时采用系统目录作为工作目录，并在游戏启动前恢复；不修改系统区域设置或游戏保存路径。运行库使用统一 LoadFrom 身份，避免多份程序集分裂静态统计状态。依据及实测边界见 VALIDATION.md。

## 依赖与资料

- BepInEx 官方发布：https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6
- 预加载器契约：https://docs.bepinex.dev/articles/dev_guide/preloader_patchers.html
- Mono 编码问题：https://github.com/mono/mono/issues/20968
- 依赖许可证及来源：licenses/、THIRD_PARTY_NOTICES.md；Doorstop 对应源码包附于 vendor/source/。
