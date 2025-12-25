using UnityEngine;
using UnityEngine.UI;

public class PersonalityEventButtons : MonoBehaviour
{
    [Header("Controller References")]
    public InkStoryController inkController;

    [Header("Buttons")]
    public Button startStoryButton;
    public Button triggerComfortButton;
    public Button triggerIgnoreButton;
    public Button reduceGuiltEventButton;
    public Button setDeadButton;

    void Start()
    {
        if (startStoryButton)
            startStoryButton.onClick.AddListener(() =>
            {
                if (inkController != null)
                    inkController.StartStory("start");
            });

        if (triggerComfortButton)
            triggerComfortButton.onClick.AddListener(() =>
            {
                var e = new PersonalitySystem.PersonalityEvent
                {
                    Tag = PersonalitySystem.PersonalityEventTag.Player_Comfort,
                    Impact = 0.8f,
                    PlayerTone = 1f,
                    CampState = 0.5f,
                    DayNormalized = 0.3f,
                    TimeSinceLastContact = 0.2f,
                    Health = 0.9f,
                    Fatigue = 0.1f,
                    Stress = 0.1f
                };
                PersonalitySystem.Instance.RaiseEvent(e);
            });

        if (triggerIgnoreButton)
            triggerIgnoreButton.onClick.AddListener(() =>
            {
                var e = new PersonalitySystem.PersonalityEvent
                {
                    Tag = PersonalitySystem.PersonalityEventTag.Player_Ignorant,
                    Impact = 0.7f,
                    PlayerTone = -0.6f,
                    CampState = 0.6f,
                    DayNormalized = 0.4f,
                    TimeSinceLastContact = 0.8f,
                    Health = 0.8f,
                    Fatigue = 0.2f,
                    Stress = 0.3f
                };
                PersonalitySystem.Instance.RaiseEvent(e);
            });

        if (reduceGuiltEventButton)
            reduceGuiltEventButton.onClick.AddListener(() =>
            {
                var e = new PersonalitySystem.PersonalityEvent
                {
                    Tag = PersonalitySystem.PersonalityEventTag.Camp_LossDueToDecision_Player,
                    Impact = 1f,
                    Health = 0.3f,
                    Fatigue = 0.8f,
                    Stress = 0.7f
                };
                PersonalitySystem.Instance.RaiseEvent(e);
            });

        if (setDeadButton)
            setDeadButton.onClick.AddListener(() =>
            {
                PersonalitySystem.Instance?.SetAlive(false);
            });
    }
}