using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    // Property panel (design 7.5, MT-011..013) and the section push tool (MT-023). Every numeric field is a delayed field,
    // so one confirmed input = one Undo step. Values below the contract minimum are kept as typed: the validator reports them.
    internal sealed partial class StageMapToolWindow
    {
        private static readonly string[] BlockKinds = { "ground", "platform", "wall" };
        private static readonly string[] BlockSurfaces = { "normal", "ice", "bounce", "crumble", "conveyor_r", "conveyor_l" };
        private static readonly string[] Axes = { "x", "y" };

        private float shiftAtX = 40f;
        private float shiftDx = 4f;

        private void DrawInspectorPanel()
        {
            var d = controller.Data;
            var sel = controller.Selection;
            EditorGUILayout.LabelField("속성", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (sel.IsNone || !sel.IsValid(d))
                {
                    EditorGUILayout.LabelField("선택된 요소 없음 (Scene 뷰 또는 요소 목록에서 선택)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(controller.Describe(sel), EditorStyles.boldLabel);
                    DrawSelectedFields(d, sel);
                }
                DrawShiftTool();
            }
        }

        private void DrawSelectedFields(StageLayoutData d, LayoutElementRef sel)
        {
            int i = sel.index;
            switch (sel.kind)
            {
                case LayoutElementKind.Spawn:
                    Num("x", dd => dd.spawn.x, (dd, v) => dd.spawn.x = v);
                    Num("y", dd => dd.spawn.y, (dd, v) => dd.spawn.y = v);
                    break;
                case LayoutElementKind.Goal:
                    Num("x", dd => dd.goal.x, (dd, v) => dd.goal.x = v);
                    Num("y", dd => dd.goal.y, (dd, v) => dd.goal.y = v);
                    break;
                case LayoutElementKind.Section:
                    IdField(d, sel);
                    Num("startX", dd => dd.sections[i].startX, (dd, v) => dd.sections[i].startX = v);
                    Num("endX", dd => dd.sections[i].endX, (dd, v) => dd.sections[i].endX = v);
                    TextField("note", d.sections[i].note, (dd, v) => dd.sections[i].note = v);
                    break;
                case LayoutElementKind.Block:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Choice("kind", d.blocks[i].kind, BlockKinds, (dd, v) => dd.blocks[i].kind = v);
                    Num("x", dd => dd.blocks[i].x, (dd, v) => dd.blocks[i].x = v);
                    Num("y (윗면)", dd => dd.blocks[i].y, (dd, v) => dd.blocks[i].y = v);
                    Num("w", dd => dd.blocks[i].w, (dd, v) => dd.blocks[i].w = v);
                    Num("h", dd => dd.blocks[i].h, (dd, v) => dd.blocks[i].h = v);
                    Choice("surface", string.IsNullOrEmpty(d.blocks[i].surface) ? "normal" : d.blocks[i].surface, BlockSurfaces, (dd, v) => dd.blocks[i].surface = v == "normal" ? null : v);
                    break;
                case LayoutElementKind.Checkpoint:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x", dd => dd.checkpoints[i].x, (dd, v) => dd.checkpoints[i].x = v);
                    Num("y", dd => dd.checkpoints[i].y, (dd, v) => dd.checkpoints[i].y = v);
                    break;
                case LayoutElementKind.Coin:
                    SectionField(d, sel);
                    Num("x", dd => dd.coins[i].x, (dd, v) => dd.coins[i].x = v);
                    Num("y", dd => dd.coins[i].y, (dd, v) => dd.coins[i].y = v);
                    break;
                case LayoutElementKind.Special:
                    IdField(d, sel);
                    SectionField(d, sel);
                    SlotField(d, i);
                    Num("x", dd => dd.specials[i].x, (dd, v) => dd.specials[i].x = v);
                    Num("y", dd => dd.specials[i].y, (dd, v) => dd.specials[i].y = v);
                    break;
                case LayoutElementKind.Spike:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x", dd => dd.spikes[i].x, (dd, v) => dd.spikes[i].x = v);
                    Num("y", dd => dd.spikes[i].y, (dd, v) => dd.spikes[i].y = v);
                    Num("w", dd => dd.spikes[i].w, (dd, v) => dd.spikes[i].w = v);
                    break;
                case LayoutElementKind.Mover:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x", dd => dd.movers[i].x, (dd, v) => dd.movers[i].x = v);
                    Num("y", dd => dd.movers[i].y, (dd, v) => dd.movers[i].y = v);
                    Num("w", dd => dd.movers[i].w, (dd, v) => dd.movers[i].w = v);
                    Choice("axis", d.movers[i].axis, Axes, (dd, v) => dd.movers[i].axis = v);
                    Num("distance", dd => dd.movers[i].distance, (dd, v) => dd.movers[i].distance = v);
                    Num("speed", dd => dd.movers[i].speed, (dd, v) => dd.movers[i].speed = v);
                    Num("wait", dd => dd.movers[i].wait, (dd, v) => dd.movers[i].wait = v);
                    break;
                case LayoutElementKind.Enemy:
                    IdField(d, sel);
                    SectionField(d, sel);
                    MonsterField(d, i);
                    Num("x", dd => dd.enemies[i].x, (dd, v) => dd.enemies[i].x = v);
                    Num("y", dd => dd.enemies[i].y, (dd, v) => dd.enemies[i].y = v);
                    Num("range (끝점 = x + range)", dd => dd.enemies[i].range, (dd, v) => dd.enemies[i].range = v);
                    Num("speed (0 = 테이블 값)", dd => dd.enemies[i].speed, (dd, v) => dd.enemies[i].speed = v);
                    break;
                case LayoutElementKind.Faller:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x", dd => dd.fallers[i].x, (dd, v) => dd.fallers[i].x = v);
                    Num("y", dd => dd.fallers[i].y, (dd, v) => dd.fallers[i].y = v);
                    Num("triggerHalfWidth", dd => dd.fallers[i].triggerHalfWidth, (dd, v) => dd.fallers[i].triggerHalfWidth = v);
                    Num("delay", dd => dd.fallers[i].delay, (dd, v) => dd.fallers[i].delay = v);
                    Num("respawn", dd => dd.fallers[i].respawn, (dd, v) => dd.fallers[i].respawn = v);
                    break;
                case LayoutElementKind.SpikeTrap:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x (왼쪽 끝)", dd => dd.spikeTraps[i].x, (dd, v) => dd.spikeTraps[i].x = v);
                    Num("y (바닥 윗면)", dd => dd.spikeTraps[i].y, (dd, v) => dd.spikeTraps[i].y = v);
                    Num("w (m, 1.5 m 유닛으로 분할)", dd => dd.spikeTraps[i].w, (dd, v) => dd.spikeTraps[i].w = v);
                    Num("period (주기 s)", dd => dd.spikeTraps[i].period, (dd, v) => dd.spikeTraps[i].period = v);
                    Num("upTime (솟은 시간 s)", dd => dd.spikeTraps[i].upTime, (dd, v) => dd.spikeTraps[i].upTime = v);
                    Num("warn (예고 s)", dd => dd.spikeTraps[i].warn, (dd, v) => dd.spikeTraps[i].warn = v);
                    Num("offset (주기 어긋남 s)", dd => dd.spikeTraps[i].offset, (dd, v) => dd.spikeTraps[i].offset = v);
                    break;
                case LayoutElementKind.Switch:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x (중앙)", dd => dd.switches[i].x, (dd, v) => dd.switches[i].x = v);
                    Num("y (바닥 윗면)", dd => dd.switches[i].y, (dd, v) => dd.switches[i].y = v);
                    IntNum("channel", dd => dd.switches[i].channel, (dd, v) => dd.switches[i].channel = v);
                    Toggle("once (켜면 한 번 밟으면 유지)", dd => dd.switches[i].once, (dd, v) => dd.switches[i].once = v);
                    break;
                case LayoutElementKind.Gate:
                    IdField(d, sel);
                    SectionField(d, sel);
                    Num("x (중앙, 폭 1 m)", dd => dd.gates[i].x, (dd, v) => dd.gates[i].x = v);
                    Num("y (바닥 윗면)", dd => dd.gates[i].y, (dd, v) => dd.gates[i].y = v);
                    Num("h (높이 m)", dd => dd.gates[i].h, (dd, v) => dd.gates[i].h = v);
                    IntNum("channel (같은 번호 스위치로 열림)", dd => dd.gates[i].channel, (dd, v) => dd.gates[i].channel = v);
                    Num("openSeconds", dd => dd.gates[i].openSeconds, (dd, v) => dd.gates[i].openSeconds = v);
                    break;
                case LayoutElementKind.Boss:
                    BossIdField(d);
                    Num("x (보스 중앙)", dd => dd.boss.x, (dd, v) => dd.boss.x = v);
                    Num("y (아레나 바닥 윗면)", dd => dd.boss.y, (dd, v) => dd.boss.y = v);
                    Num("arenaMinX (입구 게이트 위치)", dd => dd.boss.arenaMinX, (dd, v) => dd.boss.arenaMinX = v);
                    Num("arenaMaxX", dd => dd.boss.arenaMaxX, (dd, v) => dd.boss.arenaMaxX = v);
                    break;
                case LayoutElementKind.Decoration:
                    IdField(d, sel);
                    DecorationPropField(d, i);
                    Num("x", dd => dd.decorations[i].x, (dd, v) => dd.decorations[i].x = v);
                    Num("y", dd => dd.decorations[i].y, (dd, v) => dd.decorations[i].y = v);
                    Num("z (깊이)", dd => controller.Props.ResolveZ(dd.decorations[i]), (dd, v) => dd.decorations[i].z = v);
                    DepthPresets(i);
                    Num("rotY (도)", dd => dd.decorations[i].rotY, (dd, v) => dd.decorations[i].rotY = v);
                    Num("scale (균등, > 0)", dd => dd.decorations[i].scale, (dd, v) => dd.decorations[i].scale = v);
                    break;
            }
        }

        // ------------------------------------------------------------------ decoration fields

        private static readonly (string label, float z)[] DepthLayers =
        {
            ("플레이 0", 0f), ("근경 8", 8f), ("중경 30", 30f), ("원경 70", 70f),
        };

        private void DepthPresets(int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("깊이 프리셋");
                foreach (var (label, z) in DepthLayers)
                {
                    if (!GUILayout.Button(label, EditorStyles.miniButton))
                        continue;
                    float value = z;
                    controller.ApplyEdit("Map Tool: Edit z", dd =>
                    {
                        if (index >= dd.decorations.Length || Mathf.Approximately(controller.Props.ResolveZ(dd.decorations[index]), value))
                            return false;
                        dd.decorations[index].z = value;
                        return true;
                    });
                }
            }
        }

        // The prop is chosen from the Implemented rows; a stored Planned / unknown prop stays visible and is kept (the validator reports it).
        private void DecorationPropField(StageLayoutData d, int index)
        {
            string current = d.decorations[index].prop ?? "";
            var props = controller.Props;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("prop");
                if (EditorGUILayout.DropdownButton(new GUIContent(props.LabelFor(current)), FocusType.Keyboard))
                {
                    if (!props.Available || props.Implemented.Count == 0)
                    {
                        controller.SetStatus(props.Available ? "선택할 수 있는 prop이 없습니다 (Implemented 상태 + 프리팹 필요)." : "prop 테이블을 읽을 수 없습니다: " + props.FailureReason, true);
                    }
                    else
                    {
                        ShowPropMenu(id => controller.ApplyEdit("Map Tool: Edit prop", dd =>
                        {
                            if (index >= dd.decorations.Length || dd.decorations[index].prop == id)
                                return false;
                            dd.decorations[index].prop = id;
                            return true;
                        }));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ field helpers

        private void Num(string label, Func<StageLayoutData, float> get, Action<StageLayoutData, float> set, float labelWidth = 0f)
        {
            float current = get(controller.Data);
            float prevWidth = EditorGUIUtility.labelWidth;
            if (labelWidth > 0f)
                EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.DelayedFloatField(label, current);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = prevWidth;
            if (!changed)
                return;
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                controller.SetStatus("유효한 숫자가 아닙니다.", true);
                return;
            }
            controller.ApplyEdit("Map Tool: Edit " + label, dd =>
            {
                if (Mathf.Approximately(get(dd), v))
                    return false;
                set(dd, v);
                return true;
            });
        }

        private void IntNum(string label, Func<StageLayoutData, int> get, Action<StageLayoutData, int> set)
        {
            EditorGUI.BeginChangeCheck();
            int v = EditorGUILayout.DelayedIntField(label, get(controller.Data));
            if (!EditorGUI.EndChangeCheck())
                return;
            controller.ApplyEdit("Map Tool: Edit " + label, dd =>
            {
                if (get(dd) == v)
                    return false;
                set(dd, v);
                return true;
            });
        }

        private void Toggle(string label, Func<StageLayoutData, bool> get, Action<StageLayoutData, bool> set)
        {
            EditorGUI.BeginChangeCheck();
            bool v = EditorGUILayout.Toggle(label, get(controller.Data));
            if (!EditorGUI.EndChangeCheck())
                return;
            controller.ApplyEdit("Map Tool: Edit " + label, dd =>
            {
                if (get(dd) == v)
                    return false;
                set(dd, v);
                return true;
            });
        }

        // Boss: pick from Data/Bosses/Boss_{id}.asset (BossData); an id without an asset stays visible and is reported by the validator.
        private void BossIdField(StageLayoutData d)
        {
            var ids = BossCatalog.FindIds();
            int current = d.boss.bossId;
            var labels = new List<string>();
            foreach (int id in ids)
                labels.Add(BossCatalog.LabelFor(id));
            int idx = ids.IndexOf(current);
            if (idx < 0)
            {
                labels.Add($"(BossData missing) {current}");
                ids.Add(current);
                idx = ids.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("bossId", idx, labels.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx)
            {
                int value = ids[picked];
                controller.ApplyEdit("Map Tool: Edit bossId", dd => { dd.boss.bossId = value; return true; });
            }
        }

        private void TextField(string label, string current, Action<StageLayoutData, string> set)
        {
            EditorGUI.BeginChangeCheck();
            string v = EditorGUILayout.DelayedTextField(label, current ?? "");
            if (!EditorGUI.EndChangeCheck())
                return;
            controller.ApplyEdit("Map Tool: Edit " + label, dd =>
            {
                set(dd, v);
                return true;
            });
        }

        private void Choice(string label, string current, string[] options, Action<StageLayoutData, string> set)
        {
            int idx = Array.IndexOf(options, current);
            var shown = new List<string>(options);
            if (idx < 0)
            {
                shown.Add("(invalid) " + current);
                idx = shown.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(label, idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked < options.Length && picked != idx)
                controller.ApplyEdit("Map Tool: Edit " + label, dd => { set(dd, options[picked]); return true; });
        }

        // id edit: a duplicate / empty id is refused (the model stays unchanged) and reported in the status line.
        private void IdField(StageLayoutData d, LayoutElementRef sel)
        {
            string current = StageLayoutEditOps.GetId(d, sel) ?? "";
            EditorGUI.BeginChangeCheck();
            string v = EditorGUILayout.DelayedTextField("id", current);
            if (!EditorGUI.EndChangeCheck() || v == current)
                return;
            bool ok = false;
            controller.ApplyEdit("Map Tool: Rename " + current, dd =>
            {
                ok = StageLayoutEditOps.RenameId(dd, sel, v);
                return ok;
            });
            if (!ok)
                controller.SetStatus($"id를 '{v}'로 바꿀 수 없습니다 (비어 있거나 이미 사용 중).", true);
        }

        private void SectionField(StageLayoutData d, LayoutElementRef sel)
        {
            string current = StageLayoutEditOps.GetSection(d, sel) ?? "";
            var names = new List<string> { "(없음)" };
            foreach (var s in d.sections)
                names.Add(s.id);
            int idx = string.IsNullOrEmpty(current) ? 0 : names.IndexOf(current);
            if (idx < 0)
            {
                names.Add("(unknown) " + current);
                idx = names.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("section", idx, names.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx && picked <= d.sections.Length)
            {
                string value = picked == 0 ? "" : d.sections[picked - 1].id;
                controller.ApplyEdit("Map Tool: Edit section", dd => { StageLayoutEditOps.SetSection(dd, sel, value); return true; });
            }
        }

        private void SlotField(StageLayoutData d, int index)
        {
            var options = new string[StageSession.SpecialSlotCount];
            for (int s = 0; s < options.Length; s++)
                options[s] = s.ToString();
            int current = d.specials[index].slot;
            var shown = new List<string>(options);
            int idx = current;
            if (current < 0 || current >= options.Length)
            {
                shown.Add("(invalid) " + current);
                idx = shown.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("slot", idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked < options.Length && picked != idx)
                controller.ApplyEdit("Map Tool: Edit slot", dd => { dd.specials[index].slot = picked; return true; });
        }

        // MT-012: only Implemented rows are offered; a stored Planned / Deprecated / unknown id stays visible and is preserved.
        private void MonsterField(StageLayoutData d, int index)
        {
            int current = d.enemies[index].monsterId;
            if (!controller.Monsters.Available)
            {
                EditorGUI.BeginChangeCheck();
                int typed = EditorGUILayout.DelayedIntField("monsterId (0 = 기본)", current);
                if (EditorGUI.EndChangeCheck())
                    controller.ApplyEdit("Map Tool: Edit monsterId", dd => { dd.enemies[index].monsterId = typed; return true; });
                EditorGUILayout.HelpBox("몬스터 테이블을 읽을 수 없어 숫자 입력으로 대체합니다: " + controller.Monsters.FailureReason, MessageType.Warning);
                return;
            }

            var labels = new List<string> { controller.Monsters.LabelFor(0) };
            var ids = new List<int> { 0 };
            foreach (var c in controller.Monsters.Implemented)
            {
                labels.Add(c.Label);
                ids.Add(c.Id);
            }
            int idx = ids.IndexOf(current);
            if (idx < 0)
            {
                labels.Add(controller.Monsters.LabelFor(current));
                ids.Add(current);
                idx = ids.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("monsterId", idx, labels.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx)
            {
                int value = ids[picked];
                controller.ApplyEdit("Map Tool: Edit monsterId", dd => { dd.enemies[index].monsterId = value; return true; });
            }
        }

        // ------------------------------------------------------------------ section push (P2, MT-023)

        private void DrawShiftTool()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("구간 밀기 (x ≥ X 인 모든 것을 dx만큼 이동)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                shiftAtX = EditorGUILayout.FloatField("X", shiftAtX);
                shiftDx = EditorGUILayout.FloatField("dx", shiftDx);
                if (GUILayout.Button("적용", GUILayout.Width(50f)))
                {
                    float x = shiftAtX;
                    float dx = shiftDx;
                    Defer(() => ApplyShift(x, dx));
                }
            }
        }

        private void ApplyShift(float atX, float dx)
        {
            string message = null;
            bool ok = false;
            controller.ApplyEdit($"Map Tool: Shift from x={atX:0.##} by {dx:0.##}", d =>
            {
                ok = StageLayoutEditOps.ShiftFrom(d, atX, dx, out message);
                return ok;
            });
            if (ok)
                controller.SetStatus($"구간 밀기: x ≥ {atX:0.##} 를 {dx:0.##} m 이동했습니다.");
            else
                controller.SetStatus(message ?? "구간 밀기를 적용할 수 없습니다.", true);
        }
    }
}
