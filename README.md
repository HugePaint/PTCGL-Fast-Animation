# PTCGL-Fast-Animation

BepInEx 5 插件，给 Pokémon TCG Live 的局外表现动画做倍率或跳过。当前版本 **0.3.0** 覆盖四类：补充包翻牌的 Reveal / Outro、奖励弹窗的进入和离开、货币飞向顶栏、图鉴研究任务领取后的勾选和套装完成动画。对战内、按钮反馈、加载转圈和待机不改。Harmony id 是 `com.ptcgl.fastanimation`。和另外两个 mod 的边界见 `AGENTS.md`。播放器清单在 `agent/players.md`。

## 加速

倍率在 `BepInEx\config\com.ptcgl.fastanimation.cfg` 的 `Speed.Multiplier`，默认 **2**，运行时限制在 1 到 8。再高的话 Animator 事件可能被跳过，开包流程会停住。`Speed.Skip` 为 true 时，下面打开的类别直接跳到结束并照常走完成回调，不再使用倍率。翻开的卡牌会停在正面，直到你自己关掉。

- `Speed.BoosterCards`：卡牌 Reveal、Outro，以及跟它们一起播的滑入和缩放。翻开后的 Idle 保持原速。非最后一张的 Outro 会乘在游戏自己的快进速度上。
- `Speed.RewardPopups`：`ContentItemSequenceAnimation` 的进入和离开。货币、代币、补充包，以及 Build and Battle 的开包层都走这里。
- `Speed.CurrencyFlight`：货币图标飞向顶栏。只认层级里带 Currency 的 `MoveToWithCurveAnim`。
- `Speed.MissionRewards`：图鉴研究任务领取后的勾选动画，以及套装完成那条全屏序列。序列结束会把速度设回 1。跳过时套装完成序列不会播，直接继续后面的流程。

改配置后重启游戏。启动日志里应有 `Speed=2` 和 `[Speed] patched`。

## 探针怎么记

探针默认关闭。需要再采样时把 `Probe.Enabled` 设成 `true`。

进游戏后看 `BepInEx\LogOutput.log`，搜 `[Anim]` 和 `[Probe]`。

第一次看到某个入口或某条 clip 会立刻写一行。同一条再出现只加次数。按 **F6** 把本局目录打全（次数在行首 `xN`）。

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

DLL 会复制到游戏的 `BepInEx\plugins\PTCGL.FastAnimation.dll`。游戏正在运行时文件会被锁住，先退出再编译。重启游戏后，日志里应出现 `PTCGL.FastAnimation 0.3.0 awake`，且兑换码 mod 仍是 `Chainloader` 加载的另一个插件。

## License

本仓库源码使用 [MIT License](LICENSE)。许可证只覆盖这里的插件代码，不包含游戏本体、资源或 The Pokémon Company 的商标。
