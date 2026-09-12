using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace StopFarmingItsEnough
{
    /// <summary>模组入口与设置界面；采用原版 GUI，不需要额外设置框架。</summary>
    public sealed class FarmingMod : Mod
    {
        public const string ModTitle = "Stop farming, it's enough!";
        public const string HarmonyId = "GentleCode.StopFarmingItsEnough";
        public static FarmingSettings Settings;
        private readonly List<CropEntry> filtered = new List<CropEntry>();
        private Vector2 scroll;
        private string search = "";
        private string previousSearch;
        private const float RowHeight = 80f;
        private int catalogRevision = -1;

        /// <summary>读取设置；待所有模组加载完成后安装补丁并发现植物，确保可选模组类型可用。</summary>
        public FarmingMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FarmingSettings>();
            LongEventHandler.ExecuteWhenFinished(Initialize);
        }

        /// <summary>安装原版与可选兼容补丁；只运行一次，不修改其他模组的文件或存档字段。</summary>
        private static void Initialize()
        {
            CropCatalog.Build();
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(typeof(FarmingMod).Assembly);
            OptionalCompatibility.Install(harmony);
            Log.Message("[Stop farming, it's enough!] Initialized; " + CropCatalog.Entries.Count + " crop/product rows discovered.");
        }

        /// <summary>返回 Mod 设置列表中显示的名称。</summary>
        public override string SettingsCategory()
        {
            return ModTitle;
        }

        /// <summary>绘制总开关、两种工作控制、搜索框及虚拟化植物表格。</summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Small;
            float y = inRect.y;
            DrawToggle(inRect, ref y, "SFIE_Enabled", ref Settings.Enabled);
            DrawToggle(inRect, ref y, "SFIE_ControlSowing", ref Settings.ControlSowing);
            DrawToggle(inRect, ref y, "SFIE_ControlHarvest", ref Settings.ControlHarvest);
            float toggleY = y;
            DrawToggle(new Rect(inRect.x, y, inRect.width * .49f, 28f), ref toggleY,
                "SFIE_HideSowingIcon", ref Settings.HideSowingIcon);
            DrawToggle(new Rect(inRect.x + inRect.width * .51f, y, inRect.width * .49f, 28f), ref y,
                "SFIE_HideHarvestIcon", ref Settings.HideHarvestIcon);
            string explanation = "SFIE_Explanation".Translate();
            float explanationHeight = Mathf.Max(54f, Text.CalcHeight(explanation, inRect.width));
            Widgets.Label(new Rect(inRect.x, y, inRect.width, explanationHeight), explanation);
            y += explanationHeight + 4f;
            // Widgets.Label(new Rect(inRect.x, y, inRect.width, 30f), "SFIE_Compatibility".Translate(
            //     OptionalCompatibility.SmartFarmingActive ? "SFIE_Detected".Translate() : "SFIE_NotDetected".Translate(),
            //     OptionalCompatibility.HighDensityReady ? "SFIE_Detected".Translate() : "SFIE_NotDetected".Translate()));
            y += 32f;
            Widgets.Label(new Rect(inRect.x, y, 85f, 28f), "SFIE_Search".Translate());
            search = Widgets.TextField(new Rect(inRect.x + 85f, y, inRect.width - 85f, 28f), search);
            y += 34f;
            if (!CropCatalog.Ready)
            {
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 48f), "SFIE_Loading".Translate());
                Text.Font = oldFont;
                return;
            }
            if (previousSearch != search || catalogRevision != CropCatalog.Revision) Refilter();
            float width = inRect.width - 20f;
            DrawHeader(new Rect(inRect.x, y, width, 28f));
            y += 30f;
            Rect outer = new Rect(inRect.x, y, inRect.width, Mathf.Max(40f, inRect.yMax - y));
            Rect view = new Rect(0f, 0f, width, filtered.Count * RowHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(filtered.Count, Mathf.CeilToInt((scroll.y + outer.height) / RowHeight) + 1);
            for (int i = first; i < last; i++) DrawRow(new Rect(0f, i * RowHeight, width, RowHeight), filtered[i], i);
            Widgets.EndScrollView();
            Text.Font = oldFont;
        }

        /// <summary>绘制一个设置开关；改变时更新配置版本，不在 GUI 帧内检查库存。</summary>
        private static void DrawToggle(Rect area, ref float y, string key, ref bool value)
        {
            bool before = value;
            Rect row = new Rect(area.x, y, area.width, 28f);
            Widgets.CheckboxLabeled(row, key.Translate(), ref value);
            TooltipHandler.TipRegion(row, (key + "Tip").Translate());
            if (before != value) Settings.Changed();
            y += 30f;
        }

        /// <summary>只在搜索字符串或产物目录改变时过滤目录，支持植物名、产物名和 defName。</summary>
        private void Refilter()
        {
            filtered.Clear();
            foreach (CropEntry entry in CropCatalog.Entries)
            {
                if (Matches(entry.Plant.label) || Matches(entry.Plant.defName)
                    || (entry.Product != null && (Matches(entry.Product.label) || Matches(entry.Product.defName))))
                    filtered.Add(entry);
            }
            previousSearch = search;
            catalogRevision = CropCatalog.Revision;
            scroll = Vector2.zero;
        }

        /// <summary>不区分大小写匹配搜索词；空搜索词匹配全部植物。</summary>
        private bool Matches(string text)
        {
            return string.IsNullOrEmpty(search) || (text != null && text.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        /// <summary>绘制与数据行一致的列标题；阈值为物品件数而非营养值或堆数。</summary>
        private static void DrawHeader(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width * .26f, rect.height), "SFIE_Crop".Translate());
            Widgets.Label(new Rect(rect.x + rect.width * .27f, rect.y, rect.width * .27f, rect.height), "SFIE_Products".Translate());
            Widgets.Label(new Rect(rect.x + rect.width * .55f, rect.y, rect.width * .12f, rect.height), "SFIE_Ignore".Translate());
            Widgets.Label(new Rect(rect.x + rect.width * .77f, rect.y, rect.width * .105f, rect.height), "SFIE_Lower".Translate());
            Widgets.Label(new Rect(rect.x + rect.width * .885f, rect.y, rect.width * .105f, rect.height), "SFIE_Upper".Translate());
        }

        /// <summary>绘制一对作物与产物的独立设置；忽略框贴近文字，错误信息放在输入框下方。</summary>
        private static void DrawRow(Rect rect, CropEntry entry, int index)
        {
            if (index % 2 == 0) Widgets.DrawLightHighlight(rect);
            DrawDefRow(new Rect(rect.x + 4f, rect.y + 3f, rect.width * .27f - 34f, 36f), entry.Plant);
            Rect add = new Rect(rect.x + rect.width * .27f - 27f, rect.y + 7f, 23f, 24f);
            if (Widgets.ButtonText(add, "+")) Find.WindowStack.Add(new Window_AddHarvestProduct(entry.Plant));
            TooltipHandler.TipRegion(add, "SFIE_AddProduct".Translate());
            bool removable = CropCatalog.CanRemoveManual(entry);
            if (removable)
            {
                Rect remove = new Rect(rect.x + rect.width * .55f - 27f, rect.y + 7f, 23f, 24f);
                TooltipHandler.TipRegion(remove, "SFIE_RemoveManualProduct".Translate());
                if (Widgets.ButtonText(remove, "−") && CropCatalog.RemoveManualProduct(entry)) return;
            }
            if (entry.Product != null)
                DrawDefRow(new Rect(rect.x + rect.width * .27f, rect.y + 3f, rect.width * .28f - (removable ? 34f : 8f), 36f), entry.Product);
            if (!entry.Supported)
            {
                Widgets.Label(new Rect(rect.x + rect.width * .55f, rect.y + 3f, rect.width * .45f, 50f),
                    (entry.Product == null ? "SFIE_NoProduct" : "SFIE_Uncounted").Translate());
                return;
            }
            CropRule rule = entry.Rule;
            bool ignoredSowing = rule.IgnoreSowing, ignoredHarvest = rule.IgnoreHarvest;
            int lower = rule.Lower, upper = rule.Upper;
            DrawIgnoreToggle(new Rect(rect.x + rect.width * .55f, rect.y + 3f, rect.width * .21f, 28f),
                "SFIE_IgnoreSowing", ref rule.IgnoreSowing);
            DrawIgnoreToggle(new Rect(rect.x + rect.width * .55f, rect.y + 31f, rect.width * .21f, 28f),
                "SFIE_IgnoreHarvest", ref rule.IgnoreHarvest);
            Widgets.TextFieldNumeric(new Rect(rect.x + rect.width * .77f, rect.y + 3f, rect.width * .105f, 28f),
                ref rule.Lower, ref rule.LowerBuffer, 0f, int.MaxValue);
            Widgets.TextFieldNumeric(new Rect(rect.x + rect.width * .885f, rect.y + 3f, rect.width * .105f, 28f),
                ref rule.Upper, ref rule.UpperBuffer, 0f, int.MaxValue);
            if (ignoredSowing != rule.IgnoreSowing || ignoredHarvest != rule.IgnoreHarvest || lower != rule.Lower || upper != rule.Upper) Settings.Changed();
            if (!Hysteresis.Valid(rule.Lower, rule.Upper) || rule.Lower == 0)
            {
                GameFont old = Text.Font;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + rect.width * .55f, rect.y + 58f, rect.width * .45f, 22f),
                    (Hysteresis.Valid(rule.Lower, rule.Upper) ? "SFIE_ZeroLower" : "SFIE_InvalidThreshold").Translate());
                Text.Font = old;
            }
        }
        /// <summary>分别绘制两类忽略开关；按实际文字宽度让勾选框紧跟文字，不把勾选框推到列末。</summary>
        private static void DrawIgnoreToggle(Rect rect, string key, ref bool value)
        {
            string label = key.Translate();
            rect.width = Text.CalcSize(label).x + 6f + 24f;
            Widgets.CheckboxLabeled(rect, label, ref value);
            TooltipHandler.TipRegion(rect, (key + "Tip").Translate());
        }
        /// <summary>绘制一行定义图标及名称；名称过长时截断，并用悬停提示保留全名与 defName。</summary>
        private static void DrawDefRow(Rect rect, ThingDef def)
        {
            Widgets.DefIcon(new Rect(rect.x, rect.y + 2f, 32f, 32f), def, drawPlaceholder: true);
            string label = def.LabelCap.ToString();
            Widgets.Label(new Rect(rect.x + 40f, rect.y + 6f, rect.width - 40f, 26f), label.Truncate(rect.width - 40f));
            TooltipHandler.TipRegion(rect, label + "\n" + def.defName + "\n" + "SFIE_IndependentRule".Translate());
        }
    }
}






