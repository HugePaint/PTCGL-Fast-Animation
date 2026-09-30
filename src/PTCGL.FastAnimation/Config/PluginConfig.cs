using BepInEx.Configuration;
using UnityEngine;

namespace PTCGL.FastAnimation.Config
{
    internal sealed class PluginConfig
    {
        public ConfigEntry<float> SpeedMultiplier { get; }
        public ConfigEntry<bool> SkipAnimations { get; }
        public ConfigEntry<bool> SpeedBoosterCards { get; }
        public ConfigEntry<bool> SpeedRewardPopups { get; }
        public ConfigEntry<bool> SpeedMissionRewards { get; }
        public ConfigEntry<bool> SpeedCurrencyFlight { get; }
        public ConfigEntry<bool> EnableProbe { get; }
        public ConfigEntry<bool> LogEntryPoints { get; }
        public ConfigEntry<bool> LogAnimatorClips { get; }
        public ConfigEntry<float> SampleSeconds { get; }
        public ConfigEntry<KeyboardShortcut> DumpHotkey { get; }

        public PluginConfig(ConfigFile config)
        {
            SpeedMultiplier = config.Bind(
                "Speed",
                "Multiplier",
                2f,
                "Playback multiplier for the categories below. Clamped to 1-8. Ignored while Skip is true. 1 keeps the original speed.");

            SkipAnimations = config.Bind(
                "Speed",
                "Skip",
                false,
                "Skip the enabled categories instead of speeding them up. Completion callbacks still run, so pack opens and reward grants continue. The revealed card stays visible until you dismiss it.");

            SpeedBoosterCards = config.Bind(
                "Speed",
                "BoosterCards",
                true,
                "Speed booster card Reveal and Outro, including the slide/scale that plays with them. Idle after the reveal stays at normal speed.");

            SpeedRewardPopups = config.Bind(
                "Speed",
                "RewardPopups",
                true,
                "Speed ContentItem reward popup intro and outro (currency, tokens, boosters, Build and Battle pack layer).");

            SpeedMissionRewards = config.Bind(
                "Speed",
                "MissionRewards",
                true,
                "Speed or skip the dex research-mission claim flourish: completing checkmark and the set-complete sequence.");

            SpeedCurrencyFlight = config.Bind(
                "Speed",
                "CurrencyFlight",
                true,
                "Speed or skip currency icons flying to the top bar.");

            EnableProbe = config.Bind(
                "Probe",
                "Enabled",
                false,
                "Log animation entry points and newly seen Animator clips. Does not change playback speed.");

            LogEntryPoints = config.Bind(
                "Probe",
                "LogEntryPoints",
                true,
                "Postfix known presentation methods (coin flip, pack open, card move, match outro, and similar).");

            LogAnimatorClips = config.Bind(
                "Probe",
                "LogAnimatorClips",
                true,
                "Sample active Animators and log each new scene/controller/clip once.");

            SampleSeconds = config.Bind(
                "Probe",
                "SampleSeconds",
                2f,
                "Seconds between Animator samples.");

            DumpHotkey = config.Bind(
                "Probe",
                "DumpHotkey",
                new KeyboardShortcut(KeyCode.F6),
                "Write the session animation catalog to BepInEx\\LogOutput.log. Avoid F8 (CodeRedeem debug menu).");
        }
    }
}
