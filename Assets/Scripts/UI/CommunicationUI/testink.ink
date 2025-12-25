// CmdTest.ink
// 用 tag 命令测试 InkManager 的命令解析：#cmd: ...
->start
=== start ===
进入系统测试对话。

我将执行一组“开局命令”，然后给你菜单继续触发。
#cmd: hs_activity Idle
#cmd: hs_sleep false

-> menu


=== menu ===
你要验证哪条命令？

+ [人格：安慰她（Player_Comfort, 0.8）]
    你轻声说了几句安慰的话。
    #cmd: ps_event Player_Comfort 0.8
    -> after

+ [人格：严厉（Player_Harsh, 0.8）]
    你的语气变得尖锐。
    #cmd: ps_event Player_Harsh 0.8
    -> after

+ [健康：造成伤害（Damage, 0.2）]
    你记录到一次身体损伤。
    #cmd: hs_event Damage 0.2
    -> after

+ [健康：增加疲劳（AddFatigue, 0.35）]
    她明显更疲惫了。
    #cmd: hs_event AddFatigue 0.35
    -> after

+ [健康：设置为高强度工作（HardWork）]
    接下来让她进入高强度工作状态（影响后续 Tick 消耗）。
    #cmd: hs_activity HardWork
    -> after

+ [健康：进入睡眠（true）]
    让她睡一会儿（sleep=true，会让 activity 变成 Resting）。
    #cmd: hs_sleep true
    -> after

+ [健康：醒来（false）]
    让她醒来（sleep=false）。
    #cmd: hs_sleep false
    -> after

+ [组合测试：过劳 + 疲劳 + 安慰]
    组合命令将被依次执行。
    #cmd: hs_activity HardWork
    #cmd: hs_event AddFatigue 0.45
    #cmd: ps_event Player_Comfort 0.7
    -> after

+ [结束]
    测试结束。
    -> END


=== after ===
（如果你的 InkManager 开启了 logCommands，你应当能在 Console 看到 [InkCmd] ...）

你可以继续再触发其他命令。
-> menu


=== END ===
-> DONE
