using UnityEditor;
using UnityEngine;

namespace OpenEmpires
{
    public static class CursorTextureImporter
    {
        [MenuItem("Tools/Fix Cursor Texture Import")]
        public static void FixCursorTexture()
        {
            const string path = "Assets/Resources/ResourceIcons/cursoricon.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Could not find texture importer at: " + path);
                return;
            }

            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log("Cursor texture reimported successfully with Read/Write enabled.");
        }
    }
}
