# PTCGL.FastAnimation

插件只放在 `BepInEx\plugins`。`doorstop_config.ini`、FastStartup 的启动补丁、兑换码的优惠券入口和 `ManagerBase` 留给另外两个 mod。

倍率只改局外表现播放器。`Time.timeScale` 和回合计时保持游戏原样。`Speed.Multiplier` 运行时限制在 1 到 8，再高会跳过 Animator 事件，开包流程会停住。`Speed.Skip` 为 true 时，已打开的类别跳到结束并照常走完成回调。

已经接上的类别是补充包翻牌、奖励弹窗、货币飞向顶栏、图鉴研究任务的勾选和套装完成。对战内、按钮反馈、加载转圈和待机保持原速。加一个新类别之前，读 [agent/players.md](agent/players.md)。

探针默认关闭。采样步骤在 README 的「探针怎么记」。目录导出键是 F6。F8 是兑换码 mod 的调试键。

游戏目录用 `PTCGL_GAME_ROOT`，否则用 `Directory.Build.user.props`，再否则用 `Directory.Build.props` 里的候选路径。文档不写绝对游戏路径。Release 编译带 `-p:CopyPluginToGame=true`，命令在 README。游戏进程开着时 DLL 会被锁住，先退出再复制。启动后 `BepInEx\LogOutput.log` 里有 `[Speed] patched`。
