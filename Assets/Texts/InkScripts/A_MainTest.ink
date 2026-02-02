// A_MainTest.ink
// 主测试入口，所有 Ink 文件的统一入口规范：=== start ===
->start
=== start ===
【A_MainTest / start】连接建立。开始全功能自检。

#cmd: unknown_command foo bar

+ [进入健康系统命令测试（hs_*）]
    #cmd: switch_ink HealthTest start
    -> END

+ [进入人格系统命令测试（ps_event）]
    #cmd: switch_ink PersonalityTest start
    -> END

+ [进入延迟触发测试（schedule）]
    #cmd: switch_ink ScheduleTest start
    -> END

+ [进入压力测试（大量命令 + 截断）]
    #cmd: switch_ink StressTest start
    -> END

+ [结束测试]
    -> end

=== end ===
【A_MainTest / end】主测试结束。
-> END
