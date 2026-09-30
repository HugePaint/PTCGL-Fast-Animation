using System;
using System.Reflection;
using HarmonyLib;
using PTCGL.FastAnimation.Probe;

namespace PTCGL.FastAnimation.Compat
{
    /// <summary>
    /// Patch presentation entry points by name. A missing method is skipped so a game update cannot fail Awake.
    /// These targets are animation playback only. FastStartup, CodeRedeem, and coupon types are intentionally absent.
    /// </summary>
    internal static class SoftPatcher
    {
        private static readonly (string Type, string Method)[] Targets =
        {
            ("CoinFlipOverlay", "Open"),
            ("CoinFlipOverlay", "FlipAllCoins"),
            ("CoinFlipOverlay", "FlipCoin"),
            ("OpenCardsView", "OpenPack"),
            ("OpenCardsView", "AnimatePackEffects"),
            ("OpenCardsView", "RevealCard"),
            ("ContentItem3DAsset_Booster", "RevealNextCard"),
            ("ContentItemRewardController", "AnimateIn"),
            ("ContentItemRewardController", "AnimateOut"),
            ("ContentItemSequenceAnimation", "AnimateIn"),
            ("ContentItemSequenceAnimation", "AnimateOut"),
            ("BattleOutro", "ShowMatchResultOverlay"),
            ("BattleOutro", "SkipToSequenceEnd"),
            ("CardAnimClip", "Play"),
            ("CardAnimClip", "PlayAnimClip"),
            ("CardAnimDelay", "Play"),
            ("CardAnimPath", "Play"),
            ("CardAnimTimed", "Play"),
            ("MoveToWithCurveAnim", "Play"),
            ("Card3DViewController", "StartViewTransitionFX"),
            ("CardMatchVfxAnimController", "PlayCardMatchOutcomeVfx"),
            ("Board3dViewController", "PlayKoBoardEffect"),
            ("Board3dViewController", "PlayCenterBoardEffect"),
            ("Board3dViewController", "PlayPlayerBoardEffect"),
            ("Board3dViewController", "PlayOpponentBoardEffect"),
            ("BenchSlot", "Reveal"),
            ("RankedAnimController", "Play"),
            ("RankedAnimController", "AnimateToExp"),
            ("FrontEndLoadingAnimManager", "StartLoad"),
            ("MenuGroupAnimation", "Open"),
            ("MenuGroupAnimation", "OpenPage"),
            ("DialogueManager", "StartDialogue"),
            ("RpsResultDisplay", "UpdateResultText"),
            ("BuildAndBattleBoxOpeningPopup", "Open"),
            ("BuildAndBattleBoxOpeningPopup", "Show"),
            ("_Rainier.Features.CardDex.ResearchMissions.ResearchMissionAnimationController", "Play"),
        };

        public static int Apply(Harmony harmony, Action<string> log)
        {
            var postfix = new HarmonyMethod(typeof(AnimationCatalog).GetMethod(
                nameof(AnimationCatalog.EntryPostfix),
                BindingFlags.Public | BindingFlags.Static));

            var patched = 0;
            foreach (var (typeName, methodName) in Targets)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    log($"[Probe] skip missing type {typeName}");
                    continue;
                }

                var found = false;
                foreach (var method in AccessTools.GetDeclaredMethods(type))
                {
                    if (method.Name != methodName || method.IsAbstract)
                        continue;
                    found = true;
                    try
                    {
                        harmony.Patch(method, postfix: postfix);
                        patched++;
                        log($"[Probe] patched {type.Name}.{method.Name}");
                    }
                    catch (Exception ex)
                    {
                        log($"[Probe] failed {type.Name}.{method.Name}: {ex.GetType().Name} {ex.Message}");
                    }
                }

                if (!found)
                    log($"[Probe] skip missing method {type.Name}.{methodName}");
            }

            return patched;
        }
    }
}
