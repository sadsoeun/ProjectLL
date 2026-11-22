using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Adds the third-person WorldMapCameraRig next to the map camera's CameraFollow and wires the controller.
    /// Idempotent (tuned rig values are kept). Menu: Tools/MyGame/World Map/Setup Third-Person Camera.
    /// Also called by WorldMapBuilder.Build.
    /// </summary>
    public static class WorldMapCameraSetup
    {
        [MenuItem("Tools/MyGame/World Map/Setup Third-Person Camera")]
        public static void SetupMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WorldMapBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(WorldMapBuilder.ScenePath, OpenSceneMode.Additive);
            var ctrl = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldMapController>(true)).FirstOrDefault()
                       ?? throw new InvalidOperationException("05_WorldMap has no WorldMapController.");
            Debug.Log(Apply(ctrl));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }

        public static string Apply(WorldMapController ctrl)
        {
            var follow = (CameraFollow)new SerializedObject(ctrl).FindProperty("cameraFollow").objectReferenceValue
                         ?? throw new InvalidOperationException("WorldMapController.cameraFollow is not assigned.");
            var rig = follow.GetComponent<WorldMapCameraRig>();
            bool added = rig == null;
            if (added)
                rig = Undo.AddComponent<WorldMapCameraRig>(follow.gameObject);
            EditorWire.Set(ctrl, "cameraRig", rig);
            EditorWire.Set(ctrl, "thirdPersonCamera", true);
            return $"[WorldMapCameraSetup] rig {(added ? "added to" : "kept on")} '{follow.name}', controller wired";
        }
    }
}
