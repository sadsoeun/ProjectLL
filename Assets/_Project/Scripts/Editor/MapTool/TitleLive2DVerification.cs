using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    [InitializeOnLoad]
    public static class TitleLive2DVerification
    {
        static string Work => Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/HudReadabilityWork");
        static double next;
        static TitleLive2DVerification(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+1;
            string request=Path.Combine(Work,"title-check.request");
            if(!File.Exists(request)||!EditorApplication.isPlaying)return;
            var live=UnityEngine.Object.FindFirstObjectByType<TitleLive2DCharacter>();
            if(live==null||live.Model==null)return;
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                var output=Path.Combine(Work,"Verification");Directory.CreateDirectory(output);
                if(command=="check")
                {
                    var model=live.Model;
                    if(model.Moc==null)throw new Exception("Native Cubism moc missing");
                    if(!model.Parameters.Any(p=>p.Id=="ParamEyeLOpen")||!model.Parameters.Any(p=>p.Id=="ParamBreath"))throw new Exception("Rig parameters missing");
                    live.SetPose(1,0);model.ForceUpdateNow();
                    var before=model.Drawables.Select(d=>d.VertexPositions.ToArray()).ToArray();
                    live.SetPose(0,1);model.ForceUpdateNow();
                    float delta=0;
                    for(int d=0;d<before.Length;d++)for(int v=0;v<before[d].Length;v++)delta=Mathf.Max(delta,Vector3.Distance(before[d][v],model.Drawables[d].VertexPositions[v]));
                    if(delta<.0001f)throw new Exception("Breath parameter does not deform the model");
                    live.SetPose(1,0);model.ForceUpdateNow();
                    File.WriteAllText(Path.Combine(output,"live2d-verification.txt"),"PASS: genuine Cubism moc loaded; eye and breath parameters present; "+model.Drawables.Length+" ArtMeshes; breath vertex delta="+delta+".\n");
                }
                else if(command=="closed"||command=="open")
                {
                    // Hold the existing two cel drawings while capturing the actual Game view.
                    typeof(TitleLive2DCharacter).GetField("nextBlink",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(live,float.MaxValue);
                    typeof(TitleLive2DCharacter).GetField("blinkEnd",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(live,command=="closed"?float.MaxValue:-1f);
                }
                else if(command=="resume")
                {
                    float elapsed=(float)typeof(TitleLive2DCharacter).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(live);
                    typeof(TitleLive2DCharacter).GetField("nextBlink",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(live,elapsed+2.8f);
                    typeof(TitleLive2DCharacter).GetField("blinkEnd",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(live,-1f);
                }
                else if(command.StartsWith("capture-"))
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,command+".png"));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Work,"Verification/live2d-error.txt"),e.ToString());Debug.LogException(e);}
        }
    }
}
