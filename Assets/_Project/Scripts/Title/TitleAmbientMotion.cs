using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Layered title scenery in the same fitted frame as the illustration. UI stays stationary.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class TitleAmbientMotion : MonoBehaviour
    {
        RawImage background, rotor;
        RawImage[] clouds, leaves;
        RectTransform frame;
        TitleHairWind hair;
        TitleExplorerCharacter character;
        float cameraZoom=1;
        Vector2 cameraPan;
        float elapsed;
        public float Elapsed => elapsed;

        public bool Initialize(RawImage illustration)
        {
            if(background!=null)return true;
            var plate=Resources.Load<Texture2D>("TitleArt/MeadowMotionBackground");
            var cloud=Resources.Load<Texture2D>("TitleArt/Cloud");
            var leaf=Resources.Load<Texture2D>("TitleArt/GoldenLeaf");
            var blades=Resources.Load<Texture2D>("TitleArt/WindmillRotor");
            if(plate==null||cloud==null||leaf==null||blades==null)return false;
            background=illustration; background.texture=plate;
            frame=GoldUiTheme.Rect("TitleWeather",illustration.transform);
            GoldUiTheme.Stretch(frame);frame.SetAsFirstSibling();
            frame.gameObject.AddComponent<RectMask2D>();
            clouds=new[]{Sprite("CloudFar",cloud),Sprite("CloudNear",cloud),Sprite("CloudSmall",cloud)};
            rotor=Sprite("WindmillRotor",blades);
            leaves=new[]{Sprite("DriftingLeafA",leaf),Sprite("DriftingLeafB",leaf),Sprite("DriftingLeafC",leaf)};
            hair=illustration.GetComponentInChildren<TitleHairWind>(true);
            character=GetComponent<TitleExplorerCharacter>();
            Sample(0);
            return true;
        }
        RawImage Sprite(string name,Texture texture)
        {
            var rect=GoldUiTheme.Rect(name,frame);
            var image=rect.gameObject.AddComponent<RawImage>();
            image.texture=texture;image.raycastTarget=false;
            return image;
        }
        void Update()
        {
            if(!IntroTitleTransition.IsActive && (!GameSystems.Exists || !GameSystems.Instance.Loader.IsBusy))
                elapsed+=Time.unscaledDeltaTime;
        }
        void LateUpdate(){Sample(elapsed);}
        public void Sample(float seconds)
        {
            elapsed=seconds;
            if(background==null)return;
            float t=elapsed;
            // One shared camera pose moves the scene, character and weather together.
            // Overscan always exceeds the pan distance, so no empty edges can appear.
            CameraPose(t,out cameraZoom,out cameraPan);
            float span=1/cameraZoom;
            var center=new Vector2(.5f-cameraPan.x/(1920f*cameraZoom),.5f-cameraPan.y/(1080f*cameraZoom));
            background.uvRect=new Rect(center.x-span*.5f,center.y-span*.5f,span,span);
            if(character!=null)character.ApplyCamera(cameraZoom,cameraPan);
            Cloud(clouds[0],t,1120,94,375,8.5f,.83f);
            Cloud(clouds[1],t,1530,160,455,12f,.93f);
            Cloud(clouds[2],t,1840,70,270,6.5f,.75f);
            // The plate's axle is at (1577,291) in its 1672x941 source.
            var axle=new Vector2(1577f/1672f,1-291f/941f);
            Place(rotor,new Vector2(axle.x*1920,(1-axle.y)*1080),new Vector2(191,191));
            rotor.rectTransform.localRotation=Quaternion.Euler(0,0,32-t*8);
            for(int i=0;i<leaves.Length;i++)
            {
                float phase=Mathf.Repeat(t/(19+i*4)+i*.32f,1);
                float x=Mathf.Lerp(1940,870,phase);
                float y=310+i*160+phase*150+Mathf.Sin(t*.85f+i*2)*33;
                float size=29+i*9;
                Place(leaves[i],new Vector2(x,y),new Vector2(size,size));
                leaves[i].rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*1.2f+i)*34+phase*90);
                leaves[i].rectTransform.localScale=new Vector3(.75f+.25f*Mathf.Sin(t*1.3f+i),1,1);
                float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(phase/.09f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-phase)/.13f));
                leaves[i].color=new Color(1,1,1,fade*.72f);
            }
            if(hair!=null)hair.Sample(t);
        }
        public static void CameraPose(float seconds,out float zoom,out Vector2 pan)
        {
            zoom=1.035f+.015f*(.5f-.5f*Mathf.Cos(seconds*Mathf.PI*2/32f));
            pan=new Vector2(24*Mathf.Sin(seconds*Mathf.PI*2/28f),6*Mathf.Sin(seconds*Mathf.PI*2/36f));
        }
        void Cloud(RawImage cloud,float t,float start,float y,float width,float speed,float opacity)
        {
            float x=940+Mathf.Repeat(start-940+t*speed,1220);
            Place(cloud,new Vector2(x,y),new Vector2(width,width*cloud.texture.height/cloud.texture.width));
            float a=Mathf.SmoothStep(0,1,Mathf.Clamp01((x-940)/160))*Mathf.SmoothStep(0,1,Mathf.Clamp01((2160-x)/180));
            cloud.color=new Color(1,1,1,a*opacity);
        }
        void Place(RawImage image,Vector2 positionFromTopLeft,Vector2 size)
        {
            float scale=frame.rect.height/1080f;
            image.rectTransform.anchoredPosition=(new Vector2(positionFromTopLeft.x-960,540-positionFromTopLeft.y)*cameraZoom+cameraPan)*scale;
            image.rectTransform.sizeDelta=size*cameraZoom*scale;
        }
    }
}

