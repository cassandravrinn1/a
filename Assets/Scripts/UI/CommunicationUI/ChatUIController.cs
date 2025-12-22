using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ink.Runtime;
using System.Collections.Generic;
using ProjectSulamith.Dialogue;
using System.Collections;

public class ChatUIController : MonoBehaviour
{
    [Header("Message Area")]
    public Transform messageContent;
    public GameObject npcRowPrefab;
    public GameObject playerRowPrefab;

    [Header("Option Area")]
    public Transform optionContent;
    public GameObject optionButtonPrefab;

    [Header("Scroll")]
    public ScrollRect scrollRect;

    [Header("Dialogue")]
    public InkManager inkManager;

    [Header("Typing Settings")]
    public float baseDelayK = 0.5f;
    public float perCharDelay = 0.06f;
    public float typingSpeedFactor = 1.0f;

    [Header("Typing Indicator")]
    public GameObject typingIndicatorPrefab;

    [Header("Typing Indicator (Position)")]
    public Transform typingIndicatorParent;

    private List<Choice> pendingChoices = null;

    private void Start()
    {
        if (inkManager != null)
        {
            inkManager.OnLine += HandleInkLine;
            inkManager.OnChoices += HandleInkChoices;
            inkManager.OnStoryEnded += HandleInkEnd;

            inkManager.StartStory();
        }
        else
        {
            Debug.LogError("[ChatUI] InkManager 未指定！");
        }
    }

    private void HandleInkLine(string line)
    {
        npcLineQueue.Enqueue(line);
        if (!isTyping)
            StartCoroutine(ProcessQueue());
    }


    private void HandleInkChoices(List<Choice> choices)
    {
        // 如果苏拉米斯正在打字，则先缓存选项
        if (isTyping)
        {
            pendingChoices = choices;
            return;
        }

        // 否则立即显示（和原来一样）
        ShowChoicesInternal(choices);
    }


    private void HandleInkEnd()
    {
        ClearOptions();
        npcLineQueue.Enqueue("……（通信结束）");
        if (!isTyping)
            StartCoroutine(ProcessQueue());
        
    }


    public void AddNpcMessage(string text)
    {
        GameObject row = Instantiate(npcRowPrefab, messageContent);

        var autoSize = row.GetComponentInChildren<ChatBubbleAutoSize>();
        if (autoSize != null) autoSize.SetText(text);
        else row.GetComponentInChildren<TextMeshProUGUI>().text = text;

        ScrollToBottom();
    }

    public void AddPlayerMessage(string text)
    {
        GameObject row = Instantiate(playerRowPrefab, messageContent);

        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(1f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(1f, 1f);

        var autoSize = row.GetComponentInChildren<ChatBubbleAutoSize>();
        if (autoSize != null) autoSize.SetText(text);
        else row.GetComponentInChildren<TextMeshProUGUI>().text = text;

        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null) return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ClearOptions()
    {
        for (int i = optionContent.childCount - 1; i >= 0; i--)
            Destroy(optionContent.GetChild(i).gameObject);
    }

    public void ShowOptions(string[] options, System.Action<int> onSelected)
    {
        ClearOptions();

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var go = Instantiate(optionButtonPrefab, optionContent);

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            var auto = go.GetComponent<OptionAutoSize>();

            if (auto != null)
            {
                auto.label = tmp;
                auto.SetText(options[i]);
            }
            else if (tmp != null)
                tmp.text = options[i];

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                onSelected?.Invoke(index);
            });
        }
    }
    private GameObject currentTypingIndicator;

    private void ShowTypingIndicator()
    {
        Debug.Log("ShowTypingIndicator() 被调用了");
        foreach (Transform child in typingIndicatorParent)
            Destroy(child.gameObject);

        currentTypingIndicator = Instantiate(typingIndicatorPrefab, typingIndicatorParent);

        

        var tmp = currentTypingIndicator.GetComponentInChildren<TextMeshProUGUI>();
        Debug.Log("TMP 是否为空：" + (tmp == null));
        Debug.Log("TMP 内容：" + tmp.text);

        if (tmp != null) tmp.text = "输入中…";
        Debug.Log("修改后 TMP 内容：" + tmp.text);
    }



    private void HideTypingIndicator()
    {
        if (currentTypingIndicator != null)
        {
            Destroy(currentTypingIndicator);
            currentTypingIndicator = null;
        }
    }
    private void ShowChoicesInternal(List<Choice> choices)
    {
        ClearOptions();

        string[] optionTexts = new string[choices.Count];
        for (int i = 0; i < choices.Count; i++)
            optionTexts[i] = choices[i].text;

        ShowOptions(optionTexts, index =>
        {
            AddPlayerMessage(optionTexts[index]);
            ClearOptions();
            inkManager.ChooseOption(index);
        });
    }

    private bool isTyping = false;

    private IEnumerator DisplayLineWithTypingDelay(string line)
    {
        isTyping = true;

        float delay = (baseDelayK + line.Length * perCharDelay) * typingSpeedFactor;
        float end = Time.unscaledTime + delay;

        ShowTypingIndicator();

        while (Time.unscaledTime < end)
            yield return null;

        HideTypingIndicator();
        AddNpcMessage(line);

        isTyping = false;

        // 如果玩家选项已经在打字时到来了，现在才显示
        if (pendingChoices != null)
        {
            ShowChoicesInternal(pendingChoices);
            pendingChoices = null;
        }

        yield break;
    }

    private Queue<string> npcLineQueue = new Queue<string>();
    

   
    private IEnumerator ProcessQueue()
    {
        isTyping = true;

        while (npcLineQueue.Count > 0)
        {
            string line = npcLineQueue.Dequeue();
            yield return StartCoroutine(DisplayLineWithTypingDelay(line));
        }

        isTyping = false;
    }





}
