using BepInEx.Configuration;
using UnityEngine;

namespace PTCGL.FastAnimation.Config
{
    internal sealed class PluginConfig
    {
        public ConfigEntry<bool> EnableProbe { get; }
        public ConfigEntry<bool> LogEntryPoints { get; }
        public ConfigEntry<bool> LogAnimatorClips { get; }
        public ConfigEntry<float> SampleSeconds { get; }
        public ConfigEntry<KeyboardShortcut> DumpHotkey { get; }

        public PluginConfig(ConfigFile config)
        {
            EnableProbe = config.Bind(
                "Probe",
                "Enabled",
                true,
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
