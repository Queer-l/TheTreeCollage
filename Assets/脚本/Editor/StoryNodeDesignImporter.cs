using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class StoryNodeDesignImporter
{
    private const int NarratorCharacterImageIndex = 10;
    private const int TreeGodCharacterImageIndex = 11;
    private const string DesignPath = "md\u6587\u6863/\u5177\u4f53\u8282\u70b9\u8bbe\u8ba1.md";
    private const string OutputFolder = "Assets/SOS/\u5267\u60c5so/\u4e3b\u7ebf\u5267\u60c5";
    private const string StoryRootFolder = "Assets/SOS/\u5267\u60c5so";
    private const string StoryFolderName = "\u5267\u60c5so";
    private const string MainStoryFolderName = "\u4e3b\u7ebf\u5267\u60c5";
    private const string NoneText = "\u65e0";
    private const string KongText = "kong";

    [MenuItem("Tools/Generate Story Nodes From Design")]
    public static void GenerateStoryNodesFromDesign()
    {
        EnsureOutputFolder();

        if (!File.Exists(DesignPath))
        {
            Debug.LogError($"Story node design file not found: {DesignPath}");
            return;
        }

        Dictionary<string, ClueSO> clueLookup = BuildClueLookup();
        List<StoryNodeRow> rows = ParseDesignRows(File.ReadAllLines(DesignPath));
        int createdCount = 0;
        int updatedCount = 0;

        foreach (StoryNodeRow row in rows)
        {
            string assetPath = $"{OutputFolder}/{row.nodeID}.asset";
            StoryNodeSO node = AssetDatabase.LoadAssetAtPath<StoryNodeSO>(assetPath);
            bool created = false;

            if (node == null)
            {
                node = ScriptableObject.CreateInstance<StoryNodeSO>();
                AssetDatabase.CreateAsset(node, assetPath);
                created = true;
            }

            ApplyRowToNode(node, row, clueLookup);
            EditorUtility.SetDirty(node);

            if (created)
            {
                createdCount++;
            }
            else
            {
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Story nodes generated from design. Created: {createdCount}, Updated: {updatedCount}, Total rows: {rows.Count}");
    }

    private static void ApplyRowToNode(StoryNodeSO node, StoryNodeRow row, Dictionary<string, ClueSO> clueLookup)
    {
        node.nodeID = row.nodeID;
        node.speakerName = NormalizeNone(row.speakerName);
        node.dialogueText = NormalizeNone(row.dialogueText);
        node.cgImage = null;
        node.characterImageIndex = GetCharacterImageIndex(row.speakerName, row.characterImageIndex);
        node.nextNodeID = row.nextNodeID == "0" ? string.Empty : row.nextNodeID;
        node.returnToNormalModeOnNext = false;
        node.rewardClue = FindClue(row.rewardClueName, clueLookup);
        node.flagOnEnter = ExtractAssignment(row.branchSetting, "flagOnEnter");
        node.isEnding = ExtractBoolAssignment(row.branchSetting, "isEnding");
        node.endingType = ExtractEndingType(row.branchSetting);
        node.choices = ParseChoices(row.branchSetting, clueLookup);
    }

    private static List<StoryNodeRow> ParseDesignRows(string[] lines)
    {
        List<StoryNodeRow> rows = new List<StoryNodeRow>();

        foreach (string line in lines)
        {
            if (!line.StartsWith("| "))
            {
                continue;
            }

            string[] columns = SplitMarkdownRow(line);
            if (columns.Length < 9)
            {
                continue;
            }

            if (!int.TryParse(columns[0], out _))
            {
                continue;
            }

            rows.Add(new StoryNodeRow
            {
                nodeID = columns[0],
                speakerName = columns[1],
                dialogueText = columns[2],
                characterImageIndex = int.TryParse(columns[3], out int imageIndex) ? imageIndex : -1,
                cgName = columns[4],
                requiredClueText = columns[5],
                nextNodeID = int.TryParse(columns[6], out int nextId) ? nextId.ToString() : string.Empty,
                branchSetting = columns[7],
                rewardClueName = columns[8]
            });
        }

        return rows;
    }

    private static string[] SplitMarkdownRow(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("|"))
        {
            trimmed = trimmed.Substring(1);
        }

        if (trimmed.EndsWith("|"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        string[] rawColumns = trimmed.Split('|');
        for (int i = 0; i < rawColumns.Length; i++)
        {
            rawColumns[i] = rawColumns[i].Trim();
        }

        return rawColumns;
    }

    private static List<StoryChoice> ParseChoices(string branchSetting, Dictionary<string, ClueSO> clueLookup)
    {
        List<StoryChoice> choices = new List<StoryChoice>();
        if (IsNone(branchSetting) || branchSetting.Contains("isEnding=") || branchSetting.Contains("flagOnEnter="))
        {
            return choices;
        }

        MatchCollection labelMatches = Regex.Matches(branchSetting, @"[A-Z][\uff1a:]");
        if (labelMatches.Count == 0)
        {
            return choices;
        }

        for (int i = 0; i < labelMatches.Count; i++)
        {
            int start = labelMatches[i].Index + labelMatches[i].Length;
            int end = i + 1 < labelMatches.Count ? labelMatches[i + 1].Index : branchSetting.Length;
            string segment = branchSetting.Substring(start, end - start).Trim();
            StoryChoice choice = ParseChoiceSegment(segment, clueLookup);
            if (!string.IsNullOrWhiteSpace(choice.choiceText) || !string.IsNullOrWhiteSpace(choice.nextNodeID))
            {
                choices.Add(choice);
            }
        }

        return choices;
    }

    private static StoryChoice ParseChoiceSegment(string segment, Dictionary<string, ClueSO> clueLookup)
    {
        StoryChoice choice = new StoryChoice();
        Match nextMatch = Regex.Match(segment, @"->\s*(\d+)");
        if (nextMatch.Success)
        {
            choice.nextNodeID = nextMatch.Groups[1].Value == "0" ? string.Empty : nextMatch.Groups[1].Value;
            choice.choiceText = segment.Substring(0, nextMatch.Index).Trim();
        }
        else
        {
            choice.choiceText = segment.Trim();
        }

        choice.choiceText = TrimChoiceText(choice.choiceText);
        choice.requiredFlag = ExtractFirstFlagRequirement(segment);
        choice.flagOnChoose = ExtractAssignment(segment, "flagOnChoose");

        string requiredClueName = ExtractFirstBacktickValue(segment);
        if (!IsFlag(requiredClueName))
        {
            choice.requiredClue = FindClue(requiredClueName, clueLookup);
        }

        return choice;
    }

    private static string TrimChoiceText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Trim('\uff0c', ',', '\uff1b', ';');
    }

    private static string ExtractFirstBacktickValue(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"`([^`]+)`");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractFirstFlagRequirement(string value)
    {
        string requirementText = value ?? string.Empty;
        int flagOnChooseIndex = requirementText.IndexOf("flagOnChoose=", System.StringComparison.Ordinal);
        if (flagOnChooseIndex >= 0)
        {
            requirementText = requirementText.Substring(0, flagOnChooseIndex);
        }

        MatchCollection matches = Regex.Matches(requirementText, @"`([^`]+)`");
        foreach (Match match in matches)
        {
            string token = match.Groups[1].Value.Trim();
            if (IsFlag(token))
            {
                return token;
            }
        }

        return string.Empty;
    }

    private static string ExtractAssignment(string value, string key)
    {
        Match match = Regex.Match(value ?? string.Empty, key + @"\s*=\s*`?([A-Za-z0-9_]+)`?");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static bool ExtractBoolAssignment(string value, string key)
    {
        Match match = Regex.Match(value ?? string.Empty, key + @"\s*=\s*(true|false)", RegexOptions.IgnoreCase);
        return match.Success && match.Groups[1].Value.ToLowerInvariant() == "true";
    }

    private static EndingType ExtractEndingType(string value)
    {
        string raw = ExtractAssignment(value, "endingType");
        if (System.Enum.TryParse(raw, out EndingType endingType))
        {
            return endingType;
        }

        return EndingType.None;
    }

    private static ClueSO FindClue(string clueName, Dictionary<string, ClueSO> clueLookup)
    {
        clueName = NormalizeNone(clueName);
        if (string.IsNullOrWhiteSpace(clueName))
        {
            return null;
        }

        clueName = clueName.Replace("`", string.Empty).Trim();
        return clueLookup.TryGetValue(clueName, out ClueSO clue) ? clue : null;
    }

    private static Dictionary<string, ClueSO> BuildClueLookup()
    {
        Dictionary<string, ClueSO> lookup = new Dictionary<string, ClueSO>();
        string[] guids = AssetDatabase.FindAssets("t:ClueSO", new[] { "Assets/SOS/\u7ebf\u7d22SO" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ClueSO clue = AssetDatabase.LoadAssetAtPath<ClueSO>(path);
            if (clue == null || string.IsNullOrWhiteSpace(clue.clueName))
            {
                continue;
            }

            lookup[clue.clueName.Trim()] = clue;
        }

        return lookup;
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SOS"))
        {
            AssetDatabase.CreateFolder("Assets", "SOS");
        }

        if (!AssetDatabase.IsValidFolder(StoryRootFolder))
        {
            AssetDatabase.CreateFolder("Assets/SOS", StoryFolderName);
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(StoryRootFolder, MainStoryFolderName);
        }
    }

    private static bool IsNone(string value)
    {
        value = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) || value == NoneText || value == KongText || value == "0";
    }

    private static string NormalizeNone(string value)
    {
        return IsNone(value) ? string.Empty : value.Trim();
    }

    private static bool IsFlag(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith("FLAG_");
    }

    private static int GetCharacterImageIndex(string speakerName, int configuredIndex)
    {
        if (configuredIndex >= 0)
        {
            return configuredIndex;
        }

        if (speakerName == "\u65c1\u767d")
        {
            return NarratorCharacterImageIndex;
        }

        return speakerName == "\u6811\u795e" || speakerName == "\u53e4\u795e" ? TreeGodCharacterImageIndex : configuredIndex;
    }

    private class StoryNodeRow
    {
        public string nodeID;
        public string speakerName;
        public string dialogueText;
        public int characterImageIndex;
        public string cgName;
        public string requiredClueText;
        public string nextNodeID;
        public string branchSetting;
        public string rewardClueName;
    }
}
