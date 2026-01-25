// B_HealthTest.ink
->start
=== start ===
【B_HealthTest / start】发送一组健康相关命令。

#cmd: hs_event AddFatigue 0.25
#cmd: hs_activity HardWork
#cmd: hs_sleep false

+ [让她休息]
    #cmd: hs_sleep true
    #cmd: hs_activity Rest
    -> finish

+ [继续工作]
    #cmd: hs_activity HardWork
    #cmd: hs_event AddFatigue 0.4
    -> finish

=== finish ===
【B_HealthTest】健康测试完成，返回主入口。
#cmd: switch_ink MainTest start
-> END
