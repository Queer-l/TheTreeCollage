using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 线索详情弹窗。
///
/// 监听 ClueSlot.OnClueClick 事件，并把被点击线索的图片、名称和描述显示到详情 UI 上。
/// </summary>
public class ClueInfoUI : MonoBehaviour
{
    [Header("线索详情 UI")]
    [Tooltip("显示线索图片。")]
    public Image clueImage;

    [Tooltip("显示线索名称。")]
    public TextMeshProUGUI clueName;

    [Tooltip("显示线索描述文本。")]
    public TextMeshProUGUI clueDescription;

    private UIControl uiControl;

    private void Awake()
    {
        uiControl = GetComponent<UIControl>();
    }

    private void OnEnable()
    {
        ClueSlot.OnClueClick += ShowClueInfo;
    }

    private void OnDisable()
    {
        ClueSlot.OnClueClick -= ShowClueInfo;
    }

    /// <summary>
    /// 显示指定线索详情。
    /// </summary>
    public void ShowClueInfo(ClueSO clue)
    {
        if (clue == null)
        {
            return;
        }

        if (uiControl == null)
        {
            Debug.LogWarning("线索详情 UI 缺少 UIControl 组件。", this);
            return;
        }

        SetText(clueName, clue.clueName);
        SetText(clueDescription, clue.clueIntro);

        if (clueImage != null)
        {
            clueImage.sprite = clue.clueImage;
            clueImage.enabled = clue.clueImage != null;
        }

        uiControl.Open();
        uiControl.BringToFront();
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }
}
