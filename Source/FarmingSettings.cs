using System;
using System.Collections.Generic;
using Verse;

namespace StopFarmingWhenReachLimit
{
    /// <summary>单种植物的全局配置。键是植物 defName，绝不是产物名称。</summary>
    public sealed class CropRule : IExposable
    {
        public string PlantDefName;
        public bool Ignore = true;
        public int Lower = 500;
        public int Upper = 1500;
        // 输入缓冲只在设置界面使用，不序列化；保留输入中的暂时无效文本。
        [NonSerialized] public string LowerBuffer;
        [NonSerialized] public string UpperBuffer;

        /// <summary>保存植物键、忽略开关及阈值；读取设置时不依赖 DefDatabase 已完成加载。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref PlantDefName, "plantDefName");
            Scribe_Values.Look(ref Ignore, "ignore", true);
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
        public List<CropRule> Rules = new List<CropRule>();
        private readonly Dictionary<string, CropRule> byName = new Dictionary<string, CropRule>();
        public int Revision { get; private set; }

        /// <summary>保存全局开关和逐植物配置；缺失的模组植物规则保留，重新启用模组后可继续使用。</summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref ControlSowing, "controlSowing", true);
            Scribe_Values.Look(ref ControlHarvest, "controlHarvest", false);
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
                if (rule == null || string.IsNullOrEmpty(rule.PlantDefName) || byName.ContainsKey(rule.PlantDefName))
                {
                    Rules.RemoveAt(i);
                    continue;
                }
                byName.Add(rule.PlantDefName, rule);
            }
            Changed();
        }

        /// <summary>返回指定植物的独立规则；新发现植物默认忽略，避免对新产物误用阈值。</summary>
        public CropRule GetOrCreate(string plantDefName)
        {
            CropRule rule;
            if (byName.TryGetValue(plantDefName, out rule)) return rule;
            rule = new CropRule { PlantDefName = plantDefName };
            Rules.Add(rule);
            byName.Add(plantDefName, rule);
            Changed();
            return rule;
        }

        /// <summary>使各地图在下次检查时重建小型规则缓存；不直接扫描库存。</summary>
        public void Changed()
        {
            unchecked { Revision++; }
        }
    }
}
