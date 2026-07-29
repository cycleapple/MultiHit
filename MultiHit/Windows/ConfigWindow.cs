using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace MultiHit.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private string addGroupNameText = "";
    private string editGroupNameText = "";
    private string addActionFilterText = "";
    private int selectedGroupIdx = -1;
    private int selectedActionIdx = -1;

    private readonly FileDialogManager _dialogManager = SetupFileManager();
    private bool _dialogOpen;
    private string _lastExportDirectory = ".";

    public ConfigWindow(Plugin plugin) : base(
        "MultiHit 設定")
    {
        this.Size = new Vector2(700, 500);
        this.SizeCondition = ImGuiCond.FirstUseEver;

        this.Plugin = plugin;
        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Checkbox("啟用", ref Configuration.Enabled))
        {
            this.Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("匯出"))
        {
            if (_dialogOpen)
            {
                _dialogManager.Reset();
                _dialogOpen = false;
            }
            else
            {
                // Use the current input as start directory if it exists,
                // otherwise the current mod directory, otherwise the current application directory.
                var startDir = _lastExportDirectory ?? ".";

                _dialogManager.OpenFolderDialog("選擇匯出資料夾", (b, s) =>
                {
                    Plugin.ExportGroups(s);
                    _lastExportDirectory = s;
                    _dialogOpen = false;
                }, startDir);
                _dialogOpen = true;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("匯入"))
        {
            if (_dialogOpen)
            {
                _dialogManager.Reset();
                _dialogOpen = false;
            }
            else
            {
                // Use the current input as start directory if it exists,
                // otherwise the current mod directory, otherwise the current application directory.
                var startDir = ".";

                _dialogManager.OpenFileDialog("選擇要匯入的設定", ".json", (b, strs) =>
                {
                    foreach (var s in strs)
                    {
                        Plugin.ImportGroup(s);
                    }
                    _dialogOpen = false;
                }, 10, startDir);
                _dialogOpen = true;
            }
        }
        ImGui.SameLine();
        if(Configuration.changed)
        {
            var applyText = "套用變更";
            var nextPos = ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(applyText).X - 20;
            var validateText = "驗證群組";
            nextPos = nextPos - ImGui.CalcTextSize(validateText).X - 30;
            ImGui.SetCursorPosX(nextPos);
            if (ImGui.Checkbox(validateText, ref Configuration.validateActionGroups))
            {
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("驗證技能各段傷害百分比的總和不超過 100%%");
            }
            ImGui.SameLine();
            if (ImGui.Button(applyText))
            {
                Plugin.validateActionGroups();
                Plugin.updateAffectedAction();
                Configuration.ApplyChange();
            }
        }
        ImGui.Spacing();
        if (ImGui.BeginChild("#MultiHitGroup",
                new Vector2((float)(ImGui.GetContentRegionMax().X * 0.3), (float)(ImGui.GetContentRegionAvail().Y * 0.98)),
                true
            ))
        {
            this.DrawGroup();
            ImGui.EndChild();
        }
        ImGui.SameLine();
        if (ImGui.BeginChild("#MultiHitDetails",
                new Vector2((float)(ImGui.GetContentRegionMax().X * 0.68), (float)(ImGui.GetContentRegionAvail().Y * 0.98)),
                true
            ))

        {
            this.DrawDetail();
            ImGui.EndChild();
        }

        _dialogManager.Draw();
    }
    public void DrawGroup()
    {
        //ImGui.Text("Group Window");
        if (ImGui.BeginChild("#MultiHitGroupList",
                new Vector2((float)(ImGui.GetContentRegionMax().X), (float)(ImGui.GetContentRegionAvail().Y * 0.95))
            ))
        {
            var groupToDeleteIdx = -1;
            var actionGroups = CollectionsMarshal.AsSpan(Configuration.actionGroups);
            for (var groupIdx = 0; groupIdx < Configuration.actionGroups.Count; groupIdx++)
            {
                ref var group = ref actionGroups[groupIdx];
                if(!group.enabled)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                }
                var open = ImGui.TreeNode(group.name + $"##Group{groupIdx}");
                if (!group.enabled)
                {
                    ImGui.PopStyleColor();
                }
                if (ImGui.BeginPopupContextItem($"NewActionPopup##Group{groupIdx}"))
                {
                    if (ImGui.BeginMenu("新增技能"))
                    {
                        ImGui.SetNextItemWidth(200);
                        ImGui.InputText("##Filter", ref addActionFilterText, 128);
                        if (ImGui.BeginChild("ActionList",
                            new Vector2((float)(ImGui.GetContentRegionAvail().X), 300),
                            true
                            ))
                        {
                            foreach (var action in Plugin.actionList.Where(act =>
                                act.Name.ToString().ToLower().Contains(addActionFilterText.ToLower())
                                || act.RowId.ToString().ToLower().Contains(addActionFilterText.ToLower())))
                            {
                                if (ImGui.Selectable(action.Name.ToString() + $"##Action{action.RowId}"))
                                {
                                    group.actionList.Add(new ActionMultiHit((int)action.RowId, action.Name.ToString()));
                                    Configuration.Save();
                                    ImGui.CloseCurrentPopup();
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"#{action.RowId}");
                                }
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.Selectable("啟用"))
                    {
                        group.enabled = true;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("停用"))
                    {
                        group.enabled = false;
                        Configuration.Save();
                    }
                    if (ImGui.BeginMenu("編輯名稱"))
                    {
                        var name = editGroupNameText == "" ? group.name : editGroupNameText;
                        if (ImGui.InputText("##EditGroupPopupNameEdit", ref name, 64))
                        {
                            if (name != group.name)
                            {
                                editGroupNameText = name;
                            }
                        }
                        if (ImGui.Button("儲存"))
                        {
                            group.name = editGroupNameText;
                            editGroupNameText = "";
                            Configuration.Save();
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.Selectable("匯出"))
                    {
                        if (_dialogOpen)
                        {
                            _dialogManager.Reset();
                            _dialogOpen = false;
                        }
                        else
                        {
                            var startDir = _lastExportDirectory ?? ".";
                            var tempGroup = group;
                            _dialogManager.OpenFolderDialog("選擇匯出資料夾", (b, s) =>
                            {
                                Plugin.ExportGroup(s, tempGroup);
                                _lastExportDirectory = s;
                                _dialogOpen = false;
                            }, startDir);
                            _dialogOpen = true;
                        }
                        Plugin.Log.Debug($"Exporting group#{groupIdx}");
                    }
                    if (ImGui.Selectable("刪除"))
                    {
                        groupToDeleteIdx = groupIdx;
                        Plugin.Log.Debug($"To delete group#{groupIdx}");
                    }
                    ImGui.EndPopup();
                }
                if (open)
                {
                    var actionToDeleteIdx = -1;
                    if (group.actionList != null)
                    {
                        var actionList = CollectionsMarshal.AsSpan(group.actionList);
                        for (int actionIdx = 0; actionIdx < group.actionList.Count; actionIdx++)
                        {
                            ref var action = ref actionList[actionIdx];
                            if (!action.enabled || !group.enabled)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                            }
                            var actionOpen = ImGui.Selectable(action.ToString(), selectedGroupIdx == groupIdx && selectedActionIdx == actionIdx);
                            if (!action.enabled || !group.enabled)
                            {
                                ImGui.PopStyleColor();
                            }
                            if (ImGui.BeginPopupContextItem($"OperationPopup##Group{groupIdx}action{actionIdx}"))
                            {
                                if (ImGui.Selectable("啟用"))
                                {
                                    action.enabled = true;
                                    Configuration.Save();
                                }
                                if (ImGui.Selectable("停用"))
                                {
                                    action.enabled = false;
                                    Configuration.Save();
                                }
                                if (ImGui.Selectable("刪除"))
                                {
                                    actionToDeleteIdx = actionIdx;
                                    Plugin.Log.Debug($"To delete action {action}");
                                }
                                ImGui.EndPopup();
                            }
                            if (actionOpen)
                            {
                                Plugin.Log.Debug($"Selecting {action}");
                                selectedGroupIdx = groupIdx;
                                selectedActionIdx = actionIdx;
                            }
                        }
                        if (actionToDeleteIdx != -1)
                        {
                            group.actionList.RemoveAt(actionToDeleteIdx);
                            selectedGroupIdx = -1;
                            selectedActionIdx = -1;
                            Configuration.Save();
                        }
                    }
                    ImGui.TreePop();
                }
            }
            
            if (groupToDeleteIdx != -1)
            {
                Configuration.actionGroups.RemoveAt(groupToDeleteIdx);
                selectedGroupIdx = -1;
                selectedActionIdx = -1;
                Configuration.Save();
            }
            ImGui.EndChild();
        }
        if (ImGui.Button("新增群組"))
        {
            ImGui.OpenPopup("Add Group Popup");
        }
        if (ImGui.BeginPopup("Add Group Popup"))
        {
            var len = Configuration.actionGroups.Count;
            var defaultName = $"群組 {len + 1}";
            var name = addGroupNameText == "" ? defaultName : addGroupNameText;
            if (ImGui.InputText("##AddGroupPopupNameEdit", ref name, 64))
            {
                if(name != defaultName)
                {
                    addGroupNameText = name;
                }
            }
            if (ImGui.Button("新增"))
            {
                addGroupNameText = "";
                Configuration.actionGroups.Add(new ActionGroup(name));
                Configuration.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
    public void DrawDetail()
    {
        if(selectedGroupIdx == -1 || selectedActionIdx == -1)
        {
            var hintText = "請選擇技能";
            ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(hintText).X) * 0.5f);
            ImGui.Text(hintText);
            return;
        }
        var actionList = CollectionsMarshal.AsSpan(Configuration.actionGroups[selectedGroupIdx].actionList);
        ref var action = ref actionList[selectedActionIdx];
        var titleText = $"{action}";
        ImGui.Spacing();
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(titleText).X) * 0.5f);
        ImGui.Text(titleText);
        if(ImGui.Checkbox("啟用", ref action.enabled))
        {
            Configuration.Save();
        }
        if (ImGui.Checkbox("可被中斷", ref action.interruptible))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("其他動畫是否能中斷接下來的浮動文字。");
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("顯示段數", ref action.showHit))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("是否在浮動文字中顯示「第 i 段」。");
        }
        if (ImGui.Checkbox("顯示最終傷害", ref action.showFinal))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("是否將原始傷害顯示為最終傷害。");
        }
        if (action.showFinal)
        {
            int delay = (int)action.finalHit.time;
            ImGui.SameLine();
            ImGui.Text("延遲：");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("延遲顯示最終傷害（在最後一段之後）。");
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##DelayHit_Final", ref delay, 1, 5))
            {
                delay = Math.Min(delay, 300);
                delay = Math.Max(delay, 0);
                action.finalHit.time = delay;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("30 = 1 秒");
            }
            ImGui.SameLine();
            var uintCol = action.finalHit.color;
            var R = uintCol >> 24;
            var G = (uintCol >> 16) & 0xFF;
            var B = (uintCol >> 8) & 0xFF;
            var A = uintCol & 0xFF;
            var col = new Vector4(R / 255.0f, G / 255.0f, B / 255.0f, A / 255.0f);
            if (ImGui.ColorEdit4($"ColorPicker##FinalColorPickerHit", ref col,
                ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar))
            {
                action.finalHit.color = ((uint)(col.X * 255.0) << 24) | ((uint)(col.Y * 255.0) << 16) | ((uint)(col.Z * 255.0) << 8) | (uint)(col.W * 255.0);
                Configuration.Save();
            }
        }
        if (ImGui.Checkbox("自訂名稱", ref action.hasCustomName))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("自訂浮動文字中的技能名稱。");
        }
        if (action.hasCustomName)
        {
            ImGui.SameLine();
            var name = action.customName ?? "";
            if (ImGui.InputText("##CustomName", ref name, 32))
            {
                action.customName = name;
                Configuration.Save();
            }
        }
        if (ImGui.Button("新增一段"))
        {
            if (action.hitList.Count < 100)
            {
                action.hitList.Add(new Hit());
                Configuration.Save();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("全部清除"))
        {
            action.hitList.Clear();
            Configuration.Save();
        }
        if (ImGui.BeginChild("detailActionList", 
            new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y * 0.8f)
            , false))
        {
            var hitList = CollectionsMarshal.AsSpan(action.hitList);
            int hitIdxToDelete = -1;
            int hitIdxToBeColored = -1;
            uint hitColorToBeColored = 0;
            for (int hitIdx = 0; hitIdx < action.hitList.Count; hitIdx++)
            {
                ref var hit = ref hitList[hitIdx];
                ImGui.Text($"第 {hitIdx + 1, 3} 段：");
                ImGui.SameLine();
                int pct = hit.percent;
                ImGui.Text("百分比："); ImGui.SameLine();
                ImGui.SetNextItemWidth(120);
                if (ImGui.InputInt($"##PercentageHit_{hitIdx}", ref pct, 1, 5))
                {
                    pct = Math.Min(pct, 100);
                    pct = Math.Max(pct, 0);
                    hit.percent = pct;
                    Configuration.Save();
                }
                ImGui.SameLine();
                int delay = (int)hit.time;
                ImGui.Text("延遲："); ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt($"##DelayHit_{hitIdx}", ref delay, 1, 5))
                {
                    delay = Math.Min(delay, 300);
                    delay = Math.Max(delay, 0);
                    hit.time = delay;
                    Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("30 = 1 秒");
                }
                ImGui.SameLine();
                var uintCol = hit.color;
                var R = uintCol >> 24;
                var G = (uintCol >> 16) & 0xFF;
                var B = (uintCol >> 8) & 0xFF;
                var A = uintCol & 0xFF;
                var col = new Vector4(R / 255.0f, G / 255.0f, B / 255.0f, A / 255.0f);
                if (ImGui.ColorEdit4($"ColorPicker##ColorPickerHit_{hitIdx}", ref col,
                    ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar))
                {
                    hit.color = ((uint)(col.X * 255.0) << 24) | ((uint)(col.Y * 255.0) << 16) | ((uint)(col.Z * 255.0) << 8) | (uint)(col.W * 255.0);
                    Configuration.Save();
                }
                if (ImGui.BeginPopupContextItem($"ApplyColor##{hitIdx}"))
                {
                    if (ImGui.Selectable("套用至所有段數"))
                    {
                        hitIdxToBeColored = hitIdx;
                        hitColorToBeColored = uintCol;
                    }
                    ImGui.EndPopup();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Alpha 不為 0 時才會套用自訂顏色。");
                }

                ImGui.SameLine();
                if(ImGuiComponents.IconButton( FontAwesomeIcon.Trash.ToIconString() + $"##{hitIdx}"))
                //if (ImGui.Button($"Delete##DeleteHit_{hitIdx}"))
                {
                    hitIdxToDelete = hitIdx;
                }
            }
            if (hitIdxToDelete != -1)
            {
                action.hitList.RemoveAt(hitIdxToDelete);
                Configuration.Save();
            }
            if (hitIdxToBeColored != -1)
            {
                for (int hitIdx = 0; hitIdx < action.hitList.Count; hitIdx++)
                {
                    ref var hit = ref hitList[hitIdx];
                    hit.color = hitColorToBeColored;
                    Configuration.Save();
                }
            }
            ImGui.EndChild();
        }
    }
    public static FileDialogManager SetupFileManager()
    {
        var fileManager = new FileDialogManager
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
        };

        // Remove Videos and Music.
        fileManager.CustomSideBarItems.Add(("Videos", string.Empty, 0, -1));
        fileManager.CustomSideBarItems.Add(("Music", string.Empty, 0, -1));

        return fileManager;
    }
}
