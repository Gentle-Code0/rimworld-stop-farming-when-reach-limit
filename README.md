# Stop farming when reach limit

作者沿用 visitor-spot：`"GentleCode"`。适用于 RimWorld **1.6**，开发时对照本机 **1.6.4871 rev590**。

## 安装与使用

1. 将整个模组目录放在 RimWorld/Mods 中；本机已处于该位置，已提供编译好的 `1.6/Assemblies/StopFarmingWhenReachLimit.dll`。
2. 在游戏模组列表启用 **Harmony** 和 **Stop farming when reach limit**。
3. 如果启用了 **Smart Farming** 或 **High Density Hydroponics**，把本模组放在它们下方。两者均为可选项，About.xml 只将 Harmony 列为强依赖。
4. 重启游戏，在“选项 → 模组设置 → Stop farming when reach limit”中配置。
5. 总开关和自动播种控制默认开启；自动收获控制默认关闭；每种新发现的植物默认勾选“忽略”。搜索目标植物，设置上下限后取消“忽略”。
6. 播种和收获控制可以分别启用；启用的功能共用该植物的上下限和滞回状态。设置示例：水稻下限 500、上限 1500；大米库存严格大于 1500 时暂停，严格小于 500 时恢复。
7. 库存等于上下限或位于两者之间时，保持上次状态。首次启用没有历史状态的规则，在区间内默认不暂停。
8. 每张地图独立统计、独立记忆暂停状态；阈值配置是全局 Mod 设置，适用于各地图和存档。

**时间口径：**固定每 **600 ticks** 检查一次，正常 60 TPS 时约 10 秒；游戏加速会缩短现实时间间隔，低 TPS 时会变长，暂停时不检查。此处严格采用最终需求的 tick 口径。

**设置生效：**关闭总开关、关闭某工作控制或勾选忽略，会立即解除对应库存限制。新规则、取消忽略及阈值修改在下一个检查点更新判定。禁用、忽略不会清除已记住的滞回状态；重新启用后区间内继续保持历史状态。

**阈值校验：**要求 `0 ≤ 下限 < 上限`，无效规则暂不控制。下限为 0 时，非负库存不可能严格小于 0，因此暂停后无法自动恢复；通常应使用正下限。

## 植物发现与统计口径

- 所有模组定义加载后，只遍历一次 ThingDef，自动发现有 sowTags 的植物，以 `plant.harvestedThingDef` 找到标准收获产物。
- 规则键为植物 **defName**。两种植物即使产出同一种物品，也能各自设置不同阈值、各自忽略、各自保持状态。
- 每轮对相同产物只读取一次 `map.resourceCounter.AllCountedAmounts`。这是原版 `GetCount` 使用的同一个字典；不调用 `UpdateResourceCounts`、`CheckUpdateResource`，不遍历储存区或地面库存。
- 原版统计不包含的产物显示为不受支持；缓存缺失键时不把它当成 0，不执行这条规则，保留历史状态。
- 数量是原版认可的库存物品件数。未入库收获物、背包、商队及特殊容器是否计入，完全遵循原版或修改了原版资源计数器的其他模组。
- 普通新增作物自动兼容。多产物、随机产物只监控标准 harvestedThingDef；无标准产物或自定义自动农业流程需要专门适配，不声称支持所有任意 C# 农业实现。

## Smart Farming 兼容

开发时检查了本地 Workshop **3220129183** 的 1.6 DLL；packageId 为 `Owlchemist.SmartFarming`。

本模组直接保留 Smart Farming 的 On / Smart / Force / Off 按钮、状态、季节判断与优先级，不写入 `sowMode` 或 `allowSow`。在它的工作判断之外叠加库存限制：

```text
最终播种资格 = 原版与 Smart Farming 允许播种 AND 没有库存暂停
```

因此库存回落只是解除本模组的锁，不会把手动 Off 改成 On，也不会把 Smart 改成普通种植。Force 仍受库存锁约束；需要手动绕过库存限制时，勾选该植物的“忽略”。Smart Farming 已有的收获限制也会保留，本模组从不强制将其他模组拒绝的收获工作重新开启。

## High Density Hydroponics 兼容

开发时检查了本地 Workshop **3534458015** 的 DLL；packageId 为 `MapleApple.HighDensityHydroponics.Fixed.ZouHb.zades`。

- 在 `CanAcceptSowNowInternal()` 上额外收紧播种条件；即使只播种了部分批次也严格暂停，没有“种满再停”的例外。
- 同时约束普通播种入口和正在执行的播种任务，防止已领取工作绕过暂停。未完成幼苗由原版任务结束清理，已经完成并存入箱内的植物保留。
- 不拦截 `HandleSowing()`：已完成播种的植物仍可被接收入内部，已填满的批次仍可自然进入生长阶段。部分批次通常需要恢复播种并填满后才继续整批生长，这是严格暂停策略的实际代价。
- 收获控制启用且当前批次被暂停时，阻止 `HandleHarvest()` 输出新植物，同时阻止小人收获已经输出的植物。当前批次使用实际存储植物类型，不误用下一轮选定的植物。
- 不停止建筑 Tick，不断电、不清空、不重置生长进度。原本的环境伤害与衰老继续；长时间暂停仍可能导致作物死亡。
- 重复收获植物也受收获开关控制；若只启用播种控制，已有重复收获植物仍可继续产出，因为它们无需重新播种。
- 用运行时反射定位可选入口，工作时无反射查找。版本不符时写一条启动错误，不擅自猜测字段；设置页显示适配未启用。未安装 HDH 时不会安装这些补丁。

## 收获范围与严格暂停

自动收获限制覆盖玩家种植区和玩家种植设施内的正常收获、指定收获、Smart Farming 的收获标记，以及已领取的收获队列。根据每株实际植物判断，避免改种时误控制旧作物。区域以外的野生植物、普通砍除与清理指令不受影响。

设置页两个开关独立：只控制播种时，已有作物继续生长、收获；同时控制收获时，成熟作物会等待库存下降。库存不是硬上限：原版库存缓存刷新和 600-tick 检查之间存在延迟，已经掉落但尚未入库的产物还可能继续入库。

## Visual Studio 打开与编译

打开 `Source/StopFarmingWhenReachLimit.sln`，选择 **Release / Any CPU** 后生成。

要求 Visual Studio 2022（安装 .NET 桌面开发工作负载、.NET Framework 4.8 targeting pack 和可用的 .NET SDK）。项目采用 SDK 风格 C# 类库、目标 **.NET Framework 4.8**、C# 7.3。

默认按当前目录布局引用本地游戏 DLL 和 Steam Workshop 的 Harmony DLL。没有下载 NuGet 包的构建依赖，不复制游戏 DLL、Harmony DLL 或两个农业模组 DLL到输出目录。

命令行编译：

```powershell
dotnet build '.\Source\StopFarmingWhenReachLimit.sln' -c Release
```

如果游戏或 Harmony 位于其他位置，传入 MSBuild 属性，或在 Source 下自行创建 Directory.Build.props 定义相同属性：

```powershell
dotnet build '.\Source\StopFarmingWhenReachLimit.sln' -c Release '-p:RimWorldDir=D:\Games\RimWorld' '-p:HarmonyPath=D:\Mods\Harmony\Current\Assemblies\0Harmony.dll'
```

输出固定到 `1.6/Assemblies/StopFarmingWhenReachLimit.dll`。根目录 `loadFolders.xml` 只加载 1.6 内容。无需为此纯代码模组创建空 Defs、Textures 或复制 visitor-spot 的建筑、美术和发布 ID。

## 代码结构和实现过程

1. 对照 visitor-spot 的元数据和版本化布局，建立独立 About、Source/Properties、1.6/Assemblies、1.6/Languages。
2. 核对本地游戏及可选模组程序集，确定播种、收获、任务队列和批次处理入口。
3. `Hysteresis.cs`：严格上下限滞回纯逻辑。
4. `FarmingSettings.cs`、`CropCatalog.cs`：逐植物配置、一次性自动发现、设置持久化。
5. `MapComponent_FarmingLimits.cs`：每地图状态与存档、按产物去重、600-tick 原版缓存读取。
6. `FarmingGate.cs`、`WorkPatches.cs`：工作资格过滤、实际植物判定、任务执行阶段严格暂停、选中说明。
7. `OptionalCompatibility.cs`：保留 Smart Farming 原模式，动态安装 HDH 可选补丁。
8. `FarmingMod.cs`：原版设置窗口，搜索结果缓存，只绘制可见列表行，全部界面文本提供英／简中同义版本。

每个命名函数均有用途注释；关键设计边界有额外中文注释。各功能只使用主线程，不创建后台库存扫描线程，不添加额外建筑。

## English usage

Enable Harmony and this mod. Load this mod **after** Smart Farming and High Density Hydroponics if either is enabled; neither is required. Restart RimWorld, then open Options → Mod settings → Stop farming when reach limit.

The master switch and sowing control default to on; harvest control defaults to off. Newly discovered crops default to **Ignore**. Set a crop's limits and uncheck Ignore. Each crop has independent limits and hysteresis memory, even if multiple crops produce the same item. Sowing and harvesting have separate global switches but share that crop's limits and state.

Stocks strictly above the upper limit pause enabled work; stocks strictly below the lower limit resume it. Equality and the range between limits preserve the previous state. Each map has separate stock counts and saved states. Require `0 ≤ lower < upper`; lower = 0 prevents automatic resumption. Rule changes are evaluated at the next fixed **600-tick** check, approximately 10 seconds at 60 TPS, faster in real time at accelerated game speeds. Disabling a control or checking Ignore releases its restrictions immediately without erasing remembered states.

Smart Farming modes and manual Off are preserved. Inventory limits also apply in Force mode. HDH uses **strict pause**, including partially sown batches; existing growth, aging and environmental damage continue. With harvest control enabled, both internal batch output and pawn harvest jobs pause. With only sowing control enabled, existing repeat-harvest plants can keep producing. Cutting commands and wild plants outside managed growing areas are unaffected.

The mod reads the existing vanilla resource-count dictionary; it never rescans stored items. Standard mod crops are discovered automatically. Unsupported or non-counted products are skipped. Stock-count refresh and check delays mean this is not a hard cap. Paused crops may still age and die.

Open `Source/StopFarmingWhenReachLimit.sln` in Visual Studio 2022 with the .NET Framework 4.8 targeting pack and .NET SDK installed. Build Release / Any CPU. Set `RimWorldDir` and `HarmonyPath` MSBuild properties if your installation differs. Output goes to `1.6/Assemblies`.
