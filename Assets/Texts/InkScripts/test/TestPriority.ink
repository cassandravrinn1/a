-> start

=== start ===
苏拉米斯：选择测试：
+ [Test1：强制唤醒] -> test1
+ [Test2：睡眠期间普通消息排队，醒来后吐出] -> test2
+ [Test3:] ->test3
// --------------------
// Test1（你已验证通过）
// --------------------
=== test1 ===
苏拉米斯：Test1：我将睡觉，20秒后强制醒来。
#cmd: hs_sleep true
#cmd: schedule_to TestPriority wake_up 20s p=100 f=true
-> sleep_hold

=== sleep_hold ===
苏拉米斯：（睡眠中……等待 wake_up）
+ [（保持等待，不操作）] -> sleep_hold

=== wake_up ===
#cmd: hs_sleep false
苏拉米斯：醒了（应当必现）。
-> END

=== END ===
苏拉米斯：结束。
+ [回到 start] -> start


// --------------------
// Test2：排队 + 醒来吐队列
// --------------------
=== test2 ===
苏拉米斯：Test2：我将睡觉，同时安排一条普通消息 normal_queued（p=20 f=false）。
苏拉米斯：它应排队，等我醒来后再出现。
#cmd: hs_sleep true
#cmd: schedule_to TestPriority normal_queued 8s p=20
#cmd: schedule_to TestPriority wake_up_2 12s p=100 f=true
-> sleep_hold_2

=== sleep_hold_2 ===
苏拉米斯：（睡眠中……等待 wake_up_2）
+ [（保持等待，不操作）] -> sleep_hold_2

=== wake_up_2 ===
#cmd: hs_sleep false
苏拉米斯：醒了（Test2）。接下来应该出现 normal_queued（可能紧跟着或稍后）。
-> END

=== normal_queued ===
苏拉米斯：normal_queued 出现 ✅（说明排队→吐队列成功）
-> END

// ===================================================
// Test3：多条排队消息的优先级排序（p 高者先出）
// ===================================================

=== test3 ===
苏拉米斯：Test3：优先级排序测试。
苏拉米斯：我将进入睡眠，同时安排三条普通消息（不同 p），以及一条强制唤醒。
苏拉米斯：醒来后，**应按优先级从高到低依次出现**。

#cmd: hs_sleep true

// 三条普通排队消息（不同优先级，全部 f=false）
#cmd: schedule_to TestPriority msg_low    5s  p=10
#cmd: schedule_to TestPriority msg_mid    6s  p=30
#cmd: schedule_to TestPriority msg_high   7s  p=60

// 强制唤醒（最高优先级 + 强制）
#cmd: schedule_to TestPriority wake_up_3  12s p=100 f=true

-> sleep_hold_3


=== sleep_hold_3 ===
苏拉米斯：（睡眠中……等待 wake_up_3）
+ [（保持等待，不操作）] -> sleep_hold_3


=== wake_up_3 ===
#cmd: hs_sleep false
苏拉米斯：醒了（Test3）。
苏拉米斯：接下来应当按顺序出现：
苏拉米斯：msg_high → msg_mid → msg_low
-> END


// --------------------
// 排队消息（按预期顺序）
// --------------------

=== msg_high ===
苏拉米斯：msg_high 出现（p=60）✅
-> END

=== msg_mid ===
苏拉米斯：msg_mid 出现（p=30）✅
-> END

=== msg_low ===
苏拉米斯：msg_low 出现（p=10）✅
-> END

