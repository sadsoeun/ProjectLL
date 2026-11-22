#ifndef MYGAME_SCREEN_OUTLINE_MASK_INCLUDED
#define MYGAME_SCREEN_OUTLINE_MASK_INCLUDED

// Screen-space outline mask, written by the project's DepthNormals passes into the alpha channel of
// _CameraNormalsTexture (R8G8B8A8_SNorm; URP itself leaves it at 0) and read by FX_ScreenOutline.shader:
//   0   = regular background geometry (outlined)
//   0.5 = soft surface (wind foliage): interior crease lines are mostly suppressed, silhouettes stay
//   1   = excluded (player / monsters / bosses, which draw their own inverted-hull outline)
// Exclusion is per renderer through the Rendering Layer "ScreenOutlineExclude" (bit 4), so no material changes.
// Requires Core.hlsl (GetMeshRenderingLayer) to be included first.

#define SCREEN_OUTLINE_EXCLUDE_LAYER (1u << 4)

#ifndef SCREEN_OUTLINE_BASE_MASK
    #define SCREEN_OUTLINE_BASE_MASK 0.0
#endif

half ScreenOutlineMask()
{
    return (GetMeshRenderingLayer() & SCREEN_OUTLINE_EXCLUDE_LAYER) != 0u ? 1.0h : (half)SCREEN_OUTLINE_BASE_MASK;
}

#endif
