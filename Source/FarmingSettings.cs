using System;
using System.Collections.Generic;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>来源不明用于兼容旧设置，不能假定它是手工关联。</summary>
    public enum ProductLinkSource { Unknown, Automatic, Manual }

    /// <summary>单个作物＋产物的全局配置；同产物的不同作物互不覆盖。</summary>
    public sealed class CropRule : IExposable
    {
        public string PlantDefName;
        public string ProductDefName;
        public ProductLinkSource Source;
        public string StateId;

        /// <summary>手工关联每次新建都有独立状态键，删除后重建不会继承旧存档中的暂停记忆。</summary>
        public string StateKey => FarmingSettings.Key(PlantDefName, ProductDefName)
            + (string.IsNullOrEmpty(StateId) ? "" : ":" + StateId);
        public bool IgnoreSowing = true;
        public bool IgnoreHarvest = true;
        public int Lower = 500;
        public int Upper = 1500;
        // 输入缓冲只在设置界面使用，不序列化；保留输入中的暂时无效文本。
        [NonSerialized] public string LowerBuffer;
        [NonSerialized] public string UpperBuffer;

        /// <summary>保存植物键、忽略开关及阈值；读取设置时不依赖 DefDatabase 已完成加载。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref PlantDefName, "plantDefName");
            Scribe_Values.Look(ref ProductDefName, "productDefName");
            Scribe_Values.Look(ref Source, "source", ProductLinkSource.Unknown);
            Scribe_Values.Look(ref StateId, "stateId");
                        // 只读旧字段用于迁移；新字段显式写入，允许两个方向独立且不会被旧值覆盖。
            bool legacyIgnore = true;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyIgnore, "ignore", true);
            Scribe_Values.Look(ref IgnoreSowing, "ignoreSowing", legacyIgnore, forceSave: true);
            Scribe_Values.Look(ref IgnoreHarvest, "ignoreHarvest", legacyIgnore, forceSave: true);
            Scribe_Values.Look(ref Lower, "lower", 500);
            Scribe_Values.Look(ref Upper, "upper", 1500);
        }
    }

    /// <summary>游戏 Mod 设置；控制开关即时生效，阈值和新规则在下一个 600-tick 检查生效。</summary>
    public sealed class FarmingSettings : ModSettings
    {
        public bool Enabled = true;
        public bool ControlSowing = true;
        public bool ControlHarvest;
        public bool HideSowingIcon;
        public bool HideHarvestIcon;
        public List<CropRule> Rules = new List<CropRule>();
        private readonly Dictionary<string, CropRule> byName = new Dictionary<string, CropRule>();
        public int Revision { get; private set; }

        /// <summary>保存全局开关和逐植物配置；缺失的模组植物规则保留，重新启用模组后可继续使用。</summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref ControlSowing, "controlSowing", true);
            Scribe_Values.Look(ref ControlHarvest, "controlHarvest", false);
            Scribe_Values.Look(ref HideSowingIcon, "hideSowingIcon", false);
            Scribe_Values.Look(ref HideHarvestIcon, "hideHarvestIcon", false);
            Scribe_Collections.Look(ref Rules, "rules", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Rules == null) Rules = new List<CropRule>();
                Reindex();
            }
        }

        /// <summary>重建设置索引并剔除损坏／重复键；只在启动或读取设置时运行。</summary>
        public void Reindex()
        {
            byName.Clear();
            for (int i = Rules.Count - 1; i >= 0; i--)
            {
                CropRule rule = Rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.PlantDefName) || byName.ContainsKey(Key(rule.PlantDefName, rule.ProductDefName)))
                {
                    Rules.RemoveAt(i);
                    continue;
                }
                byName.Add(Key(rule.PlantDefName, rule.ProductDefName), rule);
            }
            Changed();
        }

        /// <summary>返回指定植物的独立规则；新发现植物默认忽略，避免对新产物误用阈值。</summary>
        public CropRule GetOrCreate(string plantDefName, string productDefName, bool primary = false)
        {
            string key = Key(plantDefName, productDefName);
            CropRule rule;
            if (byName.TryGetValue(key, out rule)) return rule;
            string legacyKey = Key(plantDefName, null);
            if (primary && byName.TryGetValue(legacyKey, out rule))
            {
                // 旧版本的逐作物配置仅迁移到主产物，不复制到新增副产物。
                byName.Remove(legacyKey);
                rule.ProductDefName = productDefName;
            }
            else
            {
                rule = new CropRule { PlantDefName = plantDefName, ProductDefName = productDefName };
                Rules.Add(rule);
            }
            byName.Add(key, rule);
            Changed();
            return rule;
        }

        /// <summary>仅删除索引中同一个已证实手工创建的规则；旧运行缓存立即通过 Ignore 放行。</summary>
        internal bool RemoveManual(CropRule rule)
        {
            if (rule == null || rule.Source != ProductLinkSource.Manual) return false;
            string key = Key(rule.PlantDefName, rule.ProductDefName);
            CropRule current;
            if (!byName.TryGetValue(key, out current) || !ReferenceEquals(current, rule)) return false;
            rule.IgnoreSowing = true;
            rule.IgnoreHarvest = true;
            byName.Remove(key);
            Rules.Remove(rule);
            Changed();
            return true;
        }
        /// <summary>使用植物名称长度前缀生成无歧义的作物＋产物持久化键。</summary>
        public static string Key(string plant, string product)
        {
            return plant.Length + ":" + plant + (product ?? "");
        }
        /// <summary>使各地图在下次检查时重建小型规则缓存；不直接扫描库存。</summary>
        public void Changed()
        {
            unchecked { Revision++; }
        }
    }
}



