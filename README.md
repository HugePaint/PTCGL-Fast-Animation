# PTCGL-Fast-Animation

BepInEx 5 插件，目标是给 Pokémon TCG Live 的表现层动画做倍率或跳过。当前版本 **0.1.0 只记录动画，不改播放速度**。

客户端核对：Unity `6000.3.5f2`，BepInEx `5.4.23.5`。游戏目录里已经有 FastStartup（Doorstop 入口）和 `PTCGL.CodeRedeemMOD`（`com.ptcgl.coderedeemmod`，调试键默认 F8）。本插件的 Harmony id 是 `com.ptcgl.fastanimation`，只挂在 `BepInEx\plugins`，不改 `doorstop_config.ini`，也不 patch FastStartup、兑换码或 `ManagerBase`。

## 游戏里的动画从哪来

表现播放集中在 `TPCI.RainierClient.dll`。`MatchLogic.dll` 里的 `CoinFlip`、`VFXEffect`、`ActionVFX` 是规则数据，不是播放器。Unity Timeline / Playables 在这些程序集里没有调用。

实际在播的有四条：

Unity **Animator** 是主路径，RainierClient 里大约一百多个类型会碰到 `Animator`。对局里能对上的包括 `CoinFlipOverlay`（硬币）、`OpenCardsView`（开包）、`ContentItem3DAsset_Booster`（卡牌揭示）、`BattleOutro`（胜负，已有 `SkipToSequenceEnd`）、`Card3DViewController`（卡面切换）、`CardMatchVfxAnimController`（攻击结果特效）、`Board3dViewController`（击倒 / 回合指示）、`VersusScreenController`、`FrontEndLoadingAnimManager`、`DialogueManager`、`RankedAnimController`。

**DOTween**（`DG.Tweening`）大约二十来个调用方，用来做位移而不是 Animator clip。卡牌走位在 `CardAnimTimed`、`CardAnimDelay`、`MoveToWithCurveAnim`，手牌布局在 `HandFanCollapsedLayout` / `HandFanExpandedLayout`。

**AnimationClip / legacy Animation** 走 `CardAnimClip` 和 `BenchSlot.flipClip`。主菜单镜头还有 **PathMagic**（`CardAnimPath`、`MainMenu3DController`）。登录界面另有一套 `TPCI.PTCS` 的 `CanvasGroupTweener` / `RectTransformTweener`。

全局 `Time.timeScale` 不适合当加速开关：`ActionTimer`、`MatchClockAnimationTrigger` 和回合计时跟动画挤在同一套时间里。后面做倍率时只动表现播放器（`Animator.speed`、DOTween、卡牌 `durationInSeconds`），并单独放过时钟。

## 探针怎么记

进游戏后看 `BepInEx\LogOutput.log`，搜 `[Anim]` 和 `[Probe]`。

第一次看到某个入口或某条 clip 会立刻写一行。同一条再出现只加次数。按 **F6** 把本局目录打全（次数在行首 `xN`）。F8 仍是兑换码 mod，不要改。

配置在 `BepInEx\config\com.ptcgl.fastanimation.cfg`：`Probe.Enabled`、`LogEntryPoints`、`LogAnimatorClips`、`SampleSeconds`、`DumpHotkey`。

请跑一遍这些画面，然后把含 `[Anim]` 的日志留下：

1. 主菜单停几秒（加载动画、导航）
2. 开一个补充包，让卡牌翻完
3. 打一局：对战登场、硬币、出牌、攻击、击倒、结算
4. 如果方便，再看一次段位经验或任务奖励弹出

入口方法对不上的类型（例如只在 Animator 上播、没有独立 `Play` 方法）会靠 clip 采样补上。`[Probe] skip missing` 表示这个版本客户端没有该方法，可以忽略。

## 编译

需要 .NET SDK（`net472`）。Managed 目录按这个顺序找：环境变量 `PTCGL_GAME_ROOT`，然后 `Directory.Build.user.props`（从 example 复制，已 gitignore），然后本机常见安装路径。

```powershell
dotnet build src/PTCGL.FastAnimation/PTCGL.FastAnimation.csproj -c Release -p:CopyPluginToGame=true
```

DLL 会复制到游戏的 `BepInEx\plugins\PTCGL.FastAnimation.dll`。游戏正在运行时文件会被锁住，先退出再编译。重启游戏后，日志里应出现 `PTCGL.FastAnimation 0.1.0 awake`，且兑换码 mod 仍是 `Chainloader` 加载的另一个插件。

## License

本仓库源码使用 [MIT License](LICENSE)。许可证只覆盖这里的插件代码，不包含游戏本体、资源或 The Pokémon Company 的商标。
