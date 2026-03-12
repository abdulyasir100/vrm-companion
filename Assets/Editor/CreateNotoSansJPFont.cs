#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

public static class CreateNotoSansJPFont
{
    [MenuItem("Tools/Create Noto Sans JP Font Asset")]
    public static void Create()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP.ttf");
        if (font == null)
        {
            Debug.LogError("Font not found at Assets/Fonts/NotoSansJP.ttf");
            return;
        }

        // Create a dynamic TMP font asset (renders glyphs on demand — supports all CJK)
        var fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset == null)
        {
            Debug.LogError("Failed to create TMP_FontAsset");
            return;
        }

        fontAsset.name = "NotoSansJP SDF";

        AssetDatabase.CreateAsset(fontAsset, "Assets/Fonts/NotoSansJP SDF.asset");
        // Save the atlas texture alongside
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = "NotoSansJP SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "NotoSansJP SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created NotoSansJP SDF font asset at Assets/Fonts/NotoSansJP SDF.asset");
    }
}
#endif