using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 线索配置资产。
///
/// 一个 ClueSO 表示玩家可以解锁和查看的一条线索。它只保存数据，不处理 UI 和交互。
/// </summary>
[CreateAssetMenu(fileName = "New Clue", menuName = "Game/ClueSO")]
public class ClueSO : ScriptableObject
{
    [Header("线索信息")]
    [Tooltip("线索显示名称。")]
    [FormerlySerializedAs("clueName")]
    public string clueName;

    [TextArea]
    [Tooltip("线索介绍。")]
    [FormerlySerializedAs("clueText")]
    public string clueIntro;

    [Tooltip("线索在格子和详情面板中显示的图片。")]
    [FormerlySerializedAs("clueIcon")]
    public Sprite clueImage;

    [Tooltip("线索唯一 ID。剧情条件和旧数据迁移会使用这个值，请不要重复。")]
    [FormerlySerializedAs("clueID")]
    public int clueId;

    [Header("剧情扩展")]
    [Tooltip("是否为影响主线或结局的关键线索。")]
    public bool isKeyClue;

    [Tooltip("获得该线索时写入的剧情 flag，可用于解锁后续分支。")]
    public string storyFlagOnCollect;
}
