using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Live2D.Cubism.Core;

namespace MyGame
{
    /// <summary>Renders the exported Cubism model into the title's fitted design space.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleLive2DCharacter : MonoBehaviour
    {
        const string ModelPath = "TitleLive2D/LeafAndLeap-Cel-v2";
        GameObject rig;
        Camera renderCamera;
        RenderTexture target;
        RawImage image;
        Material composite;
        CubismModel model;
        CubismParameter blink, breath;
        float elapsed, nextBlink = 2.8f, blinkEnd = -1;
        public CubismModel Model => model;
        public Camera RenderCamera => renderCamera;

        public bool Initialize(RectTransform illustration)
        {
            if (rig != null) return true;
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null) { Debug.LogWarning("Title Cubism prefab is missing."); return false; }
            rig = new GameObject("Title Cubism Render Rig");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, gameObject.scene);
            rig.transform.position = new Vector3(10000,10000,0);
            var instance = Instantiate(prefab,rig.transform,false);
            model = instance.GetComponent<CubismModel>();
            if (model == null) { Destroy(rig); rig=null; return false; }
            foreach(var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31;
            foreach(var p in model.Parameters)
            {
                if(p.Id=="ParamEyeLOpen") blink=p;
                if(p.Id=="ParamBreath") breath=p;
            }
            target=new RenderTexture(1024,1536,24,RenderTextureFormat.ARGB32)
            {name="Title Cubism RGBA",antiAliasing=1,filterMode=FilterMode.Bilinear};
            target.Create();
            var cameraObject=new GameObject("Title Cubism Camera");
            cameraObject.transform.SetParent(rig.transform,false);
            cameraObject.transform.localPosition=new Vector3(0,0,-10);
            renderCamera=cameraObject.AddComponent<Camera>();
            renderCamera.orthographic=true; renderCamera.orthographicSize=.75f;
            renderCamera.nearClipPlane=.1f; renderCamera.farClipPlane=20;
            renderCamera.clearFlags=CameraClearFlags.SolidColor; renderCamera.backgroundColor=Color.clear;
            renderCamera.cullingMask=1<<31; renderCamera.targetTexture=target;
            renderCamera.allowHDR=false; renderCamera.allowMSAA=false; renderCamera.depth=-20;
            var data=renderCamera.GetUniversalAdditionalCameraData();
            // Dedicated Cubism renderer is slot 1 in both PC and Mobile pipeline assets.
            data.SetRenderer(1);
            data.renderPostProcessing=false; data.renderShadows=false;
            data.requiresColorOption=CameraOverrideOption.Off; data.requiresDepthOption=CameraOverrideOption.Off;
            var rt=GoldUiTheme.Rect("Live2DCharacter",illustration);
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.pivot=new Vector2(.5f,.5f);
            image=rt.gameObject.AddComponent<RawImage>(); image.raycastTarget=false; image.texture=target;
            var shader=Resources.Load<Shader>("TitleArt/TitlePremultiplied");
            if(shader!=null) {composite=new Material(shader);image.material=composite;}
            SetPose(1,0); Layout(); return true;
        }

        public void SetPose(float eyeOpening,float breathing)
        {
            if(blink!=null)blink.Value=Mathf.Clamp01(eyeOpening);
            if(breath!=null)breath.Value=Mathf.Clamp01(breathing);
        }

        void Update()
        {
            if(model==null)return;
            elapsed+=Time.unscaledDeltaTime;
            if(elapsed>=nextBlink)
            {
                blinkEnd=elapsed+.13f;
                nextBlink=elapsed+Random.Range(3.2f,5.4f);
            }
            // Cel eyelids use two discrete drawings, avoiding a translucent double eye.
            // Keep the body at its original shape until independently layered
            // torso/head/hair rigging is ready; the current whole-body warp looks unnatural.
            SetPose(elapsed<blinkEnd?0:1, 0);
        }

        void LateUpdate() { Layout(); }
        public void Layout()
        {
            if(image==null)return;
            var parent=(RectTransform)image.transform.parent;
            float scale=parent.rect.height/1080f;
            image.rectTransform.anchoredPosition=new Vector2(530,-25)*scale;
            image.rectTransform.sizeDelta=new Vector2(640,960)*scale;
        }
        void OnEnable(){if(rig!=null)rig.SetActive(true);}
        void OnDisable(){if(rig!=null)rig.SetActive(false);}
        void OnDestroy()
        {
            if(rig!=null)Destroy(rig);
            if(target!=null){target.Release();Destroy(target);}
            if(composite!=null)Destroy(composite);
        }
    }
}
