using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个剧情节点的数据资产。
///
/// 一个 StoryNodeSO 对应剧情流程中的一段对话或一个剧情状态：
/// - StoryManager.ShowNode 会读取本节点的文本、CG、立绘索引、奖励线索和分支选项。
/// - 大部分顺序剧情可以通过 nextNodeID 跳转到下一段剧情。
/// - 玩家选择某个 StoryChoice 后，会跳转到该选项配置的 nextNodeID。
/// - 节点本身只保存剧情配置，不负责 UI 显示和按钮逻辑。
/// </summary>
[CreateAssetMenu(fileName = "New Story Node", menuName = "Game/Story Node")]
public class StoryNodeSO : ScriptableObject
{
    [Header("节点信息")]
    [Tooltip("剧情节点唯一 ID。用于 PlayerData.currentStoryNodeId 记录当前剧情进度，建议不要重复。")]
    public string nodeID;

    [Tooltip("当前对话显示的说话人名称。旁白或系统文本可以填空或填“旁白”。")]
    public string speakerName;

    [TextArea(3, 10)]
    [Tooltip("当前节点显示的主要对话/剧情文本。")]
    public string dialogueText;

    [Header("画面资源")]
    [Tooltip("当前节点使用的场景 CG 或背景图。为空时 StoryManager 会隐藏对应 Image。")]
    public Sprite cgImage;

    [Tooltip("当前节点显示的人物立绘索引。StoryManager 会用该索引从 StoryCanvas 上的立绘数组中读取 Sprite。\n-1：不显示立绘\n0：小明\n1：父亲\n2：母亲\n3：陈爷爷/陈工\n4：陈奶奶\n5：陈春柏\n6：高中历史老师\n7：卫生所年轻医生\n8：疯疯癫癫的道士\n9：村民群像\n10：旁白\n11：树神/古神")]
    public int characterImageIndex = -1;

    [Header("顺序对话")]
    [Tooltip("下一段顺序剧情节点 ID。下一对话按钮会优先用这个 ID 查找目标节点。")]
    public string nextNodeID;

    [Tooltip("勾选后，玩家在本节点点击“下一对话”时不会继续跳转剧情，而是关闭 Story UI，回到普通模式。适合每一幕最后一句。")]
    public bool returnToNormalModeOnNext;

    [Header("节点效果")]
    [Tooltip("进入该节点时自动解锁的线索 SO。为空表示该节点不奖励线索。")]
    public ClueSO rewardClue;

    [Tooltip("进入该节点时自动写入 PlayerData.storyFlags 的剧情标记。可用于后续分支条件。")]
    public string flagOnEnter;

    [Tooltip("该节点是否为结局节点。用于后续扩展结局 UI、结算或返回主菜单逻辑。")]
    public bool isEnding;

    [Tooltip("结局类型。只有 isEnding 为 true 时才有实际语义。")]
    public EndingType endingType;

    [Header("分支")]
    [Tooltip("当前节点可供玩家选择的分支列表。StoryManager 会按列表顺序生成/刷新选择按钮。")]
    public List<StoryChoice> choices = new List<StoryChoice>();
}

/// <summary>
/// 单个剧情选择项。
///
/// StoryManager 会根据 requiredClue 和 requiredFlag 判断该选项是否可选。
/// 玩家点击可选分支后，会写入 flagOnChoose，并跳转到 nextNodeID 对应的剧情节点。
/// </summary>
[Serializable]
public class StoryChoice
{
    [Tooltip("显示在选择按钮上的文字。")]
    public string choiceText;

    [Tooltip("玩家选择该分支后跳转到的剧情节点 ID。每个分支都应填写自己的跳转 ID。")]
    public string nextNodeID;

    [Tooltip("选择该分支需要已经解锁的线索。为空表示不需要线索条件。")]
    public ClueSO requiredClue;

    [Tooltip("选择该分支需要已经拥有的剧情 flag。为空表示不需要 flag 条件。")]
    public string requiredFlag;

    [Tooltip("玩家选择该分支后写入 PlayerData.storyFlags 的剧情标记。")]
    public string flagOnChoose;
}

/// <summary>
/// 剧情结局类型。
///
/// 当前主要用于标记结局语义，方便之后根据不同结局显示不同 UI、成就或存档结果。
/// </summary>
public enum EndingType
{
    /// <summary>普通剧情节点或未指定结局类型。</summary>
    None,

    /// <summary>真结局。</summary>
    TrueEnding,

    /// <summary>普通结局。</summary>
    NormalEnding,

    /// <summary>坏结局。</summary>
    BadEnding
}
