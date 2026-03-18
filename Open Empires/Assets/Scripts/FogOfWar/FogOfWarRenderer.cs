using UnityEngine;

namespace OpenEmpires
{
    public class FogOfWarRenderer : MonoBehaviour
    {
        private Texture2D fogTexture;
        private Color32[] pixelBuffer;
        private Color32[] blurBuffer;
        private int texWidth;
        private int texHeight;
        private int localPlayerId;
        private const int blurRadius = 3;
        private Material terrainMaterial;
        private Material waterMaterial;

        private static readonly Color32 Unexplored = new Color32(0, 0, 0, 255);
        private static readonly Color32 Explored = new Color32(0, 0, 0, 160);
        private static readonly Color32 Visible = new Color32(0, 0, 0, 0);

        public void Initialize(int mapWidth, int mapHeight, int playerId, float terrainHeightScale = 0f)
        {
            texWidth = mapWidth;
            texHeight = mapHeight;
            localPlayerId = playerId;

            fogTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            fogTexture.wrapMode = TextureWrapMode.Clamp;

            pixelBuffer = new Color32[texWidth * texHeight];
            blurBuffer = new Color32[texWidth * texHeight];

            // Start fully black (unexplored)
            for (int i = 0; i < pixelBuffer.Length; i++)
                pixelBuffer[i] = Unexplored;
            fogTexture.SetPixels32(pixelBuffer);
            fogTexture.Apply();

            // Set fog texture directly on materials (global textures don't bind on WebGL/GLES)
            var groundGO = GameObject.FindWithTag("Ground");
            if (groundGO != null)
            {
                terrainMaterial = groundGO.GetComponent<Renderer>()?.material;
                if (terrainMaterial != null)
                    terrainMaterial.SetTexture("_FogOfWarTex", fogTexture);
            }
            var waterGO = GameObject.Find("Water");
            if (waterGO != null)
            {
                waterMaterial = waterGO.GetComponent<Renderer>()?.material;
                if (waterMaterial != null)
                    waterMaterial.SetTexture("_FogOfWarTex", fogTexture);
            }
            Shader.SetGlobalVector("_FogOfWarParams", new Vector4(mapWidth, mapHeight, 0, 0));
        }

        public void UpdateTexture(FogOfWarData fogData, int playerId)
        {
            // Write hard per-tile values
            for (int z = 0; z < texHeight; z++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    var vis = fogData.GetVisibility(playerId, x, z);
                    pixelBuffer[z * texWidth + x] = vis switch
                    {
                        TileVisibility.Visible => Visible,
                        TileVisibility.Explored => Explored,
                        _ => Unexplored
                    };
                }
            }

            // Separable box blur on alpha channel
            int kernelSize = blurRadius * 2 + 1;

            // Horizontal pass: pixelBuffer -> blurBuffer
            for (int z = 0; z < texHeight; z++)
            {
                int rowOffset = z * texWidth;
                int sum = 0;

                // Seed the window for x=0
                for (int k = -blurRadius; k <= blurRadius; k++)
                {
                    int sx = k < 0 ? 0 : k;
                    sum += pixelBuffer[rowOffset + sx].a;
                }

                blurBuffer[rowOffset] = pixelBuffer[rowOffset];
                blurBuffer[rowOffset].a = (byte)(sum / kernelSize);

                for (int x = 1; x < texWidth; x++)
                {
                    // Remove the column that just left the window, add the column that just entered
                    int removeX = x - blurRadius - 1;
                    int addX = x + blurRadius;
                    if (removeX < 0) removeX = 0;
                    if (addX >= texWidth) addX = texWidth - 1;

                    sum += pixelBuffer[rowOffset + addX].a - pixelBuffer[rowOffset + removeX].a;

                    blurBuffer[rowOffset + x] = pixelBuffer[rowOffset + x];
                    blurBuffer[rowOffset + x].a = (byte)(sum / kernelSize);
                }
            }

            // Vertical pass: blurBuffer -> pixelBuffer
            for (int x = 0; x < texWidth; x++)
            {
                int sum = 0;

                // Seed the window for z=0
                for (int k = -blurRadius; k <= blurRadius; k++)
                {
                    int sz = k < 0 ? 0 : k;
                    sum += blurBuffer[sz * texWidth + x].a;
                }

                pixelBuffer[x] = blurBuffer[x];
                pixelBuffer[x].a = (byte)(sum / kernelSize);

                for (int z = 1; z < texHeight; z++)
                {
                    int removeZ = z - blurRadius - 1;
                    int addZ = z + blurRadius;
                    if (removeZ < 0) removeZ = 0;
                    if (addZ >= texHeight) addZ = texHeight - 1;

                    sum += blurBuffer[addZ * texWidth + x].a - blurBuffer[removeZ * texWidth + x].a;

                    pixelBuffer[z * texWidth + x] = blurBuffer[z * texWidth + x];
                    pixelBuffer[z * texWidth + x].a = (byte)(sum / kernelSize);
                }
            }

            fogTexture.SetPixels32(pixelBuffer);
            fogTexture.Apply();
        }

        private void OnDestroy()
        {
            if (fogTexture != null)
                Destroy(fogTexture);
        }
    }
}
