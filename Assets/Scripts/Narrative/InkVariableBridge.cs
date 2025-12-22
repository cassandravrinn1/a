using UnityEngine;
using Ink.Runtime;

/// <summary>
/// 单向同步：在开局或每次开始对话前，把当前人格/健康状态写进 Ink 变量。
/// </summary>
public class InkVariableBridge : MonoBehaviour
{
    public InkStoryController inkController;

    private Story Story => inkController != null ? inkController.Story : null;

    private void Awake()
    {
        if (inkController == null)
            inkController = GetComponent<InkStoryController>();
    }

    public void SyncGameToInk()
    {
        if (Story == null) return;

        // Personality -> Ink
        if (PersonalitySystem.Instance != null)
        {
            var emo = PersonalitySystem.Instance.Current;
            Story.variablesState["ps_hope"] = Mathf.RoundToInt(emo.Hope * 100);
            Story.variablesState["ps_trust"] = Mathf.RoundToInt(emo.Trust * 100);
            Story.variablesState["ps_guilt"] = Mathf.RoundToInt(PersonalitySystem.Instance.guilt * 100);
        }

        // Health -> Ink
        if (HealthSystem.Instance != null)
        {
            var h = HealthSystem.Instance.Snapshot;
            Story.variablesState["hs_vitality"] = Mathf.RoundToInt(h.Vitality * 100);
            Story.variablesState["hs_state"] = h.HealthStateTag;
        }
    }
}
