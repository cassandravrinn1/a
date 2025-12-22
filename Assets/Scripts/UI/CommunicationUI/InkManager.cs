using System;
using System.Collections;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

namespace ProjectSulamith.Dialogue
{
    /// <summary>
    /// Ink 管理器：
    /// - 创建 Story；
    /// - 输出对白与选项；
    /// - 修复第一点击重复问题（过滤掉选项文本）
    /// </summary>
    public class InkManager : MonoBehaviour
    {
        [Header("Ink JSON")]
        public TextAsset inkJSONAsset;

        public Story StoryInstance { get; private set; }

        public event Action<string> OnLine;
        public event Action<List<Choice>> OnChoices;
        public event Action OnStoryEnded;

        private bool _initialized = false;
        private bool _isContinuing = false;

        /// ★ 新增：记录玩家刚刚选择的选项文本
        private string lastChoiceText = null;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;

            if (inkJSONAsset == null)
            {
                Debug.LogError("[InkManager] inkJSONAsset 未指定！");
                return;
            }

            StoryInstance = new Story(inkJSONAsset.text);
            _initialized = true;
            Debug.Log("[InkManager] 初始化完成。");
        }

        public void StartStory()
        {
            if (!_initialized) Initialize();
            if (StoryInstance == null) return;

            ContinueStory();
        }

        public void ContinueStory()
        {
            if (StoryInstance == null)
            {
                Debug.LogError("[InkManager] StoryInstance 为空！");
                return;
            }

            if (_isContinuing)
            {
                Debug.LogWarning("[InkManager] ContinueStory 重入已阻止。");
                return;
            }

            _isContinuing = true;
            try
            {
                while (StoryInstance.canContinue)
                {
                    string line = StoryInstance.Continue()?.Trim();

                    // ★ 修复重复 —— 如果 Ink 输出了“刚刚玩家选的选项文本”，则跳过
                    if (!string.IsNullOrEmpty(lastChoiceText) && line == lastChoiceText)
                    {
                        // Debug.Log($"[InkManager] 过滤掉重复行: {line}");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Debug.Log($"[InkManager] Line: {line}");
                        OnLine?.Invoke(line);
                    }
                }

                var choices = StoryInstance.currentChoices;
                if (choices != null && choices.Count > 0)
                {
                    OnChoices?.Invoke(new List<Choice>(choices));
                }
                else
                {
                    if (!StoryInstance.canContinue)
                    {
                        OnChoices?.Invoke(new List<Choice>());
                        OnStoryEnded?.Invoke();
                    }
                }
            }
            finally
            {
                _isContinuing = false;

                // ★ 一旦这一轮 ContinueStory 结束，清空 lastChoiceText
                lastChoiceText = null;
            }
        }


        /// <summary>
        /// 玩家选择某个选项
        /// </summary>
        public void ChooseOption(int index)
        {
            if (StoryInstance == null)
            {
                Debug.LogError("[InkManager] StoryInstance 为空！");
                return;
            }

            var choices = StoryInstance.currentChoices;
            if (choices == null || choices.Count == 0)
            {
                Debug.LogWarning("[InkManager] 当前没有可选选项。");
                return;
            }

            if (index < 0 || index >= choices.Count)
            {
                Debug.LogWarning($"[InkManager] 选项索引越界: {index}");
                return;
            }

            // ★ 记录这次玩家选择的文本（用于过滤）
            lastChoiceText = choices[index].text.Trim();

            StoryInstance.ChooseChoiceIndex(index);

            // 使用你原来的“延迟一帧”防止重入
            StartCoroutine(ContinueNextFrame());
        }

        private IEnumerator ContinueNextFrame()
        {
            yield return null;
            ContinueStory();
        }
    }
}
