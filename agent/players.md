# Animation players

表现播放集中在 `TPCI.RainierClient.dll`。`MatchLogic.dll` 里的 `CoinFlip`、`VFXEffect`、`ActionVFX` 是规则数据，不是播放器。这些程序集里没有 Unity Timeline / Playables 调用。

Unity Animator 是主路径。对局里能对上的包括 `CoinFlipOverlay`（硬币）、`OpenCardsView`（开包）、`ContentItem3DAsset_Booster`（卡牌揭示）、`BattleOutro`（胜负，已有 `SkipToSequenceEnd`）、`Card3DViewController`（卡面切换）、`CardMatchVfxAnimController`（攻击结果特效）、`Board3dViewController`（击倒 / 回合指示）、`VersusScreenController`、`FrontEndLoadingAnimManager`、`DialogueManager`、`RankedAnimController`。

DOTween（`DG.Tweening`）做位移。卡牌走位在 `CardAnimTimed`、`CardAnimDelay`、`MoveToWithCurveAnim`，手牌布局在 `HandFanCollapsedLayout` / `HandFanExpandedLayout`。

AnimationClip / legacy Animation 走 `CardAnimClip` 和 `BenchSlot.flipClip`。主菜单镜头还有 PathMagic（`CardAnimPath`、`MainMenu3DController`）。登录界面另有 `TPCI.PTCS` 的 `CanvasGroupTweener` / `RectTransformTweener`。

`ActionTimer`、`MatchClockAnimationTrigger` 和回合计时跟动画挤在同一套时间里，所以倍率只动表现播放器（`Animator.speed`、DOTween、卡牌 `durationInSeconds`），并放过时钟。

已接上的四类和它们的完成回调以 README「加速」为准。这份清单里其余入口是探针目录，缺了不影响加载。
