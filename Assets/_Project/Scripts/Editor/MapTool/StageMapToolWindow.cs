using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Map tool window (view). Menu: Tools/MyGame/Map Tool/Open Map Tool. Panels are in StageMapToolWindow.Panels.cs and
    /// the property editor in StageMapToolWindow.Inspector.cs. All model changes go through <see cref="StageMapController"/>;
    /// state-changing buttons are deferred (EditorApplication.delayCall) so the IMGUI layout is never changed mid-event.
    /// </summary>
    internal sealed partial class StageMapToolWindow : EditorWindow, IStageMapPrompts
    {
        // Serialized so they survive script reloads and Play Mode entry (MT-015).
        [SerializeField] private StageMapSession session = new StageMapSession();
        [SerializeField] private StageLayoutDocument document;

        private StageMapController controller;
        private StageMapSceneOverlay overlay;
        private Vector2 scroll;

        [MenuItem("Tools/MyGame/Map Tool/Open Map Tool")]
        private static void OpenWindow()
        {
            var window = GetWindow<StageMapToolWindow>("Map Tool");
            window.Show();
            window.Focus();
        }

        internal bool IsDirty => controller != null && controller.IsDirty;
        internal string StatusText => controller != null ? controller.StatusMessage : "";

        /// <summary>Opens a file for the automation API (the caller has checked that nothing is dirty).</summary>
        internal bool OpenFromAutomation(string assetPath)
        {
            return controller != null && controller.Open(assetPath);
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            titleContent = new GUIContent("Map Tool");
            minSize = new Vector2(380f, 460f);

            controller = new StageMapController(session, document, this);
            document = controller.Document;
            controller.Changed += OnControllerChanged;
            controller.ValidationUpdated += Repaint;
            overlay = new StageMapSceneOverlay(controller);
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SyncUnsavedState();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Undo.undoRedoPerformed -= Repaint;
            if (overlay != null)
            {
                overlay.Dispose();
                overlay = null;
            }
            if (controller != null)
            {
                controller.Changed -= OnControllerChanged;
                controller.ValidationUpdated -= Repaint;
                controller.Dispose();
                controller = null;
            }
        }

        // Only runs when the window is really closed (not on a script reload): release the transient document.
        private void OnDestroy()
        {
            if (document != null)
            {
                Undo.ClearUndo(document);
                DestroyImmediate(document);
                document = null;
            }
        }

        private void OnFocus()
        {
            if (controller == null)
                return;
            controller.Monsters.Refresh();
            controller.Props.Refresh();
            RefreshThemeNames();
            controller.CheckExternalChange();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnControllerChanged()
        {
            SyncUnsavedState();
            Repaint();
        }

        private void SyncUnsavedState()
        {
            bool dirty = controller != null && controller.IsDirty;
            if (hasUnsavedChanges != dirty)
                hasUnsavedChanges = dirty;
            saveChangesMessage = controller != null && controller.HasDocument
                ? $"'{controller.FileName}'에 저장되지 않은 변경 사항이 있습니다."
                : "";
        }

        // Unity asks "save / discard / cancel" when the window closes with hasUnsavedChanges set.
        public override void SaveChanges()
        {
            if (controller != null && controller.Save())
                base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            base.DiscardChanges();
        }

        // ------------------------------------------------------------------ IStageMapPrompts

        public PromptChoice Ask(string title, string message, string primary, string secondary, string cancel)
        {
            if (string.IsNullOrEmpty(secondary))
                return EditorUtility.DisplayDialog(title, message, primary, cancel) ? PromptChoice.Primary : PromptChoice.Cancel;
            // DisplayDialogComplex: 0 = first button (ok), 1 = second (cancel), 2 = third (alt).
            int r = EditorUtility.DisplayDialogComplex(title, message, primary, cancel, secondary);
            return r == 0 ? PromptChoice.Primary : r == 2 ? PromptChoice.Secondary : PromptChoice.Cancel;
        }

        public bool Confirm(string title, string message, string ok)
        {
            return EditorUtility.DisplayDialog(title, message, ok, "취소");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Runs <paramref name="action"/> after the current IMGUI event (dialogs, scene changes and structure changes are safe there).</summary>
        private void Defer(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && controller != null)
                    action();
            };
        }

        private void HandleWindowKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || controller == null || !controller.HasDocument)
                return;
            bool mod = e.control || e.command;
            if (mod && e.keyCode == KeyCode.S)
            {
                e.Use();
                Defer(() => controller.Save());
            }
            else if (mod && e.keyCode == KeyCode.D && GUIUtility.keyboardControl == 0)
            {
                e.Use();
                Defer(() => controller.DuplicateSelected());
            }
            else if (!mod && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && GUIUtility.keyboardControl == 0)
            {
                e.Use();
                Defer(() => controller.DeleteSelected());
            }
        }

        private static void OpenEmptyCanvas()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private void OnGUI()
        {
            if (controller == null)
                return;
            HandleWindowKeys();

            bool locked = StageMapController.IsLocked;
            DrawFileBar(locked);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!controller.HasDocument)
            {
                EditorGUILayout.HelpBox("열린 레이아웃이 없습니다. '새로 만들기' 또는 '열기'를 사용하세요.", MessageType.Info);
            }
            else
            {
                if (locked)
                    EditorGUILayout.HelpBox("Play Mode 또는 컴파일 중에는 표시만 되며 편집·저장·빌드가 잠깁니다.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(locked))
                {
                    DrawStagePanel();
                    DrawPalettePanel();
                    DrawElementListPanel();
                    DrawInspectorPanel();
                    DrawValidationPanel();
                    DrawBuildPanel(locked);
                }
            }
            EditorGUILayout.EndScrollView();
            DrawStatusBar();
        }
    }
}
