#if UNITY_EDITOR
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class CherryBombFontRegenerator
{
    private const string SourceFontPath = "Assets/_Project/other/CherryBombOne-Regular.ttf";
    private const string FontAssetPath = "Assets/_Project/Fonts/CherryBombOne-Regular SDF.asset";
    private const int AtlasSize = 1024;
    private const int SamplingPointSize = 66;
    private const int AtlasPadding = 5;

    private const string PolishLetters = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";

    [MenuItem("FAST VR/Regenerate Cherry Bomb Font (Polish)")]
    public static void RegenerateFromMenu()
    {
        Regenerate();
    }

    public static void RegenerateBatch()
    {
        if (!Regenerate())
            EditorApplication.Exit(1);
        else
            EditorApplication.Exit(0);
    }

    public static bool Regenerate()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"Cherry Bomb regenerator: source font not found at {SourceFontPath}");
            return false;
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError($"Cherry Bomb regenerator: font asset not found at {FontAssetPath}");
            return false;
        }

        string sourceGuid = AssetDatabase.AssetPathToGUID(SourceFontPath);
        string characters = BuildRequiredCharacterSet();

        Undo.RecordObject(fontAsset, "Regenerate Cherry Bomb Font (Polish)");
        if (fontAsset.atlasTexture != null)
            Undo.RecordObject(fontAsset.atlasTexture, "Regenerate Cherry Bomb Font (Polish)");
        if (fontAsset.material != null)
            Undo.RecordObject(fontAsset.material, "Regenerate Cherry Bomb Font (Polish)");

        ApplyFontAssetSettings(fontAsset, sourceGuid);
        EnsureAtlasSize(fontAsset, AtlasSize, AtlasSize);

        fontAsset.ClearFontAssetData(false);
        ApplyFontAssetSettings(fontAsset, sourceGuid);
        EnsureAtlasSize(fontAsset, AtlasSize, AtlasSize);

        if (!fontAsset.TryAddCharacters(characters, out string missing, includeFontFeatures: true)
            || !string.IsNullOrEmpty(missing))
        {
            Debug.LogError($"Cherry Bomb regenerator failed. Missing characters: {missing}", fontAsset);
            return false;
        }

        UpdateCreationSettings(fontAsset, sourceGuid, characters);
        MarkDirtyAndSave(fontAsset);

        Debug.Log(
            $"Cherry Bomb font rebuilt on a single {AtlasSize}px atlas ({fontAsset.characterTable.Count} characters).",
            fontAsset);
        return true;
    }

    private static void ApplyFontAssetSettings(TMP_FontAsset fontAsset, string sourceGuid)
    {
        var serializedFont = new SerializedObject(fontAsset);
        serializedFont.FindProperty("m_SourceFontFileGUID").stringValue = sourceGuid;
        serializedFont.FindProperty("m_AtlasPopulationMode").enumValueIndex = (int)AtlasPopulationMode.Dynamic;
        serializedFont.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = false;
        serializedFont.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureAtlasSize(TMP_FontAsset fontAsset, int width, int height)
    {
        var serializedFont = new SerializedObject(fontAsset);
        serializedFont.FindProperty("m_AtlasWidth").intValue = width;
        serializedFont.FindProperty("m_AtlasHeight").intValue = height;
        serializedFont.ApplyModifiedPropertiesWithoutUndo();

        UpdateFontMaterial(fontAsset, width, height);
    }

    private static void UpdateFontMaterial(TMP_FontAsset fontAsset, int width, int height)
    {
        Material material = fontAsset.material;
        if (material == null)
            return;

        material.SetFloat(ShaderUtilities.ID_TextureWidth, width);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, height);

        if (material.HasProperty(ShaderUtilities.ID_GradientScale))
            material.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1f);
    }

    private static void UpdateCreationSettings(TMP_FontAsset fontAsset, string sourceGuid, string characters)
    {
        FontAssetCreationSettings settings = fontAsset.creationSettings;
        settings.sourceFontFileGUID = sourceGuid;
        settings.pointSize = SamplingPointSize;
        settings.padding = AtlasPadding;
        settings.atlasWidth = AtlasSize;
        settings.atlasHeight = AtlasSize;
        settings.characterSetSelectionMode = 7;
        settings.characterSequence = characters;
        fontAsset.creationSettings = settings;
    }

    private static void MarkDirtyAndSave(TMP_FontAsset fontAsset)
    {
        EditorUtility.SetDirty(fontAsset);

        if (fontAsset.atlasTexture != null)
            EditorUtility.SetDirty(fontAsset.atlasTexture);

        if (fontAsset.material != null)
            EditorUtility.SetDirty(fontAsset.material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string BuildRequiredCharacterSet()
    {
        var builder = new StringBuilder(256);

        for (int i = 32; i <= 126; i++)
            builder.Append((char)i);

        builder.Append(PolishLetters);
        builder.Append('\u00A0');
        builder.Append('\u2026');
        builder.Append("Ładowanie gry…Wybierz gręCzekaj na polecenia diagnosty");
        builder.Append("NEXTSampleSceneForestWalkMalowanieWyjdź");
        builder.Append("Spacer w lesieZacznij badanieWitamy");

        return new string(builder.ToString().Distinct().ToArray());
    }
}
#endif
