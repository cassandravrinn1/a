using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ProjectSulamith.Systems;

public class SceneAutoBuilder : MonoBehaviour
{
    [MenuItem("Tools/Build Personality Test Scene")]
    public static void BuildScene()
    {
        // === Canvas ===
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // === UIManager + EmotionBinder ===
        GameObject uiManager = new GameObject("UIManager");
        EmotionBinder binder = uiManager.AddComponent<EmotionBinder>();

        // === Emotion Sliders (Left Side) ===
        CreateEmotionSlider(canvasGO.transform, "Hope", new Vector2(-800, 300));
        CreateEmotionSlider(canvasGO.transform, "Happiness", new Vector2(-800, 240));
        CreateEmotionSlider(canvasGO.transform, "Trust", new Vector2(-800, 180));
        CreateEmotionSlider(canvasGO.transform, "Affinity", new Vector2(-800, 120));

        // === Emotion Text Display ===
        CreateInfoText(canvasGO.transform, "EmotionValuesText", new Vector2(-800, 50));

        // === Buttons (Right Side) ===
        CreateButton(canvasGO.transform, "StartStoryButton", "Start Story", new Vector2(700, 300));
        CreateButton(canvasGO.transform, "TriggerComfortEvent", "Trigger Comfort", new Vector2(700, 240));
        CreateButton(canvasGO.transform, "TriggerIgnoreEvent", "Trigger Ignore", new Vector2(700, 180));
        CreateButton(canvasGO.transform, "ReduceHealthButton", "Reduce Health", new Vector2(700, 120));
        CreateButton(canvasGO.transform, "SetDeadButton", "Set isAlive = false", new Vector2(700, 60));

        // === Scroll Log Output ===
        CreateLogPanel(canvasGO.transform, new Vector2(0, -250));

        // === GameManager and Core Systems ===
        GameObject gameManager = new GameObject("GameManager");
        gameManager.AddComponent<PersonalitySystem>();
        gameManager.AddComponent<HealthSystem>();
        gameManager.AddComponent<InkVariableBridge>();

        // === Ink Controller ===
        GameObject inkCtrl = new GameObject("InkController");
        inkCtrl.AddComponent<InkStoryController>();

        // === TimeManager ===
        GameObject timeManager = new GameObject("TimeManager");
        timeManager.AddComponent<TimeSystemDummy>();

        Debug.Log("Scene build complete. Now assign Ink JSON and connect sliders manually.");
    }

    private static void CreateEmotionSlider(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject sliderGO = new GameObject(name + "Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(parent);
        RectTransform rt = sliderGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 20);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        GameObject labelGO = new GameObject(name + "Label", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(sliderGO.transform);
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = name;
        label.fontSize = 18;
        label.alignment = TextAlignmentOptions.Left;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0.5f);
        labelRT.anchorMax = new Vector2(0, 0.5f);
        labelRT.pivot = new Vector2(0, 0.5f);
        labelRT.anchoredPosition = new Vector2(-80, 0);
    }

    private static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGO.transform.SetParent(parent);
        RectTransform rt = buttonGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        GameObject textGO = new GameObject("Text", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(buttonGO.transform);
        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 16;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
    }

    private static void CreateInfoText(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject textGO = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(parent);
        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 60);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "[Emotion Values]";
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontSize = 16;
    }

    private static void CreateLogPanel(Transform parent, Vector2 anchoredPos)
    {
        GameObject panel = new GameObject("LogPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(800, 200);
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = anchoredPos;

        GameObject logTextGO = new GameObject("LogText", typeof(TextMeshProUGUI));
        logTextGO.transform.SetParent(panel.transform);
        TextMeshProUGUI tmp = logTextGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "[Log output here...]";
        tmp.enableWordWrapping = true;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.fontSize = 14;

        RectTransform logRT = logTextGO.GetComponent<RectTransform>();
        logRT.anchorMin = new Vector2(0, 0);
        logRT.anchorMax = new Vector2(1, 1);
        logRT.offsetMin = new Vector2(10, 10);
        logRT.offsetMax = new Vector2(-10, -10);
    }
}

// Dummy TimeSystem class to avoid null errors
public class TimeSystemDummy : MonoBehaviour { }