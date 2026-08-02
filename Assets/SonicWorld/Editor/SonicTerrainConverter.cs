#if UNITY_EDITOR
using System;
using System.IO;
using MK.Toon;
using SonicWorld;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonicWorldEditor
{
    public static class SonicTerrainConverter
    {
        public const int ConversionVersion = 3;
        private const int ChunkCells = 64;
        private const int BakeResolution = 2048;
        private const string GeneratedRoot = "Assets/SonicWorld/GeneratedTerrain";
        private const string BakeShaderPath =
            "Assets/SonicWorld/Shaders/SonicTerrainLayerBake.shader";

        [MenuItem("Tools/Sonic World/Convert Selected Terrain To MK Toon Mesh")]
        public static void ConvertSelected()
        {
            Terrain terrain = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Terrain>()
                : null;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog(
                    "Sonic Terrain Converter",
                    "Select a GameObject with a Terrain component.",
                    "OK");
                return;
            }

            Material baseMaterial = LoadTestMaterial();
            if (baseMaterial == null)
            {
                EditorUtility.DisplayDialog(
                    "Sonic Terrain Converter",
                    "Test.mat could not be found.",
                    "OK");
                return;
            }

            Convert(
                terrain,
                baseMaterial,
                $"{GeneratedRoot}/{Sanitize(terrain.name)}",
                true);
        }

        public static GameObject Convert(
            Terrain terrain,
            Material baseMaterial,
            string outputFolder,
            bool disableSource)
        {
            if (terrain == null ||
                terrain.terrainData == null ||
                baseMaterial == null)
            {
                return null;
            }

            EnsureFolder(GeneratedRoot);
            EnsureFolder(outputFolder);

            string generatedName = terrain.name.Replace(" Source", string.Empty) +
                " MK Toon Mesh";
            Transform oldGenerated = terrain.transform.parent != null
                ? terrain.transform.parent.Find(generatedName)
                : null;
            if (oldGenerated != null)
                UnityEngine.Object.DestroyImmediate(oldGenerated.gameObject);

            GameObject root = new GameObject(generatedName);
            SonicGeneratedTerrain marker =
                root.AddComponent<SonicGeneratedTerrain>();
            marker.Configure(ConversionVersion);
            root.transform.SetParent(terrain.transform.parent, false);
            root.transform.SetPositionAndRotation(
                terrain.transform.position,
                terrain.transform.rotation);
            root.transform.localScale = terrain.transform.localScale;

            TerrainData data = terrain.terrainData;
            Texture2D bakedAlbedo = BakeAlbedo(data, outputFolder);
            Material terrainMaterial = CreateTerrainMaterial(
                baseMaterial,
                bakedAlbedo,
                outputFolder);

            int totalCells = data.heightmapResolution - 1;
            int chunkCountX = Mathf.CeilToInt(totalCells / (float)ChunkCells);
            int chunkCountZ = Mathf.CeilToInt(totalCells / (float)ChunkCells);
            for (int z = 0; z < chunkCountZ; z++)
            {
                for (int x = 0; x < chunkCountX; x++)
                {
                    int startX = x * ChunkCells;
                    int startZ = z * ChunkCells;
                    int cellsX = Mathf.Min(ChunkCells, totalCells - startX);
                    int cellsZ = Mathf.Min(ChunkCells, totalCells - startZ);
                    Mesh mesh = BuildChunk(
                        data,
                        startX,
                        startZ,
                        cellsX,
                        cellsZ,
                        totalCells);
                    mesh.name = $"{terrain.name} Chunk {x:00}_{z:00}";
                    string meshPath =
                        $"{outputFolder}/{mesh.name.Replace(' ', '_')}.asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = existing;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }

                    GameObject chunk = new GameObject(mesh.name);
                    chunk.transform.SetParent(root.transform, false);
                    MeshFilter filter = chunk.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = terrainMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    MeshCollider collider = chunk.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    SonicMKToonTarget target =
                        chunk.AddComponent<SonicMKToonTarget>();
                    target.Configure(renderer, null);
                }
            }

            if (disableSource)
            {
                terrain.enabled = false;
                TerrainCollider sourceCollider =
                    terrain.GetComponent<TerrainCollider>();
                if (sourceCollider != null)
                    sourceCollider.enabled = false;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(terrain);
            Selection.activeGameObject = root;
            return root;
        }

        private static Mesh BuildChunk(
            TerrainData data,
            int startX,
            int startZ,
            int cellsX,
            int cellsZ,
            int totalCells)
        {
            int verticesX = cellsX + 1;
            int verticesZ = cellsZ + 1;
            Vector3[] vertices = new Vector3[verticesX * verticesZ];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[cellsX * cellsZ * 6];

            for (int z = 0; z < verticesZ; z++)
            {
                for (int x = 0; x < verticesX; x++)
                {
                    float normalizedX = (startX + x) / (float)totalCells;
                    float normalizedZ = (startZ + z) / (float)totalCells;
                    int index = z * verticesX + x;
                    vertices[index] = new Vector3(
                        normalizedX * data.size.x,
                        data.GetInterpolatedHeight(normalizedX, normalizedZ),
                        normalizedZ * data.size.z);
                    normals[index] =
                        data.GetInterpolatedNormal(normalizedX, normalizedZ);
                    uv[index] = new Vector2(normalizedX, normalizedZ);
                }
            }

            int triangle = 0;
            for (int z = 0; z < cellsZ; z++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    int lowerLeft = z * verticesX + x;
                    int upperLeft = (z + 1) * verticesX + x;
                    triangles[triangle++] = lowerLeft;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = lowerLeft + 1;
                    triangles[triangle++] = lowerLeft + 1;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = upperLeft + 1;
                }
            }

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D BakeAlbedo(
            TerrainData data,
            string outputFolder)
        {
            string pngPath = $"{outputFolder}/Terrain_Albedo.png";
            TerrainLayer[] layers = data.terrainLayers;
            if (layers == null || layers.Length == 0)
                return CreateFallbackAlbedo(pngPath);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(BakeShaderPath);
            if (shader == null)
                shader = Shader.Find("Hidden/SonicWorld/Terrain Layer Bake");
            Material bakeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            RenderTexture target = RenderTexture.GetTemporary(
                BakeResolution,
                BakeResolution,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);

            Texture2D[] alphaMaps = data.alphamapTextures;
            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null || layer.diffuseTexture == null)
                    continue;

                int alphaMapIndex = i / 4;
                if (alphaMapIndex >= alphaMaps.Length)
                    break;
                int channel = i % 4;
                Vector4 channelMask = Vector4.zero;
                channelMask[channel] = 1f;
                Vector2 tileSize = layer.tileSize;
                if (Mathf.Abs(tileSize.x) < 0.0001f)
                    tileSize.x = data.size.x;
                if (Mathf.Abs(tileSize.y) < 0.0001f)
                    tileSize.y = data.size.z;
                bakeMaterial.SetTexture("_LayerTexture", layer.diffuseTexture);
                bakeMaterial.SetTexture("_AlphaMap", alphaMaps[alphaMapIndex]);
                bakeMaterial.SetVector("_AlphaChannel", channelMask);
                bakeMaterial.SetVector(
                    "_LayerST",
                    new Vector4(
                        data.size.x / tileSize.x,
                        data.size.z / tileSize.y,
                        layer.tileOffset.x / tileSize.x,
                        layer.tileOffset.y / tileSize.y));
                Graphics.Blit(Texture2D.whiteTexture, target, bakeMaterial);
            }

            Texture2D baked = new Texture2D(
                BakeResolution,
                BakeResolution,
                TextureFormat.RGBA32,
                true,
                false);
            baked.ReadPixels(
                new Rect(0, 0, BakeResolution, BakeResolution),
                0,
                0);
            baked.Apply(true, false);
            byte[] png = baked.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(baked);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(bakeMaterial);

            File.WriteAllBytes(Path.GetFullPath(pngPath), png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer =
                AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = BakeResolution;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        private static Texture2D CreateFallbackAlbedo(string pngPath)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.32f, 0.38f, 0.42f, 1f);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(pngPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        private static Material CreateTerrainMaterial(
            Material baseMaterial,
            Texture2D albedo,
            string outputFolder)
        {
            string path = $"{outputFolder}/MKToon_Terrain.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(baseMaterial)
                {
                    name = "MK Toon Terrain"
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(baseMaterial);
                material.shader = baseMaterial.shader;
            }

            Properties.albedoMap.SetValue(material, albedo);
            Properties.UpdateSystemProperties(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadTestMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Test.mat");
            return material != null
                ? material
                : AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Material-Test/Test.mat");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static string Sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Replace(' ', '_');
        }
    }
}
#endif
