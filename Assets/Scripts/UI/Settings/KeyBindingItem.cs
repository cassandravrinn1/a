using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KeyBindingItem : MonoBehaviour
{
    [Header("UI引用")]
    public TextMeshProUGUI actionText; //显示"相机前移"等
    public Button keyButton;           //点击修改按键的按钮

    [Header("按钮下的子对象")]
    public TextMeshProUGUI keyText;    //显示"W"等
    public GameObject waitingDisplay;  // 显示"Press Key"

    [Header("当前状态")]
    public KeyCode currentKey;        //当前绑定的按键

    //====状态控制方法====
    // 设置动作名称
    public void SetActionName(string name)
    {
        if (actionText != null)
            actionText.text = name;
    }

    // 设置按键
    public void SetKeyCode(KeyCode key)
    {
        currentKey = key;

        if (keyText != null)
        {
            // 将KeyCode转换为易读的字符串
            keyText.text = KeyCodeToString(key);

        }

        SetNormalState();
    }

    // 设置为等待状态
    public void SetWaitingState()
    {
        if (waitingDisplay != null)
            waitingDisplay.SetActive(true);       //隐藏正常文本

        if (keyText != null)
            keyText.gameObject.SetActive(false);  //显示等待文本

        if (keyButton != null)
            keyButton.interactable = false;        //防止重复点击
    }

    // 设置为正常状态
    public void SetNormalState()
    {
        if (waitingDisplay != null)
            waitingDisplay.SetActive(false);      //显示正常文本

        if (keyText != null)
            keyText.gameObject.SetActive(true);   //隐藏等待文本

        if (keyButton != null)
            keyButton.interactable = true;        //确保按钮可点击
    }

    // KeyCode转字符串（处理特殊键）
    private string KeyCodeToString(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift: return "Shift";
            case KeyCode.RightShift: return "R.Shift";
            case KeyCode.LeftControl: return "Ctrl";
            case KeyCode.RightControl: return "R.Ctrl";
            case KeyCode.LeftAlt: return "Alt";
            case KeyCode.RightAlt: return "R.Alt";
            //case KeyCode.Mouse0: return "鼠标左键";
            //case KeyCode.Mouse1: return "鼠标右键";
            //case KeyCode.Mouse2: return "鼠标中键";
            //case KeyCode.Space: return "空格";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.Plus: return "+";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";

            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            default: return key.ToString();
        }
    }
}