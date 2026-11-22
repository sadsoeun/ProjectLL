using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace MyGame.EditorTools
{
    // Presentation-only, explicit batch entry point. Never opens or focuses a desktop window.
    public static class PresentationCaptureBatch
    {
        public static void CaptureMap()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            string dir=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/PresentationWork/Captures");
            var hostType=typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
            var guiType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            var host=ScriptableObject.CreateInstance(hostType);
            var map=ScriptableObject.CreateInstance<StageMapToolWindow>();
            var bounds=new Rect(0,0,660,1400);
            guiType.GetMethod("Internal_Init",flags).Invoke(host,new object[]{24,1,false,false});
            hostType.GetMethod("SetActualViewInternal",flags).Invoke(host,new object[]{map,false});
            hostType.GetMethod("SetPosition",flags).Invoke(host,new object[]{bounds});
            map.OpenFromAutomation("Assets/_Project/Data/StageLayouts/W1_S1.json");
            var controller=(StageMapController)typeof(StageMapToolWindow).GetField("controller",flags).GetValue(map);
            controller.Select(new LayoutElementRef(LayoutElementKind.Block,1));
            controller.RunValidation();
            SaveView(host,bounds,Path.Combine(dir,"maptool-actual.png"));
            var scene=ScriptableObject.CreateInstance<SceneView>();
            var sceneHost=ScriptableObject.CreateInstance(hostType);
            guiType.GetMethod("Internal_Init",flags).Invoke(sceneHost,new object[]{24,1,false,false});
            hostType.GetMethod("SetActualViewInternal",flags).Invoke(sceneHost,new object[]{scene,false});
            var sceneBounds=new Rect(0,0,1400,850);
            hostType.GetMethod("SetPosition",flags).Invoke(sceneHost,new object[]{sceneBounds});
            scene.in2DMode=true;
            scene.LookAt(new Vector3(22,2,0),Quaternion.identity,10,true,true);
            SaveView(sceneHost,sceneBounds,Path.Combine(dir,"maptool-scene.png"));
            File.WriteAllText(Path.Combine(dir,"maptool-validation.txt"),controller.FilePath+"\nerrors="+controller.ErrorCount+" warnings="+controller.WarningCount+"\n"+string.Join("\n",controller.Issues.Select(i=>i.Message)));
            UnityEngine.Object.DestroyImmediate(sceneHost);UnityEngine.Object.DestroyImmediate(scene);
            UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(map);
        }
        private static void SaveView(ScriptableObject host,Rect bounds,string path)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var guiType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            int width=(int)bounds.width,height=(int)bounds.height;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.Create();
            guiType.GetMethod("SendEvent",flags).Invoke(host,new object[]{new Event{type=EventType.Layout}});
            guiType.GetMethod("RepaintImmediately",flags).Invoke(host,null);
            guiType.GetMethod("GrabPixels",flags).Invoke(host,new object[]{rt,bounds});
            RenderTexture.active=rt;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            tex.ReadPixels(bounds,0,0);tex.Apply();
            var raw=tex.GetPixels32();var upright=new Color32[raw.Length];
            for(int y=0;y<height;y++)Array.Copy(raw,y*width,upright,(height-1-y)*width,width);
            tex.SetPixels32(upright);tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG());
            RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        public static void Probe()
        {
            string dir=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/PresentationWork/Captures");
            Directory.CreateDirectory(dir);
            string result="batch="+Application.isBatchMode+"\n"+SystemInfo.graphicsDeviceName;
            foreach(string name in new[]{"UnityEditor.GUIView","UnityEditor.HostView","UnityEditor.View"}) {
                var t=typeof(EditorWindow).Assembly.GetType(name);
                result+="\n"+name+"\n"+string.Join("\n",t.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Select(m=>m.ToString()));
            }
            File.WriteAllText(Path.Combine(dir,"unity-probe.txt"),result);
        }
    }
}
