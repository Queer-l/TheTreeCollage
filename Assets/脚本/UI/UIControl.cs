using UnityEngine;

public class UIControl : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("控制面板透明度、交互和射线阻挡。未手动指定时会自动获取。")]
    public CanvasGroup canvasGroup;

    [Tooltip("控制面板层级。未手动指定时会自动获取。")]
    public Canvas canvas;

    [Tooltip("面板打开时使用的排序层级。")]
    public int openSortingOrder = 2;

    [Tooltip("面板关闭时使用的排序层级。")]
    public int closedSortingOrder = 0;

    public bool isOpen = false;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }
    }

    public void Open()
    {
        SetVisible(true, openSortingOrder);
    }

    public void Close()
    {
        SetVisible(false, closedSortingOrder);
    }

    public void BringToFront()
    {
        if (canvas != null)
        {
            canvas.sortingOrder = openSortingOrder;
        }
    }

    private void SetVisible(bool visible, int sortingOrder)
    {
        if (isOpen == visible)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }

        isOpen = visible;
    }
}

