using UnityEngine;
namespace MyGame
{
    /// <summary>One silhouette shared by the collectible mesh and its UI symbol.</summary>
    public static class GoldenLeafShape
    {
        public static readonly Vector2[] Outline = {
            new Vector2(-.16f,-.34f),new Vector2(.12f,-.26f),new Vector2(.29f,-.08f),
            new Vector2(.34f,.15f),new Vector2(.28f,.43f),new Vector2(.07f,.34f),
            new Vector2(-.16f,.20f),new Vector2(-.28f,-.02f),new Vector2(-.25f,-.20f)};
        public static readonly Vector2 Center=new Vector2(-.01f,.01f);
        public static readonly Vector2[] Veins={
            new Vector2(-.30f,-.49f),new Vector2(-.16f,-.34f),
            new Vector2(-.16f,-.34f),new Vector2(-.01f,.01f),
            new Vector2(-.01f,.01f),new Vector2(.28f,.43f),
            new Vector2(-.08f,-.16f),new Vector2(.15f,-.12f),
            new Vector2(-.08f,-.16f),new Vector2(-.22f,-.09f),
            new Vector2(.02f,.06f),new Vector2(.27f,.13f),
            new Vector2(.02f,.06f),new Vector2(-.14f,.18f),
            new Vector2(.13f,.22f),new Vector2(.27f,.29f)};
    }
}
