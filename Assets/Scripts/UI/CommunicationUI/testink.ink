VAR trust = 50
VAR mood = "neutral"
->start

=== start ===

SULAMIS: （系统测试启动……）
SULAMIS: 如果你看到这句话，说明对白显示正常。

现在，将测试第一组选项：

+ 我能听到
    ~ trust += 5
    ~ mood = "relieved"
    SULAMIS: 太好了，我还担心你连不上系统。
    -> continue_1

+ 你说话好像有点卡
    ~ trust -= 5
    ~ mood = "worried"
    SULAMIS: 嗯？我卡？那应该是终端延迟，不是我。
    -> continue_1

+ 快点开始测试吧
    ~ trust -= 10
    ~ mood = "annoyed"
    SULAMIS: （皱眉）……看来你不太耐心。
    -> continue_1


=== continue_1 ===

SULAMIS: 接下来测试 **点击选项不会重复输出**。

+ 这句话如果你没有重复说，说明过滤成功
    SULAMIS: 检查中……如果你没看到我重复上一句，那说明修复成功。
    -> conditions_test


=== conditions_test ===

SULAMIS: 好，我们来测试变量条件。

{ trust > 60:
    SULAMIS: 你对我挺好的，我能感觉到。
- else:
    SULAMIS: 嗯……我们的信任还需要一点时间。
}

SULAMIS: 当前状态：trust = {trust}, mood = {mood}。

SULAMIS: 测试嵌套分支。

+ 你现在心情如何？
    { 
    -mood == "relieved":
        SULAMIS: 我现在挺放松的。
    - mood == "worried":
        SULAMIS: 有点担心……怕系统掉线。
    - mood == "annoyed":
        SULAMIS: 你刚才的态度让我有点不开心。
    - else:
        SULAMIS: 说不上来。
    }
    -> loop_test

+ 我们继续吧
    SULAMIS: 嗯，我准备好了。
    -> loop_test

+ 给我你的状态报告
    SULAMIS: 状态正常，通讯稳定、心情取决于你的行为。
    -> loop_test


=== loop_test ===

SULAMIS: 测试循环逻辑与选项清理：

+ 再循环一次
    SULAMIS: 好的，我们继续循环。
    -> loop_test

+ 结束循环
    SULAMIS: 循环测试结束。
    -> end_test


=== end_test ===

SULAMIS: 所有功能测试完毕。
SULAMIS: 如果一路上没有重复文本、UI 未出错，那说明系统稳定。

-> END
