// C_PersonalityTest.ink
->start
=== start ===
【C_PersonalityTest / start】测试人格事件。

#cmd: ps_event Player_Comfort 0.8
#cmd: ps_event NotARealTag 0.5

+ [低强度安抚]
    #cmd: ps_event Player_Comfort 0.2
    -> done

+ [高强度鼓励]
    #cmd: ps_event Player_Comfort 1.0
    -> done

=== done ===
【C_PersonalityTest】人格测试完成。
#cmd: switch_ink MainTest start
-> END
