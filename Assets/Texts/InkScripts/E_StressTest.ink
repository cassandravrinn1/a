// E_StressTest.ink
->start
=== start ===
【E_StressTest / start】大量命令压力测试。

#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01
#cmd: hs_event AddFatigue 0.01

#cmd: ps_event Player_Comfort 0.1
#cmd: ps_event Player_Comfort 0.1
#cmd: ps_event Player_Comfort 0.1
#cmd: ps_event Player_Comfort 0.1

#cmd: hs_activity HardWork
#cmd: hs_sleep false

【E_StressTest】如果你设置了 maxCommandsPerContinueStep，这里会看到截断警告。
#cmd: switch_ink MainTest start
-> END
