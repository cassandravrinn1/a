using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//using UnityEngine.UIElements;

public class KeyBindingManager : MonoBehaviour
{
    // 单例模式，方便全局访问
    public static KeyBindingManager Instance { get; private set; }

    [Header("UI引用")]
    public GameObject keyBindingPanel;

    [Header("预制体")]
    public GameObject keyBindingItemPrefab;
    public Transform contentParent; // Scroll View的Content

    [Header("当前状态")]
    public bool isRebinding = false;
    public string currentRebindingAction;
    public Button currentRebindingButton;

    [Header("恢复默认")]
    public Button resetToDefaultsButton;


    // 键位定义
     [System.Serializable]
    public class KeyBindingData
    {
        public string actionName;  //内部标识
        public string displayName; // 显示用，如"相机左移"
        public KeyCode defaultKey; //默认按键
        public string category;    //分类
    }
    // 存储所有键位项的引用
    private Dictionary<string, KeyBindingItem> keyItems = new Dictionary<string, KeyBindingItem>();

    // 存储原始默认键位（防止运行时修改defaultKey）
    private Dictionary<string, KeyCode> originalDefaults = new Dictionary<string, KeyCode>();
    //预设键位
    private KeyBindingData[] _keyBindings;
    public KeyBindingData[] keyBindings
    {
        get
        {
            if (_keyBindings == null || _keyBindings.Length == 0)
            {
                InitializeKeyBindingsData();
            }
            return _keyBindings;
        }
    }
    private void InitializeKeyBindingsData()
    {
        _keyBindings = new KeyBindingData[] 
        {
        new KeyBindingData { actionName = "MoveForward", displayName = "相机上移", defaultKey = KeyCode.W,category="镜头控制" },
        new KeyBindingData { actionName = "MoveBack", displayName = "相机下移", defaultKey = KeyCode.S,category="镜头控制"},
        new KeyBindingData { actionName = "MoveLeft", displayName = "相机左移", defaultKey = KeyCode.A,category="镜头控制" },
        new KeyBindingData { actionName = "MoveRight", displayName = "相机右移", defaultKey = KeyCode.D,category="镜头控制" },
        new KeyBindingData { actionName = "TurnLeft", displayName = "向左旋转", defaultKey = KeyCode.Q,category="镜头控制"},
        new KeyBindingData { actionName = "TurnRight", displayName = "向右旋转", defaultKey = KeyCode.E,category="镜头控制" },
        new KeyBindingData { actionName = "ZoomIn", displayName = "镜头推进", defaultKey = KeyCode.PageUp,category="镜头控制"},
        new KeyBindingData { actionName = "ZoomOut", displayName = "镜头推远", defaultKey = KeyCode.PageDown,category="镜头控制" },
        new KeyBindingData { actionName = "World", displayName = "切换到世界地图", defaultKey = KeyCode.Home,category="镜头控制" },
        new KeyBindingData { actionName = "Central", displayName = "相机中心", defaultKey = KeyCode.C,category="镜头控制" },

        new KeyBindingData { actionName = "Pause", displayName = "暂停", defaultKey = KeyCode.Space,category="时间控制" },
        new KeyBindingData { actionName = "TimeSpeedUp", displayName = "提高游戏速度", defaultKey = KeyCode.Equals,category="时间控制" },
        new KeyBindingData { actionName = "TimeSpeedDown", displayName = "降低游戏速度", defaultKey = KeyCode.Minus,category="时间控制" },
        new KeyBindingData { actionName = "TimeSpeed*1", displayName = "重设游戏速度", defaultKey = KeyCode.Alpha1,category="时间控制" },
        new KeyBindingData { actionName = "TimeSpeed*2", displayName = "快速", defaultKey = KeyCode.Alpha2,category="时间控制" },
        new KeyBindingData { actionName = "TimeSpeed*3", displayName = "高速", defaultKey = KeyCode.Alpha3,category="时间控制" },

        new KeyBindingData { actionName = "Build_OpenMenu", displayName = "建筑物面板", defaultKey = KeyCode.B,category="游戏控制" },
        new KeyBindingData { actionName = "Power_OpenMenu", displayName = "能量塔建设面板", defaultKey = KeyCode.G,category="游戏控制" },
        new KeyBindingData { actionName = "Economic", displayName = "经济面板", defaultKey = KeyCode.V,category="游戏控制"  },
        new KeyBindingData { actionName = "Law", displayName = "法典", defaultKey = KeyCode.L,category="游戏控制"  },
        new KeyBindingData { actionName = "Tree", displayName = "科技树", defaultKey = KeyCode.T,category="游戏控制"  },
        new KeyBindingData { actionName = "Temperature", displayName = "温度分布", defaultKey = KeyCode.O,category="游戏控制"  },
        new KeyBindingData { actionName = "Tutorial", displayName = "教程汇总", defaultKey = KeyCode.H,category="游戏控制"  },
        new KeyBindingData { actionName = "Efficiency", displayName = "效率跟踪", defaultKey = KeyCode.LeftShift,category="游戏控制"  },
        new KeyBindingData { actionName = "Previous", displayName = "选择下一个", defaultKey = KeyCode.RightBracket,category="游戏控制" },
        new KeyBindingData { actionName = "Next", displayName = "选择上一个", defaultKey = KeyCode.LeftBracket,category="游戏控制" },
        new KeyBindingData { actionName = "QuickSave", displayName = "快速保存", defaultKey = KeyCode.F5,category="游戏控制" },
        new KeyBindingData { actionName = "QuickLoad", displayName = "快速载入", defaultKey = KeyCode.F9,category="游戏控制" },
        };
    }


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        SaveOriginalDefaults();
        Debug.Log($"Awake: originalDefaults 已保存 {originalDefaults.Count} 个键位");
        foreach (var kvp in originalDefaults)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }



    void Start()
    {
        Debug.Log($"已加载{keyBindings.Length}个键位配置");
        InitializeKeyBindings();
        InitializeResetButtons();
    }
    #region 保存原始默认值
    private void SaveOriginalDefaults()
    {
        originalDefaults.Clear();
        foreach (KeyBindingData data in keyBindings)
        {
            originalDefaults[data.actionName] = data.defaultKey;
        }
    }
    #endregion
    #region 初始化重置按钮
    private void InitializeResetButtons()
    {
        if (resetToDefaultsButton != null)
        {
            resetToDefaultsButton.onClick.AddListener(ResetAllToDefault);
        }
    }
    #endregion
    // 初始化所有键位显示
    private void InitializeKeyBindings()
    {
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        keyItems.Clear();

        foreach (KeyBindingData data in keyBindings)
        {
            GameObject itemObj = Instantiate(keyBindingItemPrefab, contentParent);
            KeyBindingItem item = itemObj.GetComponent<KeyBindingItem>();

            if (item != null)
            {
                // 设置显示
                item.SetActionName(data.displayName);

                // 加载保存的键位或使用默认
                KeyCode savedKey = (KeyCode)PlayerPrefs.GetInt($"Key_{data.actionName}", (int)data.defaultKey);
                item.SetKeyCode(savedKey);

                // 绑定按钮事件
                item.keyButton.onClick.AddListener(() => OnKeyButtonClicked(data.actionName, item.keyButton));

                // 存储引用
                keyItems[data.actionName] = item;
            }
        }
    }

    // 按键被点击 - 开始重绑定
    public void OnKeyButtonClicked(string actionName, Button button)
    {
        if (isRebinding)
        {
            Debug.Log("正在等待其他按键输入...");
            return;
        }

        // 如果是同一个按键，可以取消
        if (currentRebindingAction == actionName)
        {
            CancelRebinding();
            return;
        }

        StartRebinding(actionName, button);
    }

    public void ResetAllToDefault()
    {
        Debug.Log($"=== 开始重置键位 ===");
        Debug.Log($"是否在重绑定中: {isRebinding}");
        Debug.Log($"键位总数: {keyBindings.Length}");
        Debug.Log($"originalDefaults 数量: {originalDefaults.Count}");


        if (isRebinding)
        {
            Debug.LogWarning("正在重绑定中，请先完成操作");
            CancelRebinding();
        }
        int resetCount = 0;
        foreach (KeyBindingData data in keyBindings)
        {
            Debug.Log($"正在重置: {data.actionName} (默认键: {data.defaultKey})");
            ResetSingleKeyBinding(data.actionName);
            resetCount++;
        }
        PlayerPrefs.Save();
        RefreshAllKeyDisplay();

        //此处可播放音效或显示提示
        Debug.Log($"重置完成，共处理 {resetCount} 个键位");
        Debug.Log("所有键位已重置");
    }
    //StartRebinding OnKeyButtonClickes
    public void ResetSingleKeyBinding(string actionName)
    {
        Debug.Log($"准备重置单个键位: {actionName}");
        if (!originalDefaults.ContainsKey(actionName))
        {
            Debug.LogError($"找不到键位{actionName}");
            return;
        }
        KeyCode defaultKey = originalDefaults[actionName];//恢复原始键位
        Debug.Log($"  -> 默认键位: {defaultKey}");
        PlayerPrefs.SetInt($"Key_{actionName}", (int)defaultKey);//保存到PlayerPrefs
        Debug.Log($"键位[{actionName}]已重置为：{defaultKey}");
        if (keyItems.ContainsKey(actionName))//更新显示
        {
            Debug.Log($"  -> 更新UI显示");
            keyItems[actionName].SetKeyCode(defaultKey);
        }
    }
    #region UI刷新方法
    private void RefreshAllKeyDisplay()
    {
        Debug.Log("=== 开始刷新所有键位显示 ===");
        Debug.Log($"keyItems 数量: {keyItems.Count}");
        Debug.Log($"originalDefaults 数量: {originalDefaults.Count}");

        // 如果字典为空，说明有问题
        if (keyItems.Count == 0)
        {
            Debug.LogError("keyItems 为空！键位UI没有初始化");
            return;
        }

        if (originalDefaults.Count == 0)
        {
            Debug.LogError("originalDefaults 为空！默认键位没有保存");
            return;
        }

        int updatedCount = 0;

        int errorCount = 0;
        /*
                foreach (var kvp in keyItems)
                {
                    KeyCode currentKey = (KeyCode)PlayerPrefs.GetInt($"Key_{kvp.Key}", (int)originalDefaults[kvp.Key]);
                    kvp.Value.SetKeyCode(currentKey);
                }*/
        foreach (var kvp in keyItems)
        {
            string actionName = kvp.Key;
            KeyBindingItem item = kvp.Value;

            // 1. 检查键位名称是否有效
            if (!originalDefaults.ContainsKey(actionName))
            {
                Debug.LogError($"找不到 {actionName} 的默认键位");
                errorCount++;
                continue;
            }

            // 2. 从PlayerPrefs读取当前键位
            KeyCode defaultKey = originalDefaults[actionName];
            KeyCode currentKey = (KeyCode)PlayerPrefs.GetInt($"Key_{actionName}", (int)defaultKey);

            Debug.Log($"处理 {actionName}: PlayerPrefs值={currentKey}, 默认值={defaultKey}");

            // 3. 检查KeyBindingItem是否存在
            if (item == null)
            {
                Debug.LogError($"KeyBindingItem 为空: {actionName}");
                errorCount++;
                continue;
            }

            // 4. 更新显示
            try
            {
                item.SetKeyCode(currentKey);
                updatedCount++;
                Debug.Log($"  -> 成功更新 {actionName} 为 {currentKey}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新 {actionName} 失败: {e.Message}");
                errorCount++;
            }
        }

        Debug.Log($"=== 刷新完成 ===");
        Debug.Log($"成功更新: {updatedCount} 个");
        Debug.Log($"失败: {errorCount} 个");
        Debug.Log($"总计: {keyItems.Count} 个");
    }
    #endregion
    #region 工具方法
    //检查是否有自定义设置
    public bool HasCustomBindings()
    {
        foreach(KeyBindingData data in keyBindings)
        {
            KeyCode savedKey = (KeyCode)PlayerPrefs.GetInt($"Key_{data.actionName}", (int)originalDefaults[data.actionName]);
            if (savedKey != originalDefaults[data.actionName])
                return true;
        }
        return false;
    } 
    //获取已修改的键位数量
    public int GetModifiedKeyCount()
    {
        int count = 0;
        foreach(KeyBindingData data in keyBindings)
        {
            KeyCode savedKey = (KeyCode)PlayerPrefs.GetInt($"Key_{data.actionName}", (int)originalDefaults[data.actionName]);
            if (savedKey != originalDefaults[data.actionName])
                count++;
        }
        return count;
    }
    //导出当前设置用于备份
    public string ExportCurrentBindings()
    {
        Dictionary<string, int> bindings = new Dictionary<string, int>();
        foreach(KeyBindingData data in keyBindings)
        {
            KeyCode savedKey = (KeyCode)PlayerPrefs.GetInt($"Key_{data.actionName}", (int)originalDefaults[data.actionName]);
            bindings[data.actionName] = (int)savedKey;
        }
        return JsonUtility.ToJson(bindings);
    }
    //导入设置（从备份恢复）
    public void ImportBindings(string json)
    {
        try
        {
            var bindings = JsonUtility.FromJson<Dictionary<string, int>>(json);

            if (bindings == null)
            {
                Debug.Log("导入失败：JSON格式错误");
                return;
            }

            // 应用设置
            foreach (var binding in bindings)
            {
                PlayerPrefs.SetInt($"Key_{binding.Key}", binding.Value);
            }

            PlayerPrefs.Save();
            RefreshAllKeyDisplay();

            Debug.Log($"成功导入 {bindings.Count} 个键位");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"导入失败: {e.Message}");
        }
    }
    #endregion
    // 开始重绑定
    private void StartRebinding(string actionName, Button button)
    {
        isRebinding = true;
        currentRebindingAction = actionName;
        currentRebindingButton = button;

        // 通知对应按键项进入等待状态
        if (keyItems.ContainsKey(actionName))
        {
            keyItems[actionName].SetWaitingState();
        }

        Debug.Log($"等待输入: {actionName}");
        StartCoroutine(WaitForKeyPress());
    }

    // 等待按键输入
    private System.Collections.IEnumerator WaitForKeyPress()
    {
        while (isRebinding)
        {
            if (Input.anyKeyDown)
            {
                // 检测按键
                foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        // 排除鼠标按键
                        if (!IsMouseButton(keyCode))
                        {
                            OnKeyPressed(keyCode);//处理按下的键
                            yield break;//结束协程
                        }
                    }
                }
            }
            yield return null;//等待下一帧
        }
    }

    // 按键被按下
    private void OnKeyPressed(KeyCode newKey)
    {
        Debug.Log($"按键 {newKey} 被按下");

        // 检查冲突
        if (CheckKeyConflict(currentRebindingAction, newKey))
        {
            Debug.Log($"按键 {newKey} 已被占用");
            // 可以在这里提示用户
            CancelRebinding();
            return;
        }

        // 保存新键位
        PlayerPrefs.SetInt($"Key_{currentRebindingAction}", (int)newKey);
        PlayerPrefs.Save();

        // 更新显示
        if (keyItems.ContainsKey(currentRebindingAction))
        {
            keyItems[currentRebindingAction].SetKeyCode(newKey);
        }

        // 完成重绑定
        CompleteRebinding();
    }

    // 完成重绑定
    private void CompleteRebinding()
    {
        isRebinding = false;
        currentRebindingAction = null;
        currentRebindingButton = null;
        Debug.Log("重绑定完成");
    }

    // 取消重绑定
    private void CancelRebinding()
    {
        if (keyItems.ContainsKey(currentRebindingAction))
        {
            keyItems[currentRebindingAction].SetNormalState();
        }

        isRebinding = false;
        currentRebindingAction = null;
        currentRebindingButton = null;
        Debug.Log("重绑定已取消");
    }

    // 检查键位冲突
    private bool CheckKeyConflict(string actionName, KeyCode newKey)
    {
        foreach (var kvp in keyItems)
        {
            if (kvp.Key != actionName && kvp.Value.currentKey == newKey)
            {
                return true;
            }
        }
        return false;
    }

    // 判断是否是鼠标按键
    private bool IsMouseButton(KeyCode key)
    {
        return key == KeyCode.Mouse0 || key == KeyCode.Mouse1 ||
               key == KeyCode.Mouse2 || key == KeyCode.Mouse3 ||
               key == KeyCode.Mouse4 || key == KeyCode.Mouse5 ||
               key == KeyCode.Mouse6;
    }
}