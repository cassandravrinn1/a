VAR ps_hope = 0
VAR ps_trust = 0
VAR ps_guilt = 0
VAR hs_vitality = 0
VAR hs_state = "unknown"

=== start ===
苏拉米斯: 这里是 Personality / Health / Ink 联动测试场景。

当前状态（由 C# 注入）：
- Hope: {ps_hope}
- Trust: {ps_trust}
- Vitality: {hs_vitality}
- HealthState: {hs_state}

选择一个行为，看看数值是否变化：

* "温柔安慰她（正向事件）"
     ps_event("Player_Comfort", 0.8)
     hs_event("FullRest", 0.2)
     log("选择：安慰她")
    -> report

* "苛刻指责她（负向事件）"
     ps_event("Player_Harsh", 0.8)
     hs_event("Damage", 0.2)
     log("选择：苛责她")
    -> report

=== report ===
苏拉米斯: 事件已触发，请看 Unity 控制台和左上角调试面板的数值变化。

-> DONE
