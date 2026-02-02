-> start

=== start ===
【延时系统测试开始】

我现在会在不同时间点再次联系你。
请不要做任何操作，只等待。

#cmd: schedule delay_1min 1m
#cmd: schedule delay_3min 3m
#cmd: schedule delay_5min 5m

（如果一切正常，你将分别在 1、3、5 分钟后看到新消息。）

-> END


=== delay_1min ===
【1 分钟延时触发】

已经过去 1 分钟了。
系统时间推进正常。

-> END


=== delay_3min ===
【3 分钟延时触发】

已经过去 3 分钟了。
如果你看到这条，说明多个延时任务可以并存。

-> END


=== delay_5min ===
【5 分钟延时触发】

已经过去 5 分钟。
延时调度测试完成。

-> END
