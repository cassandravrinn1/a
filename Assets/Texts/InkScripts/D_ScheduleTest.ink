// D_ScheduleTest.ink
-> start
=== start ===
【D_ScheduleTest / start】测试 schedule 行为。

+ 正常：1 分钟后回调本文件
    我去处理一件事，1 分钟后回来。
    #cmd: schedule followup_ok 1m
    -> END

+ 陷阱：schedule 后立刻切换 Ink
    2 分钟后应该触发 followup_trap，但我会先切走。
    #cmd: schedule followup_trap 2m
    #cmd: switch_ink HealthTest start
    -> END

=== followup_ok ===
【D_ScheduleTest】到点回调成功（仍在本文件）。
#cmd: switch_ink MainTest start
-> END

=== followup_trap ===
【D_ScheduleTest】如果你看到这句，说明触发时你又回到了本文件。
#cmd: switch_ink MainTest start
-> END
