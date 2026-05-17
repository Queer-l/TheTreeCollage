using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 类似打地鼠小游戏中“可被点击物品”的脚本。
/// 可挂在 UI 物体或带 Collider/Collider2D 的场景物体上。
/// </summary>
public class WhackTarget : MonoBehaviour, IPointerClickHandler
{
    public enum HitAction
    {
        Hide,
        Destroy,
        None
    }

    [Header("得分")]
    [Tooltip("点击命中后增加到当局分数。可以为负数。")]
    public int scoreValue = 1;

    [Header("命中行为")]
    [Tooltip("命中后对当前物体执行的行为。")]
    public HitAction hitAction = HitAction.Hide;

    [Tooltip("是否只允许被命中一次。")]
    public bool canHitOnlyOnce = true;

    [Tooltip("命中后延迟多久再隐藏或销毁。")]
    public float hitActionDelay = 0f;

    [Header("事件")]
    public UnityEvent<WhackTarget> onHit;

    public static event Action<WhackTarget> TargetHit;

    public bool HasBeenHit { get; private set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        Hit();
    }

    private void OnMouseDown()
    {
        Hit();
    }

    /// <summary>
    /// 外部脚本也可以直接调用，用于键盘、手柄或射线检测命中。
    /// </summary>
    public void Hit()
    {
        if (!enabled || (canHitOnlyOnce && HasBeenHit))
        {
            return;
        }

        HasBeenHit = true;

        onHit?.Invoke(this);
        TargetHit?.Invoke(this);

        switch (hitAction)
        {
            case HitAction.Hide:
                Invoke(nameof(HideSelf), Mathf.Max(0f, hitActionDelay));
                break;
            case HitAction.Destroy:
                Destroy(gameObject, Mathf.Max(0f, hitActionDelay));
                break;
        }
    }

    /// <summary>
    /// 让对象重新变成可点击状态，适合对象池或复用同一个地鼠。
    /// </summary>
    public void ResetTarget()
    {
        HasBeenHit = false;
        gameObject.SetActive(true);
    }

    private void HideSelf()
    {
        gameObject.SetActive(false);
    }
}
