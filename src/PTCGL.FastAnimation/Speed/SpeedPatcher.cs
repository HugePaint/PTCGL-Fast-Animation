using System;
using System.Reflection;
using HarmonyLib;

namespace PTCGL.FastAnimation.Speed
{
    internal static class SpeedPatcher
    {
        public static int Apply(Harmony harmony, Action<string> log)
        {
            var patched = 0;
            patched += Try(harmony, log, "ContentItem3DAsset_Booster", "HandleBoosterOpening", nameof(BoosterPatches.ScaleOpeningDurations), null);
            patched += Try(harmony, log, "ContentItem3DAsset_Booster", "RevealNextCard", nameof(BoosterPatches.ScaleOpeningDurations), null);
            patched += Try(harmony, log, "ContentItem3DAsset_BoosterCard", "SetRevealTrigger", null, nameof(BoosterPatches.RevealStarted));
            patched += Try(harmony, log, "ContentItem3DAsset_BoosterCard", "OnRevealComplete", null, nameof(BoosterPatches.RevealFinished));
            patched += Try(harmony, log, "ContentItem3DAsset_BoosterCard", "SetOutroTrigger", null, nameof(BoosterPatches.OutroTriggered));
            patched += Try(harmony, log, "ContentItem3DBoosterEffects", "TriggerOutro", null, nameof(BoosterPatches.ParticleOutro));
            patched += PatchRevealWait(harmony, log);

            patched += Try(harmony, log, "ContentItemSequenceAnimation", "AnimateIn", nameof(RewardPatches.ScaleIntro), null);
            patched += Try(harmony, log, "ContentItemSequenceAnimation", "AnimateOut", nameof(RewardPatches.ScaleIntro), null);
            patched += Try(harmony, log, "MoveToWithCurveAnim", "Play", nameof(CurrencyPatches.ScaleFlight), nameof(CurrencyPatches.FinishFlight));

            patched += Try(harmony, log, "ResearchMissionsListObject", "PlayCheckmarkAnimation", nameof(MissionPatches.Checkmark), nameof(MissionPatches.CheckmarkSkipped));
            patched += Try(harmony, log, "ResearchMissionsListObject", "OnCheckmarkAnimationComplete", null, nameof(MissionPatches.CheckmarkFinished));
            patched += Try(harmony, log, "CardDexSetCompleteAnimationListener", "PlaySetCompleteAnimation", nameof(MissionPatches.SetCompleteStarted), null);
            patched += Try(harmony, log, "CardDexSetCompleteController", "AnimationComplete", null, nameof(MissionPatches.SetCompleteFinished));
            return patched;
        }

        private static int PatchRevealWait(Harmony harmony, Action<string> log)
        {
            var outer = AccessTools.TypeByName("ContentItem3DAsset_BoosterCard");
            if (outer == null)
            {
                log("[Speed] skip missing type ContentItem3DAsset_BoosterCard");
                return 0;
            }

            Type? nested = null;
            var nestedTypes = outer.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < nestedTypes.Length; i++)
            {
                if (nestedTypes[i].Name.IndexOf("WaitForRevealThenOutro", StringComparison.Ordinal) >= 0)
                {
                    nested = nestedTypes[i];
                    break;
                }
            }

            if (nested == null)
            {
                log("[Speed] skip missing WaitForRevealThenOutro");
                return 0;
            }

            return TryMethod(harmony, log, AccessTools.Method(nested, "MoveNext"), null, nameof(BoosterPatches.OutroAfterReveal), "WaitForRevealThenOutro.MoveNext");
        }

        private static int Try(Harmony harmony, Action<string> log, string typeName, string methodName, string? prefixName, string? postfixName)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                log($"[Speed] skip missing type {typeName}");
                return 0;
            }

            var method = AccessTools.DeclaredMethod(type, methodName);
            if (method == null)
            {
                log($"[Speed] skip missing method {typeName}.{methodName}");
                return 0;
            }

            return TryMethod(harmony, log, method, prefixName, postfixName, type.Name + "." + method.Name);
        }

        private static int TryMethod(Harmony harmony, Action<string> log, MethodInfo? method, string? prefixName, string? postfixName, string label)
        {
            if (method == null)
            {
                log($"[Speed] skip missing method {label}");
                return 0;
            }

            try
            {
                harmony.Patch(
                    method,
                    prefix: prefixName == null ? null : new HarmonyMethod(Resolve(prefixName)),
                    postfix: postfixName == null ? null : new HarmonyMethod(Resolve(postfixName)));
                log($"[Speed] patched {label}");
                return 1;
            }
            catch (Exception ex)
            {
                log($"[Speed] failed {label}: {ex.GetType().Name} {ex.Message}");
                return 0;
            }
        }

        private static MethodInfo Resolve(string name)
        {
            var candidates = new[] { typeof(BoosterPatches), typeof(RewardPatches), typeof(CurrencyPatches), typeof(MissionPatches) };
            for (var i = 0; i < candidates.Length; i++)
            {
                var method = candidates[i].GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                    return method;
            }

            throw new MissingMethodException(name);
        }
    }
}
