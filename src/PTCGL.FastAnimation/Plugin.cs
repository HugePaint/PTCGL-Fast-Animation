using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PTCGL.FastAnimation.Compat;
using PTCGL.FastAnimation.Config;
using PTCGL.FastAnimation.Probe;
using PTCGL.FastAnimation.Speed;
using UnityEngine;

namespace PTCGL.FastAnimation
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.ptcgl.fastanimation";
        public const string PluginName = "PTCGL.FastAnimation";
        public const string PluginVersion = "0.3.0";

        internal static ManualLogSource Log = null!;
        internal static PluginConfig Cfg = null!;

        private void Awake()
        {
            Log = Logger;
            Cfg = new PluginConfig(Config);
            AnimationCatalog.Reset();

            var multiplier = Playback.Multiplier;
            Log.LogInfo($"{PluginName} {PluginVersion} awake. Speed={multiplier} Skip={Cfg.SkipAnimations.Value} Booster={Cfg.SpeedBoosterCards.Value} Reward={Cfg.SpeedRewardPopups.Value} Mission={Cfg.SpeedMissionRewards.Value} Currency={Cfg.SpeedCurrencyFlight.Value} Probe={Cfg.EnableProbe.Value}.");
            if (Cfg.SpeedMultiplier.Value > Playback.MaxMultiplier)
                Log.LogWarning($"[Speed] multiplier {Cfg.SpeedMultiplier.Value} capped at {Playback.MaxMultiplier} so Animator events still fire.");
            Log.LogInfo("[Compat] Harmony id com.ptcgl.fastanimation. Does not patch FastStartup, CodeRedeem, Doorstop, or coupon APIs.");

            var harmony = new Harmony(PluginGuid);
            var speedPatches = SpeedPatcher.Apply(harmony, msg => Log.LogInfo(msg));
            Log.LogInfo($"[Speed] patches={speedPatches}.");

            if (Cfg.EnableProbe.Value)
            {
                var patched = SoftPatcher.Apply(harmony, msg => Log.LogInfo(msg));
                Log.LogInfo($"[Probe] entry patches={patched}. Dump hotkey={Cfg.DumpHotkey.Value}.");
                gameObject.AddComponent<ProbeBehaviour>();
            }
        }
    }

    internal sealed class ProbeBehaviour : MonoBehaviour
    {
        private float _nextSample;

        private void Update()
        {
            if (Plugin.Cfg == null || !Plugin.Cfg.EnableProbe.Value)
                return;

            if (Plugin.Cfg.DumpHotkey.Value.IsDown())
                AnimationCatalog.Dump("hotkey");

            if (Time.unscaledTime < _nextSample)
                return;

            _nextSample = Time.unscaledTime + Mathf.Max(0.5f, Plugin.Cfg.SampleSeconds.Value);
            AnimationCatalog.SampleAnimators();
        }
    }
}
