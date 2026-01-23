// InterruptPlan_Test.ink
// 目的：测试“计划多段消息 + 玩家回复打断后续”
// 依赖命令：
//   #cmd: plan_arm <PlanId>
//   #cmd: schedule <knot> <delay> <planId> <cancelOnReply>
// 说明：
// - 如果玩家在第一段后立刻回复（ChooseOption），InkManager 会取消同 planId 且 cancelOnReply=true 的后续任务
// - 如果玩家不回复，系统会按时间继续推送后续段落
->start
=== start ===
【测试开始】我准备分三段告诉你情况。

第一段：我已经到达维修点，但这里的信号很不稳定。
#cmd: plan_arm Plan_A
#cmd: schedule second_part 10s Plan_A true
#cmd: schedule third_part 20s Plan_A true

（现在请你试着“立刻回复”，或者什么都不做等待 20 秒。）

+ [你先别急，慢慢说。]
    -> reply_calm
+ [注意安全，别冒险。]
    -> reply_safe
+ [所以说，你到底在修什么？]
    -> reply_question

=== reply_calm ===
好……我会稳住节奏。
（如果你回复得足够早，你将不会再收到“第二段/第三段”。）
-> END

=== reply_safe ===
明白。我会先确认退路，再继续。
（如果你回复得足够早，你将不会再收到“第二段/第三段”。）
-> END

=== reply_question ===
是电力节点。坏得比我想象中严重。
（如果你回复得足够早，你将不会再收到“第二段/第三段”。）
-> END

=== second_part ===
【第二段】我找到问题了：某条线路被低温冻裂，得临时改造接头。
（如果你在第一段就回复了，这段通常不会出现。）

-> END

=== third_part ===
【第三段】我开始动手了。可能会有几分钟完全没信号——如果你看到我突然沉默，不要担心。
（如果你在第一段就回复了，这段通常不会出现。）

-> END
