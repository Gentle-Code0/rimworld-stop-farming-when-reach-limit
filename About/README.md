# Stop farming, it's enough!

作者沿用 visitor-spot：`"GentleCode"`。适用于 RimWorld **1.6**，开发时对照本机 **1.6.4871 rev590**。

## 1.4.2 内部标识与目录统一

显示名称为 **Stop farming, it's enough!**。目录、Visual Studio 项目、程序集和命名空间统一为 **StopFarmingItsEnough**；packageId 和 HarmonyId 为 **GentleCode.StopFarmingItsEnough**。语言键前缀统一为 SFIE_，地图状态字段使用 sfiePausedByPlant。

打开 Source/StopFarmingItsEnough.sln，输出为 1.6/Assemblies/StopFarmingItsEnough.dll。旧 DLL 已移除，避免重复加载。本次按测试阶段改名，不增加旧标识迁移层；游戏中需要重新启用模组，旧设置或旧存档组件不保证自动迁移。
## 1.4 独立忽略播种与收获

每条“作物＋产物”规则现在有两个独立勾选框，位于“忽略限制”列中：

- **忽略播种**：勾选后，该产物不对该作物的播种施加库存限制。
- **忽略收获**：勾选后，该产物不对该作物的收获施加库存限制。
- 新发现植物、自动发现副产物、手动新建关联默认两项都勾选。取消需要控制的那一项即可生效；总开关及对应的自动控制开关也需要开启。
- 旧配置的 ignore 值同时迁移给两个方向，保留已有规则的效果；不会将玩家已启用的限额全部重置。新文件显式保存 ignoreSowing 和 ignoreHarvest，读取时新字段优先于旧字段。
- 上下阈值和每个产物的滞回记忆仍共用，不增加第二套库存扫描。每类工作分别汇总：只要该作物某个未忽略此类工作的产物仍处于暂停状态，此类工作就暂停。不同产物可以分别限制不同工作。
- 勾选忽略立即释放该产物在对应方向上的限制。取消忽略和阈值修改仍在下一次 600-tick 检查更新；删除手动关联会立即释放其两个方向的限制。其他产物仍有效的限制继续保留。
- 暂停图标、右键灰色原因、任务结束条件、HDH 适配及收获设施搜索缓存均使用各自方向的忽略开关。忽略播种不会意外解除收获预过滤；忽略收获会立即放行收获设施缓存。
- 两个忽略框仍紧跟文字。每行增加高度以容纳两个开关，植物与产物仍分别显示图标，阈值列位于右侧。

例如：只希望库存过量时停播种、继续收获，取消“忽略播种”而保留“忽略收获”；反之则交换两项勾选状态。

## 1.3.1 按定义过滤不会自动收获的植物（保留）

原版提供 PlantProperties.Harvestable（当前版本由 harvestYield > 0.001 派生）和 autoHarvestable。Plant.HarvestableNow 则是随生长改变的当前状态，本优化不调用它，也不轮询 Growth、LifeStage 或 CanYieldNow。

- 每种植物定义首次进入设施快照时分类一次并缓存；同一定义的后续植物复用结果。已有植物随设施首次建立快照时分类，新植物通过原版格子登记事件纳入。无需额外的生长状态检查间隔。
- 对使用普通 Plant 类、没有组件和未知收获补丁的定义，只有 Harvestable 与 autoHarvestable 同时为真才参与普通收获候选组成。原版及采用相同定义逻辑的其他模组使用同一判定，不按模组名称、植物名字或是否野生区分。
- 能自动收获的植物从幼苗阶段就纳入；成熟时无需重新分类。不能自动收获的草不会使暂停作物区域变成混种；未来能自动收获的野生浆果等仍会导致保守的逐株回退。
- 自定义植物类、任何植物组件、缺失定义信息均保守纳入。若首次分类时发现其他程序集修改 HasJobOnCell、HarvestableNow 或 Harvestable，则本轮会话保守保留所有植物，不尝试猜测补丁语义。这也包括其他模组仅追加限制的补丁。
- 自定义派生收获器不使用设施预过滤，保留现有逐株拦截。手动指定/右键强制收获仍走原有流程；分类只服务于普通自动收获搜索，不代表植物不能被玩家砍除或手动收获。
- 定义分类假设模组加载后定义和收获补丁保持稳定；运行中动态改写这些定义或安装新收获补丁需要专门适配，不通过周期扫描检测。
- 植物出现、移除、移动以及区域调整继续使用 1.3 的事件驱动快照维护。没有新增逐 tick 或定期的植物生长扫描。库存阈值检查仍固定为每 600 ticks，两者互相独立。

实现增加 Source/HarvestEligibility.cs，设置中的中英文说明已同步更新。

## 1.3 收获派工搜索优化（1.3.1 对候选植物进一步筛选）

无需额外设置；启用“自动暂停和恢复收获”后自动使用，独立于 Smart Farming 的停止收获功能。

- 在原版收获派工的设施资格入口提前排除：**设施内实际植物非空、种类单一、没有其他种植建筑造成归属不明，且该实际作物已被库存规则暂停**。它的格子不会进入正常收获派工候选列表。
- 混种（即使几种作物都暂停）、空设施、重叠种植建筑或无法确认的范围保守回退到现有逐株检查。按实际地上作物判断，不按改种后选择的下一轮作物判断。
- 每个设施首次被搜索时读取一次设施范围的植物组成，并缓存种类计数。后续小人搜索只查设施缓存和已有库存暂停状态，不重新遍历整片田。
- 原版 ThingGrid 的逐格登记/注销回调维护植物快照。播种、实际收获移除、死亡销毁、移植移动及 HDH 输出植物，只重读受影响的已缓存格子；支持多格植物和共享格子的多个缓存。其他物品经过类型快速判断后返回。
- 区域增减格子、注销、种植建筑移除或搬移会注销对应缓存，下一次需要时再建立；不在每个拖拽事件中重扫田地。缓存只存在内存，随地图对象释放，不写入存档。
- 库存变化直接查询原有暂停状态；忽略、删除手工关联和关闭控制继续立即放行，不等待植物缓存更新。库存仍固定每 600 ticks 读取原版缓存。
- 右键菜单预览绕过设施预过滤，继续使用原有“可见但不可选”的原因提示流程。正在执行的收获任务和手动指定收获仍由原有逐株/任务检查兜底；本次优化不替换指定收获的全局物品搜索。
- 代价：首次搜索及编辑后的首次搜索需要读取设施格子，缓存内存随实际参与搜索的设施格子数增长；不是零开销。无场景的验证证明减少了搜索次数，不代表已测得真实游戏 TPS 提升。
- 第三方若完全替换候选枚举或重写设施资格方法且不调用原版入口，可能只能获得逐株拦截；本实现不调用或修改 Smart Farming 的停止收获设置。

实现文件为 Source/HarvestAreaCache.cs，详细验证见 VALIDATION.md。

## 1.2.1 删除手动关联

- 新版手动关联的收获产物右侧显示 **−** 按钮。点击即可删除关联及其阈值；该关联造成的暂停立即解除，其他产物的有效限制继续生效。
- 删除后可通过作物旁的 **＋** 重新关联；重新添加默认忽略，使用默认阈值和全新的暂停记忆。删除最后一个产物后，作物仍显示在列表中。
- 标准主产物、XML 声明产物、实际收获接口自动发现的产物不可删除。手动关联后来被实际收获确认为真实产物时，会自动转为受保护的自动关联。
- **1.2 及更早版本未记录来源，无法可靠识别旧的手动关联。** 旧版来源不明的关联不可删除，仍可勾选“忽略”停用。重复点击“＋”不会将自动或来源不明的关联改成可删除项。
- 关联来源和新建手工关联的状态标识随模组设置保存。固定 600-tick 库存检查和原版统计口径不变。

## 1.2 多产物与显示设置

- 设置表格分成“作物 / 收获产物 / 忽略 / 下限 / 上限”五列，作物和产物均保留 32 UI 像素图标。一种作物的各产物分别成行，分别保存忽略与阈值；“忽略”勾选框紧跟文字。
- 每个作物＋产物使用独立滞回状态。**任意未忽略且受支持的产物处于暂停状态，整种作物就暂停；所有有效产物的暂停状态都解除后才恢复。** 同产物由不同作物产出时，各作物仍独立配置。
- 设置新增“隐藏暂停播种图标”和“隐藏暂停收获图标”。只改变显示，不改变暂停及右键原因提示；隐藏其中一个后，另一个自动居中。图标不绘制黑底，操作图案与红叉均为 **70% 不透明度（alpha = 0.7）**。
- 旧版本配置及地图暂停记忆自动迁移到主产物；新增副产物默认忽略，避免意外阻止工作。
- 标准主产物在加载时发现；使用游戏 ThingComp.GetAdditionalHarvestYield() 接口提供的额外／随机产物，在首次实际收获且产出数量大于零时识别。观察器不会提前运行收获代码，也不会修改产物与数量。发现后进入设置页配置，随模组设置保存。
- 完全自定义产出代码不保证能自动识别。点击作物旁 **＋**，搜索并关联产物，再配置其阈值、取消忽略；错误的新版手动关联可点击产物旁的 − 删除，也可勾选忽略。同一关联不会重复添加。手工关联不改变作物实际产出。
- 模组作者也可以在植物 ThingDef 的 modExtensions 中声明下列扩展，让副产物在启动时直接出现：

    <li Class="StopFarmingItsEnough.HarvestProductsExtension">
      <products>
        <li>实际副产物DefName</li>
      </products>
    </li>

库存仍严格每 600 ticks 读取原版缓存，同一种产物每轮只读取一次。目录发现与手动关联不扫描任何库存。任意第三方自动农业的工作入口是否受控，仍取决于它是否使用本模组已覆盖的工作入口。

## 1.1 界面改进（保留功能）

- 设置表格分别显示植物和产物的 **32 UI 像素图标**，自动使用各模组的定义图标；长名称截断，悬停可查看全名。
- 自动暂停时，无须选中，种植区和水培箱上直接显示 **28 UI 像素**状态图标。使用原版种植区／收获图标叠加原版红叉；播种和收获同时暂停时并排显示，中间留 4 UI 像素间隙。不需要手工合成图片或额外 Textures 文件。
- 地图图标替代原来的选中信息栏附加文字；悬停图标可看到原因。种植区按设定作物显示；HDH 收获标记按内部当前批次植物显示。
- 右键正常可派工的目标，保留原版优先工作选项，但暂停期间不可点击。中文分别附加 `（因库存限额暂停播种）` 和 `（因库存限额暂停收获）` 对应提示（游戏实际括号使用半角）；英文含义一致。
- 仅在同步生成菜单时预览工作资格，返回菜单前清空执行回调；异常路径也恢复上下文。技能、可达性、手动 Off、Smart Farming 模式等原有规则继续有效，因此本来就不能生成工作的目标不会凭空新增选项。
- 图标只查询已缓存的暂停状态，不增加库存检查次数。设施引用每 600 ticks 或列表／区域形状变化时更新；区域质心只在首次显示或编辑形状后计算，屏外和迷雾中的图标不绘制。

## 安装与使用

1. 将整个模组目录放在 RimWorld/Mods 中；本机已处于该位置，已提供编译好的 `1.6/Assemblies/StopFarmingItsEnough.dll`。
2. 在游戏模组列表启用 **Harmony** 和 **Stop farming, it's enough!**。
3. 如果启用了 **Smart Farming** 或 **High Density Hydroponics**，把本模组放在它们下方。两者均为可选项，About.xml 只将 Harmony 列为强依赖。
4. 重启游戏，在“选项 → 模组设置 → Stop farming, it's enough!”中配置。
5. 总开关和自动播种控制默认开启；自动收获控制默认关闭；每种新发现的作物＋产物默认勾选“忽略”。搜索目标植物，设置上下限后取消“忽略”。
6. 播种和收获控制可以分别启用；启用的功能共用该作物各产物的上下限和滞回状态。设置示例：水稻下限 500、上限 1500；大米库存严格大于 1500 时暂停，严格小于 500 时恢复。
7. 库存等于上下限或位于两者之间时，保持上次状态。首次启用没有历史状态的规则，在区间内默认不暂停。
8. 每张地图独立统计、独立记忆暂停状态；阈值配置是全局 Mod 设置，适用于各地图和存档。

**时间口径：**固定每 **600 ticks** 检查一次，正常 60 TPS 时约 10 秒；游戏加速会缩短现实时间间隔，低 TPS 时会变长，暂停时不检查。此处严格采用最终需求的 tick 口径。

**设置生效：**关闭总开关、关闭某工作控制或勾选忽略，会立即解除对应库存限制。新规则、取消忽略及阈值修改在下一个检查点更新判定。禁用、忽略不会清除已记住的滞回状态；重新启用后区间内继续保持历史状态。

**阈值校验：**要求 `0 ≤ 下限 < 上限`，无效规则暂不控制。下限为 0 时，非负库存不可能严格小于 0，因此暂停后无法自动恢复；通常应使用正下限。

## 植物发现与统计口径

- 所有模组定义加载后，只遍历一次 ThingDef，自动发现有 sowTags 的植物，以 `plant.harvestedThingDef` 找到标准收获产物。
- 规则键为植物与产物的 **defName 组合**。两种植物即使产出同一种物品，也能各自设置不同阈值、各自忽略、各自保持状态。
- 每轮对相同产物只读取一次 `map.resourceCounter.AllCountedAmounts`。这是原版 `GetCount` 使用的同一个字典；不调用 `UpdateResourceCounts`、`CheckUpdateResource`，不遍历储存区或地面库存。
- 原版统计不包含的产物显示为不受支持；缓存缺失键时不把它当成 0，不执行这条规则，保留历史状态。
- 数量是原版认可的库存物品件数。未入库收获物、背包、商队及特殊容器是否计入，完全遵循原版或修改了原版资源计数器的其他模组。
- 普通新增作物自动兼容；多产物通过标准额外收获接口观察、XML 扩展声明或手工关联登记，详见 1.2 说明。没有标准产物的作物也可关联。完全自定义自动农业流程可能需要专门适配。

## Smart Farming 兼容

开发时检查了本地 Workshop **3220129183** 的 1.6 DLL；packageId 为 `Owlchemist.SmartFarming`。

本模组直接保留 Smart Farming 的 On / Smart / Force / Off 按钮、状态、季节判断与优先级，不写入 `sowMode` 或 `allowSow`。在它的工作判断之外叠加库存限制：

```text
最终播种资格 = 原版与 Smart Farming 允许播种 AND 没有库存暂停
```

因此库存回落只是解除本模组的锁，不会把手动 Off 改成 On，也不会把 Smart 改成普通种植。Force 仍受库存锁约束；需要手动绕过库存限制时，勾选该植物所有相关产物的“忽略”。Smart Farming 已有的收获限制也会保留，本模组从不强制将其他模组拒绝的收获工作重新开启。

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

打开 `Source/StopFarmingItsEnough.sln`，选择 **Release / Any CPU** 后生成。

要求 Visual Studio 2022（安装 .NET 桌面开发工作负载、.NET Framework 4.8 targeting pack 和可用的 .NET SDK）。项目采用 SDK 风格 C# 类库、目标 **.NET Framework 4.8**、C# 7.3。

默认按当前目录布局引用本地游戏 DLL 和 Steam Workshop 的 Harmony DLL。没有下载 NuGet 包的构建依赖，不复制游戏 DLL、Harmony DLL 或两个农业模组 DLL到输出目录。

命令行编译：

```powershell
dotnet build '.\Source\StopFarmingItsEnough.sln' -c Release
```

如果游戏或 Harmony 位于其他位置，传入 MSBuild 属性，或在 Source 下自行创建 Directory.Build.props 定义相同属性：

```powershell
dotnet build '.\Source\StopFarmingItsEnough.sln' -c Release '-p:RimWorldDir=D:\Games\RimWorld' '-p:HarmonyPath=D:\Mods\Harmony\Current\Assemblies\0Harmony.dll'
```

输出固定到 `1.6/Assemblies/StopFarmingItsEnough.dll`。根目录 `loadFolders.xml` 只加载 1.6 内容。无需为此纯代码模组创建空 Defs、Textures 或复制 visitor-spot 的建筑、美术和发布 ID。

## 代码结构和实现过程

1. 对照 visitor-spot 的元数据和版本化布局，建立独立 About、Source/Properties、1.6/Assemblies、1.6/Languages。
2. 核对本地游戏及可选模组程序集，确定播种、收获、任务队列和批次处理入口。
3. `Hysteresis.cs`：严格上下限滞回纯逻辑。
4. `FarmingSettings.cs`、`CropCatalog.cs`：作物＋产物配置、主产物发现与额外产物登记、设置持久化。
5. `MapComponent_FarmingLimits.cs`：每地图状态与存档、按产物去重、600-tick 原版缓存读取。
6. `FarmingGate.cs`、`WorkPatches.cs`：工作资格过滤、实际植物判定、任务执行阶段严格暂停。
7. `OptionalCompatibility.cs`：保留 Smart Farming 原模式，动态安装 HDH 可选补丁。
8. `FarmingMod.cs`：原版设置窗口，搜索结果缓存，只绘制可见列表行，全部界面文本提供英／简中同义版本。
9. `FarmingOverlay.cs`、`PauseIcons.cs`：缓存设施引用和区域锚点，叠绘原版状态图标。
10. `FarmingMenuPreview.cs`：右键工作菜单预览作用域、禁用选项和本地化原因提示。

每个命名函数均有用途注释；关键设计边界有额外中文注释。各功能只使用主线程，不创建后台库存扫描线程，不添加额外建筑。

## English usage

Version 1.1 adds separate 32 UI-pixel crop/product icons in settings and 28 UI-pixel map markers using vanilla growing/harvest icons plus the vanilla cancel cross. Two pause markers sit side by side with a 4 UI-pixel gap. Map markers replace the extra inspect-pane text. Normal right-click work options remain visible but disabled during an inventory pause, with a parenthesized reason. Existing manual Off, Smart Farming, skill and reachability rules still apply. No custom texture files are needed.

Enable Harmony and this mod. Load this mod **after** Smart Farming and High Density Hydroponics if either is enabled; neither is required. Restart RimWorld, then open Options → Mod settings → Stop farming, it's enough!.

The master switch and sowing control default to on; harvest control defaults to off. Newly discovered crops default to **Ignore**. Set a crop's limits and uncheck Ignore. Each crop has independent limits and hysteresis memory, even if multiple crops produce the same item. Sowing and harvesting have separate global switches but share that crop's limits and state.

Stocks strictly above the upper limit pause enabled work; stocks strictly below the lower limit resume it. Equality and the range between limits preserve the previous state. Each map has separate stock counts and saved states. Require `0 ≤ lower < upper`; lower = 0 prevents automatic resumption. Rule changes are evaluated at the next fixed **600-tick** check, approximately 10 seconds at 60 TPS, faster in real time at accelerated game speeds. Disabling a control or checking Ignore releases its restrictions immediately without erasing remembered states.

Smart Farming modes and manual Off are preserved. Inventory limits also apply in Force mode. HDH uses **strict pause**, including partially sown batches; existing growth, aging and environmental damage continue. With harvest control enabled, both internal batch output and pawn harvest jobs pause. With only sowing control enabled, existing repeat-harvest plants can keep producing. Cutting commands and wild plants outside managed growing areas are unaffected.

The mod reads the existing vanilla resource-count dictionary; it never rescans stored items. Standard mod crops are discovered automatically. Unsupported or non-counted products are skipped. Stock-count refresh and check delays mean this is not a hard cap. Paused crops may still age and die.

Open `Source/StopFarmingItsEnough.sln` in Visual Studio 2022 with the .NET Framework 4.8 targeting pack and .NET SDK installed. Build Release / Any CPU. Set `RimWorldDir` and `HarmonyPath` MSBuild properties if your installation differs. Output goes to `1.6/Assemblies`.








