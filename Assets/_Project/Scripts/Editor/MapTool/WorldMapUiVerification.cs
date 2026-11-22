using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    [InitializeOnLoad]
    public static class WorldMapUiVerification
    {
        static string Work => Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/WorldMapUiWork");
        static double next;
        static WorldMapUiVerification() { EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+1;
            string request=Path.Combine(Work,"verify.request");
            if(!File.Exists(request)||EditorApplication.isPlaying)return;
            File.Delete(request);
            try { Verify(); } catch(Exception e) {File.WriteAllText(Path.Combine(Work,"error.txt"),e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/MyGame/Verify World Map UI")]
        public static void Verify()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/05_WorldMap.unity");
            var pipeline=GraphicsSettings.defaultRenderPipeline; var quality=QualitySettings.renderPipeline;
            var previous=RenderTexture.active;
            try
            {
                GraphicsSettings.defaultRenderPipeline=null; QualitySettings.renderPipeline=null;
                var roots=scene.GetRootGameObjects();
                var canvas=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.name=="WorldMapCanvas");
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
                camera.scene=scene; camera.enabled=false; camera.cullingMask=1<<5;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.15f,.22f,.29f);
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                var root=canvas.transform; var pause=root.Find("PauseMenu");
                pause.gameObject.SetActive(false);root.Find("MessageText").gameObject.SetActive(false);
                GoldUiTheme.Apply(root); var view=root.GetComponent<WorldMapPresentation>();
                int count=root.GetComponentsInChildren<Button>(true).Length;
                GoldUiTheme.Apply(root);
                Require(count==root.GetComponentsInChildren<Button>(true).Length,"Duplicate buttons");
                var window=pause.Find("Window");
                CheckPage(window,new[]{"ResumeButton","VoiceStudioButton","SettingsButton","TitleButton"});
                foreach(var retired in new[]{"MoreButton","BackButton","SaveButton","LoadButton","QuitButton"})
                { var row=window.Find(retired); Require(row==null||!row.gameObject.activeSelf,retired+" must stay hidden"); }
                Localize(root);
                var stageIds = roots.SelectMany(r => r.GetComponentsInChildren<WorldMapNode>(true))
                    .Select(n => n.Data.Stage.StageId).OrderBy(id => id.EndsWith("_B") ? id.Replace("_B", "_Z") : id).ToArray();
                var encounters = Resources.Load<WorldMapEncounterAtlas>("WorldMapEncounters");
                Require(encounters && encounters.texture, "Encounter silhouette atlas missing");
                foreach (var id in stageIds)
                {
                    var models = encounters.ModelsFor(id);
                    Require(encounters.stages.Any(s => s.stageId == id), $"Missing encounter data on {id}");
                    if (id.EndsWith("_B")) Require(models.Length > 0, $"Missing boss on {id}");
                    Require(models.All(m => (encounters.creatureIds[m] >= 9000) == id.EndsWith("_B")), $"Wrong creature on {id}");
                }
                view.SetEncounterStages(stageIds);
                int stopCount = stageIds.Length;
                view.SetRoutePosition(0, stopCount);
                view.SetRoutePosition(2, stopCount);
                var carousel = root.GetComponentInChildren<WorldMapOrbitCarousel>();
                carousel.Advance(.2f);
                carousel.Advance(2f);
                Require(carousel.transform.Find("PlateNumber1").GetComponent<Text>().text == "03", "Route did not advance");
                view.SetRoutePosition(1, stopCount);
                carousel.Advance(2f);
                Require(carousel.transform.Find("PlateNumber1").GetComponent<Text>().text == "02", "Route did not reverse");
                view.SetRoutePosition(2, stopCount);
                carousel.Advance(2f);
                Set(root,"StageInfoPanel/StageNameText","1-3 초원 산책로");
                Set(root,"StageInfoPanel/StateText","클리어");
                Set(root,"StageInfoPanel/BestTimeText","0:42.35");
                Set(root,"StageInfoPanel/SpecialsText","2 / 3");
                var slots = root.Find("StageInfoPanel/SpecialSlots");
                for (int i = 0; i < slots.childCount; i++)
                    CollectibleSlot.Set(slots.GetChild(i).GetComponent<Image>(), i < 2);
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(2560,1080),new Vector2Int(1024,768)})
                    Capture(camera,canvas,view,size,"map-"+size.x+"x"+size.y);
                view.SetMenuVisible(true);pause.gameObject.SetActive(true);Localize(root);
                Require(!root.Find("StageInfoPanel").gameObject.activeSelf&&!root.Find("MenuButton").gameObject.activeSelf,"HUD overlaps menu");
                Capture(camera,canvas,view,new Vector2Int(1920,1080),"menu-primary");
                view.SetMenuVisible(false);pause.gameObject.SetActive(false);
                Require(root.Find("StageInfoPanel").gameObject.activeSelf&&root.Find("MenuButton").gameObject.activeSelf,"HUD did not restore");
                File.WriteAllText(Path.Combine(Work,"verification.txt"),"PASS: Unity compilation; idempotent UI; single-page menu (more/save/load/quit rows hidden); navigation restricted to visible controls; HUD hide/restore; 16:9,21:9,4:3 renders. Isolated scene preview.\n");
            }
            finally { RenderTexture.active=previous; EditorSceneManager.ClosePreviewScene(scene); GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=quality; }
        }
        static void Localize(Transform root)
        {
            foreach(var loc in root.GetComponentsInChildren<LocalizedText>(true))
            {
                string key=new SerializedObject(loc).FindProperty("key").stringValue;
                if(LocalizationTable.TryGet("ko",key,out string value))loc.GetComponent<Text>().text=value;
            }
        }
        static void Set(Transform root,string path,string text) => root.Find(path).GetComponent<LocalizedText>().SetText(text);
        static void CheckPage(Transform window,string[] names)
        {
            var buttons=window.GetComponentsInChildren<Button>(true).Where(b=>b.gameObject.activeSelf).ToArray();
            Require(buttons.Length==4&&buttons.All(b=>names.Contains(b.name)),"Wrong menu page");
            foreach(var b in buttons) Require(b.navigation.mode==Navigation.Mode.Explicit&&buttons.Contains(b.navigation.selectOnUp)&&buttons.Contains(b.navigation.selectOnDown),"Hidden button in navigation");
        }
        static void Capture(Camera camera,Canvas canvas,WorldMapPresentation view,Vector2Int size,string name)
        {
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;
            try
            {
                Canvas.ForceUpdateCanvases();view.Layout();
                view.GetComponentInChildren<WorldMapOrbitCarousel>()?.Advance(0);
                foreach(var reveal in canvas.GetComponentsInChildren<GoldUiReveal>(true))reveal.Sample(GoldUiReveal.Duration);
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try {tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Work,name+".png"),tex.EncodeToPNG());}
                finally {UnityEngine.Object.DestroyImmediate(tex);}
            }
            finally {camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    }
}
