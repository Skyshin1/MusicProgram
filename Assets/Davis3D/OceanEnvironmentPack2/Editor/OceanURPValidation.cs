// Explicitly requested, read-only project audit. This never opens/saves scenes,
// changes pipeline settings, edits materials, or remaps importers.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Davis3D.OceanEnvironmentPack2.Editor
{
    [InitializeOnLoad]
    public static class OceanURPValidation
    {
        private const string AssetRoot = "Assets/Davis3D/OceanEnvironmentPack2";
        private const string OutputRoot = "Logs/OceanURPMigration";
        private const string RequestPath = OutputRoot + "/validate.request";
        private static readonly Queue<Action> Work = new Queue<Action>();
        private static readonly HashSet<string> CompiledGroups = new HashSet<string>();
        private static readonly Dictionary<string, Shader> AuditedShaders = new Dictionary<string, Shader>();
        private static Report report;
        private static double nextPoll;
        private static string[] sceneStateBefore;
        private static readonly string[] RepresentativePrefabs =
        {
            "Prefabs/SM_Crab_Hepatus.prefab",
            "Prefabs/Corals/SM_Coral_Basic.prefab",
            "Prefabs/Corals/SM_Coral_Acropora.prefab",
            "Prefabs/Other/SM_Kelp_Medium.prefab",
            "Prefabs/Other/SM_SeaWeed.prefab",
            "Prefabs/Rocks/SM_Rock_A.prefab",
            "Prefabs/ShipWreck/SM_Shipwreck_Bottom_A.prefab",
            "Prefabs/ShipWreck/SM_Shipwreck_Mast_Main_A.prefab"
        };

        [Serializable] public class Report
        {
            public string status, startedUtc, finishedUtc, unityVersion, buildTarget, graphicsDevice;
            public string renderPipeline, compileScope;
            public int errors, warnings;
            public List<string> issues = new List<string>();
            public List<MaterialRecord> materials = new List<MaterialRecord>();
            public List<CompileRecord> compilations = new List<CompileRecord>();
            public List<ShaderRecord> shaders = new List<ShaderRecord>();
            public List<RendererRecord> rendererSlots = new List<RendererRecord>();
            public List<RemapRecord> modelRemaps = new List<RemapRecord>();
            public List<PrefabPreviewRecord> prefabPreviews = new List<PrefabPreviewRecord>();
            public List<BackendCompileRecord> androidCompilations = new List<BackendCompileRecord>();
            public string[] scenesBefore, scenesAfter;
        }

        [Serializable] public class MaterialRecord
        {
            public string path, guid, shader, shaderPath, renderPipelineTag, previewPath, previewNote;
            public bool supported, builtinSkybox;
            public int passCount, renderQueue;
            public string[] keywords;
            public List<TextureRecord> textures = new List<TextureRecord>();
        }

        [Serializable] public class TextureRecord
        {
            public string property, path, guid, savedGuid;
            public bool shaderUsesProperty, assigned, missingSavedReference;
        }

        [Serializable] public class CompileRecord
        {
            public string material, shader, passName, mode, exception;
            public int pass;
            public bool compiled;
            public string[] requestedKeywords;
        }

        [Serializable] public class BackendCompileRecord
        {
            public string shader, shaderPath, passName, platform, target, mode, stage, exception;
            public int subshader, pass, bytecodeLength;
            public bool success, hasBytecode;
            public string[] requestedKeywords;
            public List<MessageRecord> messages = new List<MessageRecord>();
        }

        [Serializable] public class ShaderRecord
        {
            public string path, name;
            public bool supported, hasError;
            public List<MessageRecord> messages = new List<MessageRecord>();
        }

        [Serializable] public class MessageRecord
        {
            public string severity, message, details, file, platform;
            public int line;
        }

        [Serializable] public class RendererRecord
        {
            public string asset, kind, renderer, rendererType;
            public int slot;
            public string material, materialGuid, shader;
            public bool missing, wrongPipeline;
        }

        [Serializable] public class RemapRecord
        {
            public string model, sourceName, sourceType, target, targetGuid;
            public bool missing;
        }

        [Serializable] public class PrefabPreviewRecord
        {
            public string asset, previewPath, note;
            public int meshRenderers, submeshDraws;
            public bool success;
            public List<string> meshes = new List<string>();
            public List<string> materials = new List<string>();
        }

        static OceanURPValidation()
        {
            // Merely installing/reloading this script never starts validation.
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Ocean Environment Pack 2/Validate URP Materials %#F8")]
        public static void RequestValidation()
        {
            if (report != null)
            {
                Debug.Log("Ocean URP validation is already running.");
                return;
            }
            Directory.CreateDirectory(OutputRoot);
            File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Ocean URP validation requested; it will run when the Editor is idle.");
        }

        private static bool EditorIsBusy()
        {
            return EditorApplication.isCompiling || EditorApplication.isUpdating ||
                   EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void Tick()
        {
            if (EditorIsBusy()) return;
            if (report == null)
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 1.0;
                if (!File.Exists(RequestPath)) return;
                try { Begin(); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    if (report != null) { Issue(true, "Start failed: " + ex); Finish("failed"); }
                }
                return;
            }
            try
            {
                if (Work.Count != 0) Work.Dequeue()();
                else if (!ShaderUtil.anythingCompiling) Finish("completed");
            }
            catch (Exception ex)
            {
                Issue(true, "Audit operation failed: " + ex);
                WriteReport();
            }
        }

        private static void Begin()
        {
            Directory.CreateDirectory(OutputRoot + "/Previews");
            Work.Clear();
            CompiledGroups.Clear();
            AuditedShaders.Clear();
            sceneStateBefore = SceneState();
            report = new Report
            {
                status = "running", startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName,
                renderPipeline = PipelinePath(),
                compileScope = "All passes for each distinct shader and real material keyword set; plain and requested INSTANCING_ON + STEREO_INSTANCING_ON. CompilePass uses the current Editor graphics backend. Ported shader passes are additionally compiled to Vulkan and GLES3x bytecode for BuildTarget.Android via ShaderData.Pass.CompileVariant in plain, stereo-instancing, stereo-multiview, and mesh-instancing-plus-stereo-instancing modes, without changing the active target. INSTANCING_ON is independent mesh instancing, not required for ordinary XR. Default Android platform defines are supplied by Unity. On these backends Vertex compilation bundles all stages. Requested stereo keyword compilation is not proof of XR runtime rendering. This is not a complete Android build, exhaustive multi_compile matrix, or headset test.",
                scenesBefore = sceneStateBefore
            };
            if (GraphicsSettings.currentRenderPipeline == null)
                Issue(true, "No active Scriptable Render Pipeline asset; previews cannot establish URP compatibility.");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { AssetRoot }).OrderBy(x => x))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Work.Enqueue(() => AuditMaterial(path));
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p =>
                p.StartsWith(AssetRoot + "/", StringComparison.Ordinal) &&
                (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))).OrderBy(p => p))
            {
                string captured = path;
                Work.Enqueue(() => AuditRendererReferences(captured));
            }
            foreach (string relativePath in RepresentativePrefabs)
            {
                string captured = AssetRoot + "/" + relativePath;
                Work.Enqueue(() => RenderPrefabPreview(captured));
            }
            // Compilation is synchronous per pass, but only one material is audited
            // per editor update; no long polling loops or scene UI blocking dialogs.
            WriteReport();
            Debug.Log("Ocean URP validation started. Reports: " + Path.GetFullPath(OutputRoot));
        }

        private static void AuditMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { Issue(true, "Material failed to load: " + path); return; }
            Shader shader = material.shader;
            var row = new MaterialRecord
            {
                path = path, guid = AssetDatabase.AssetPathToGUID(path),
                shader = shader != null ? shader.name : "<missing>",
                shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : "",
                renderPipelineTag = material.GetTag("RenderPipeline", false, ""),
                builtinSkybox = IsBuiltinSkybox(shader),
                supported = shader != null && shader.isSupported,
                passCount = material.passCount, renderQueue = material.renderQueue,
                keywords = material.shaderKeywords.OrderBy(k => k).ToArray()
            };
            report.materials.Add(row);
            if (shader == null) { Issue(true, "Missing shader: " + path); return; }
            AuditedShaders[shader.name] = shader;
            if (!row.builtinSkybox && row.renderPipelineTag != "UniversalPipeline")
                Issue(true, "Material is not URP tagged: " + path + " -> " + shader.name);
            if (!row.supported) Issue(true, "Unsupported shader: " + path + " -> " + shader.name);
            if (row.passCount == 0) Issue(true, "Shader has no active passes: " + path);
            AuditTextures(material, row);
            CompileMaterial(material, false);
            CompileMaterial(material, true);
            try { RenderPreview(material, row); }
            catch (Exception ex)
            {
                row.previewNote = "Preview failed: " + ex.Message;
                Issue(false, path + ": " + row.previewNote);
            }
            WriteReport();
        }

        private static void AuditTextures(Material material, MaterialRecord row)
        {
            var used = new HashSet<string>();
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
                if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                    used.Add(shader.GetPropertyName(i));
            var saved = new Dictionary<string, string>();
            // Read serialized GUIDs as well: unresolved references can return null
            // through GetTexture and otherwise look like an intentional empty slot.
            string yaml = File.ReadAllText(row.path);
            foreach (Match match in Regex.Matches(yaml,
                @"(?m)^\s*-\s+(?<name>[^:\r\n]+):\s*\r?\n\s+m_Texture:\s*\{(?<value>[^}]+)\}"))
            {
                Match guid = Regex.Match(match.Groups["value"].Value, @"guid:\s*([0-9a-fA-F]{32})");
                if (guid.Success) saved[match.Groups["name"].Value.Trim()] = guid.Groups[1].Value;
            }
            foreach (string property in used.Union(saved.Keys).OrderBy(p => p))
            {
                bool isUsed = used.Contains(property);
                Texture texture = isUsed ? material.GetTexture(property) : null;
                string texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : "";
                saved.TryGetValue(property, out string savedGuid);
                string savedPath = string.IsNullOrEmpty(savedGuid) ? "" : AssetDatabase.GUIDToAssetPath(savedGuid);
                bool missing = !string.IsNullOrEmpty(savedGuid) &&
                    (string.IsNullOrEmpty(savedPath) || AssetDatabase.LoadAssetAtPath<Texture>(savedPath) == null);
                row.textures.Add(new TextureRecord
                {
                    property = property, shaderUsesProperty = isUsed, assigned = texture != null,
                    path = texturePath, guid = AssetDatabase.AssetPathToGUID(texturePath),
                    savedGuid = savedGuid ?? "", missingSavedReference = missing
                });
                if (missing) Issue(isUsed, (isUsed ? "Used" : "Unused saved") +
                    " texture reference is missing: " + row.path + " / " + property + " / " + savedGuid);
            }
        }

        private static void CompileMaterial(Material source, bool stereo)
        {
            var keywords = source.shaderKeywords.Where(k =>
                k != "INSTANCING_ON" && k != "STEREO_INSTANCING_ON" && k != "STEREO_MULTIVIEW_ON").ToList();
            if (stereo) { keywords.Add("INSTANCING_ON"); keywords.Add("STEREO_INSTANCING_ON"); }
            string[] requested = keywords.Distinct().OrderBy(k => k).ToArray();
            string key = source.shader.name + "|" + string.Join(";", requested);
            if (!CompiledGroups.Add(key)) return;
            QueueAndroidCompilations(source.shader, requested,
                stereo ? "mesh-instancing-plus-stereo-instancing" : "plain");
            if (!stereo)
            {
                // URP XRPass selects one stereo mode independently of material
                // GPU instancing. Test the ordinary SRP-batched XR paths too.
                QueueAndroidCompilations(source.shader, requested.Concat(new[] { "STEREO_INSTANCING_ON" }).ToArray(),
                    "stereo-instancing");
                QueueAndroidCompilations(source.shader, requested.Concat(new[] { "STEREO_MULTIVIEW_ON" }).ToArray(),
                    "stereo-multiview");
            }
            Material clone = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                clone.shaderKeywords = requested;
                clone.enableInstancing = stereo;
                for (int pass = 0; pass < clone.passCount; pass++)
                {
                    var row = new CompileRecord
                    {
                        material = AssetDatabase.GetAssetPath(source), shader = source.shader.name,
                        pass = pass, passName = clone.GetPassName(pass),
                        mode = stereo ? "requested-single-pass-instanced" : "plain",
                        requestedKeywords = requested
                    };
                    report.compilations.Add(row);
                    try
                    {
                        ShaderUtil.CompilePass(clone, pass, true);
                        row.compiled = ShaderUtil.IsPassCompiled(clone, pass);
                        if (!row.compiled) Issue(true, "Pass did not compile: " + row.shader + " / " + row.passName + " / " + row.mode);
                    }
                    catch (Exception ex)
                    {
                        row.exception = ex.ToString();
                        Issue(true, "CompilePass failed: " + row.shader + " / " + row.passName + " / " + row.mode + ": " + ex.Message);
                    }
                }
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static void QueueAndroidCompilations(Shader shader, string[] requested, string mode)
        {
            // Limit the extra cross-compilation to the shaders ported by this
            // migration, not Unity's built-in skybox or unrelated package shaders.
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!shaderPath.StartsWith(AssetRoot + "/Shaders/", StringComparison.Ordinal)) return;
            ShaderData data = ShaderUtil.GetShaderData(shader);
            for (int subshader = 0; subshader < data.SubshaderCount; subshader++)
            {
                ShaderData.Subshader sub = data.GetSubshader(subshader);
                if (sub == null) continue;
                for (int pass = 0; pass < sub.PassCount; pass++)
                {
                    int capturedSubshader = subshader;
                    int capturedPass = pass;
                    foreach (ShaderCompilerPlatform platform in new[] { ShaderCompilerPlatform.Vulkan, ShaderCompilerPlatform.GLES3x })
                    {
                        ShaderCompilerPlatform capturedPlatform = platform;
                        string[] capturedKeywords = requested.ToArray();
                        Work.Enqueue(() => CompileAndroidVariant(shader, capturedSubshader, capturedPass,
                            capturedPlatform, capturedKeywords, mode));
                    }
                }
            }
        }

        private static void CompileAndroidVariant(Shader shader, int subshader, int pass,
            ShaderCompilerPlatform platform, string[] keywords, string mode)
        {
            var row = new BackendCompileRecord
            {
                shader = shader.name, shaderPath = AssetDatabase.GetAssetPath(shader),
                subshader = subshader, pass = pass, platform = platform.ToString(),
                target = BuildTarget.Android.ToString(), mode = mode,
                stage = "Vertex (combined backend program includes all stages)",
                requestedKeywords = keywords
            };
            report.androidCompilations.Add(row);
            try
            {
                ShaderData.Pass shaderPass = ShaderUtil.GetShaderData(shader).GetSubshader(subshader).GetPass(pass);
                row.passName = shaderPass.Name;
                ShaderData.VariantCompileInfo result = shaderPass.CompileVariant(
                    ShaderType.Vertex, keywords, platform, BuildTarget.Android);
                row.success = result.Success;
                row.bytecodeLength = result.ShaderData != null ? result.ShaderData.Length : 0;
                row.hasBytecode = row.bytecodeLength > 0;
                foreach (ShaderMessage message in result.Messages ?? Array.Empty<ShaderMessage>())
                {
                    row.messages.Add(new MessageRecord
                    {
                        severity = message.severity.ToString(), message = message.message,
                        details = message.messageDetails, file = message.file,
                        platform = message.platform.ToString(), line = message.line
                    });
                    if (message.severity == ShaderCompilerMessageSeverity.Error)
                        Issue(true, "Android " + platform + " / " + shader.name + " / " + row.passName +
                            " / " + mode + ": " + message.message);
                }
                if (!row.success)
                    Issue(true, "Android backend compilation failed: " + shader.name + " / " + row.passName + " / " + platform + " / " + mode);
                else if (!row.hasBytecode)
                    Issue(false, "Android backend returned success but empty bytecode (not a verified compiled pass): " +
                        shader.name + " / " + row.passName + " / " + platform + " / " + mode);
            }
            catch (Exception ex)
            {
                row.exception = ex.ToString();
                Issue(false, "Android backend API could not verify this pass: " + shader.name + " / " +
                    row.passName + " / " + platform + " / " + mode + ": " + ex.Message);
            }
            if (report.androidCompilations.Count % 8 == 0) WriteReport();
        }

        private static void RenderPreview(Material source, MaterialRecord row)
        {
            var preview = new PreviewRenderUtility();
            Material clone = null;
            Texture2D image = null;
            try
            {
                clone = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                if (sphere == null) throw new InvalidOperationException("Built-in sphere mesh is unavailable.");
                preview.camera.transform.position = new Vector3(0, 0, -3.2f);
                preview.camera.transform.LookAt(Vector3.zero);
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 1000f;
                preview.camera.fieldOfView = 30f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.055f, 0.085f, 0.12f, 1);
                preview.ambientColor = new Color(0.35f, 0.38f, 0.42f, 1);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = 0.65f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 177);
                preview.BeginStaticPreview(new Rect(0, 0, 256, 256));
                preview.DrawMesh(sphere, Matrix4x4.Rotate(Quaternion.Euler(0, 20, 0)), clone, 0);
                preview.Render(true);
                image = preview.EndStaticPreview();
                if (image == null) throw new InvalidOperationException("EndStaticPreview returned no image.");
                row.previewPath = OutputRoot + "/Previews/" + Path.GetFileNameWithoutExtension(row.path) + "_" + row.guid.Substring(0, 8) + ".png";
                File.WriteAllBytes(row.previewPath, image.EncodeToPNG());
                row.previewNote = "URP-enabled isolated sphere swatch; original material values. Screen-depth/color effects, near fades, large-world animation, and skyboxes require an authored scene and may be blank here. No headset/XR claim.";
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview.Cleanup();
                if (clone != null) Object.DestroyImmediate(clone);
            }
        }

        private static void RenderPrefabPreview(string path)
        {
            var row = new PrefabPreviewRecord { asset = path };
            report.prefabPreviews.Add(row);
            PreviewRenderUtility preview = null;
            Texture2D image = null;
            var clones = new Dictionary<Material, Material>();
            try
            {
                // Read the persistent prefab only. Do not instantiate it: that
                // would allow ExecuteAlways/OnEnable behaviours to affect scenes.
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) throw new InvalidOperationException("Prefab could not be loaded.");
                var lowerLod = new HashSet<Renderer>();
                var firstLod = new HashSet<Renderer>();
                foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    LOD[] levels = group.GetLODs();
                    for (int i = 0; i < levels.Length; i++)
                        foreach (Renderer renderer in levels[i].renderers)
                            if (renderer != null) (i == 0 ? firstLod : lowerLod).Add(renderer);
                }
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                    r.enabled && IsActiveInPrefab(r.transform, root.transform) &&
                    (!lowerLod.Contains(r) || firstLod.Contains(r)) &&
                    r.GetComponent<MeshFilter>() != null &&
                    r.GetComponent<MeshFilter>().sharedMesh != null).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No enabled LOD0 MeshRenderer meshes.");

                Bounds bounds = new Bounds();
                bool first = true;
                Matrix4x4 prefabSpace = root.transform.worldToLocalMatrix;
                foreach (MeshRenderer renderer in renderers)
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Matrix4x4 transform = prefabSpace * renderer.localToWorldMatrix;
                    Bounds transformed = TransformBounds(mesh.bounds, transform);
                    if (first) { bounds = transformed; first = false; }
                    else bounds.Encapsulate(transformed);
                }
                float scale = 2f / Mathf.Max(0.00001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                Matrix4x4 normalize = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-bounds.center);
                preview = new PreviewRenderUtility();
                preview.camera.transform.position = new Vector3(4, 3, -6).normalized * 10f;
                preview.camera.transform.LookAt(Vector3.zero);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = Mathf.Max(0.5f, bounds.extents.magnitude * scale * 1.15f);
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 100f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.055f, 0.085f, 0.12f, 1);
                preview.ambientColor = new Color(0.35f, 0.38f, 0.42f, 1);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = 0.65f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 177);
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                foreach (MeshRenderer renderer in renderers)
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (mesh.subMeshCount == 0) continue;
                    Material[] materials = renderer.sharedMaterials;
                    Matrix4x4 matrix = normalize * prefabSpace * renderer.localToWorldMatrix;
                    row.meshRenderers++;
                    row.meshes.Add(AssetDatabase.GetAssetPath(mesh) + " :: " + mesh.name);
                    // MeshRenderer maps one material per submesh; additional
                    // material slots intentionally draw the final submesh again.
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        Material material = materials[slot];
                        if (material == null) continue;
                        if (!clones.TryGetValue(material, out Material clone))
                        {
                            clone = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
                            clones.Add(material, clone);
                        }
                        preview.DrawMesh(mesh, matrix, clone, Mathf.Min(slot, mesh.subMeshCount - 1));
                        row.submeshDraws++;
                        row.materials.Add(AssetDatabase.GetAssetPath(material));
                    }
                    if (materials.Length < mesh.subMeshCount)
                        Issue(false, "Prefab preview has unassigned submeshes: " + path + " / " + renderer.name);
                }
                preview.Render(true);
                image = preview.EndStaticPreview();
                if (image == null) throw new InvalidOperationException("EndStaticPreview returned no image.");
                row.previewPath = OutputRoot + "/Previews/Prefab_" + Path.GetFileNameWithoutExtension(path) + ".png";
                File.WriteAllBytes(row.previewPath, image.EncodeToPNG());
                row.success = true;
                row.note = "Actual prefab LOD0 mesh/submesh geometry and original materials, normalized by transformed mesh bounds. No prefab or scripts instantiated. Isolated editor URP lighting; not a scene parity or headset screenshot.";
            }
            catch (Exception ex)
            {
                row.note = "Prefab preview failed: " + ex;
                Issue(false, path + ": " + row.note);
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                if (preview != null) preview.Cleanup();
                foreach (Material clone in clones.Values) Object.DestroyImmediate(clone);
            }
            WriteReport();
        }

        private static bool IsActiveInPrefab(Transform transform, Transform root)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) break;
            }
            return true;
        }

        private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(source.center);
            Vector3 x = matrix.MultiplyVector(new Vector3(source.extents.x, 0, 0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0, source.extents.y, 0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, source.extents.z));
            Vector3 extents = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(center, extents * 2f);
        }

        private static void AuditRendererReferences(string path)
        {
            bool isModel = path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { Issue(true, "Prefab/model did not load: " + path); return; }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    Shader shader = material != null ? material.shader : null;
                    bool wrongPipeline = material != null && !IsBuiltinSkybox(shader) &&
                        material.GetTag("RenderPipeline", false, "") != "UniversalPipeline";
                    var row = new RendererRecord
                    {
                        asset = path, kind = isModel ? "FBX" : "Prefab",
                        renderer = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform),
                        rendererType = renderer.GetType().Name, slot = i,
                        material = material != null ? AssetDatabase.GetAssetPath(material) : "",
                        materialGuid = material != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)) : "",
                        shader = shader != null ? shader.name : "<missing>",
                        missing = material == null || shader == null, wrongPipeline = wrongPipeline
                    };
                    report.rendererSlots.Add(row);
                    if (row.missing) Issue(true, "Missing renderer material/shader: " + path + " / " + row.renderer + " / slot " + i);
                    if (wrongPipeline) Issue(true, "Non-URP renderer material: " + path + " / " + row.renderer + " / " + row.shader);
                }
            }
            if (!isModel) return;
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null) { Issue(true, "FBX has no importer: " + path); return; }
            foreach (var entry in importer.GetExternalObjectMap())
            {
                if (entry.Key.type != typeof(Material)) continue;
                string targetPath = entry.Value != null ? AssetDatabase.GetAssetPath(entry.Value) : "";
                var row = new RemapRecord
                {
                    model = path, sourceName = entry.Key.name, sourceType = entry.Key.type.FullName,
                    target = targetPath, targetGuid = AssetDatabase.AssetPathToGUID(targetPath),
                    missing = !(entry.Value is Material)
                };
                report.modelRemaps.Add(row);
                if (row.missing) Issue(true, "Missing FBX material remap: " + path + " / " + row.sourceName);
            }
        }

        private static bool IsBuiltinSkybox(Shader shader)
        {
            return shader != null && shader.name.StartsWith("Skybox/", StringComparison.Ordinal) &&
                   !AssetDatabase.GetAssetPath(shader).EndsWith(".shader", StringComparison.OrdinalIgnoreCase);
        }

        private static string PipelinePath()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null ? AssetDatabase.GetAssetPath(pipeline) + " [" + pipeline.GetType().FullName + "]" : "<none>";
        }

        private static string[] SceneState()
        {
            var result = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                result.Add(scene.handle + "|" + scene.path + "|dirty=" + scene.isDirty + "|roots=" + scene.rootCount);
            }
            return result.ToArray();
        }

        private static void Issue(bool error, string message)
        {
            if (error) report.errors++; else report.warnings++;
            report.issues.Add((error ? "ERROR: " : "WARNING: ") + message);
        }

        private static void Finish(string status)
        {
            foreach (var item in AuditedShaders.OrderBy(p => p.Key))
            {
                Shader shader = item.Value;
                var row = new ShaderRecord
                {
                    name = shader.name, path = AssetDatabase.GetAssetPath(shader),
                    supported = shader.isSupported, hasError = ShaderUtil.ShaderHasError(shader)
                };
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    row.messages.Add(new MessageRecord
                    {
                        severity = message.severity.ToString(), message = message.message,
                        details = message.messageDetails, file = message.file,
                        platform = message.platform.ToString(), line = message.line
                    });
                    Issue(message.severity.ToString() == "Error",
                        shader.name + ": " + message.severity + " " + message.message + " (" + message.file + ":" + message.line + ")");
                }
                if (row.hasError && row.messages.Count == 0) Issue(true, "ShaderHasError with no messages: " + shader.name);
                report.shaders.Add(row);
            }
            report.scenesAfter = SceneState();
            if (!sceneStateBefore.SequenceEqual(report.scenesAfter))
                Issue(false, "Loaded scene state changed during the audit interval (possibly an external/user edit); the validator never opens, saves, or edits user scenes.");
            if (report.renderPipeline != PipelinePath())
                Issue(false, "Active pipeline changed externally during the audit interval.");
            report.status = status;
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            WriteReport();
            // The explicit marker is consumed only after a terminal report is saved.
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            Debug.Log("Ocean URP validation " + status + ": " + report.materials.Count + " materials, " +
                report.compilations.Count + " pass checks, " + report.errors + " errors, " + report.warnings +
                " warnings. " + Path.GetFullPath(OutputRoot + "/UnityValidation.txt"));
            report = null;
            Work.Clear();
        }

        private static void WriteReport()
        {
            Directory.CreateDirectory(OutputRoot);
            File.WriteAllText(OutputRoot + "/UnityValidation.json", JsonUtility.ToJson(report, true), Encoding.UTF8);
            var text = new StringBuilder();
            text.AppendLine("Ocean Environment Pack 2 - Unity URP Validation");
            text.AppendLine("Status: " + report.status + " | Unity: " + report.unityVersion);
            text.AppendLine("Started: " + report.startedUtc + " | Finished: " + report.finishedUtc);
            text.AppendLine("Target: " + report.buildTarget + " | Graphics: " + report.graphicsDevice);
            text.AppendLine("Pipeline: " + report.renderPipeline);
            text.AppendLine("Scope: " + report.compileScope);
            text.AppendLine("Materials: " + report.materials.Count + " | Compiled passes: " + report.compilations.Count +
                " | Renderer slots: " + report.rendererSlots.Count + " | FBX remaps: " + report.modelRemaps.Count +
                " | Android backend variants: " + report.androidCompilations.Count);
            text.AppendLine("Errors: " + report.errors + " | Warnings: " + report.warnings);
            foreach (string issue in report.issues) text.AppendLine(issue);
            foreach (MaterialRecord material in report.materials)
            {
                text.AppendLine("\nMATERIAL " + material.path + " -> " + material.shader +
                    " | supported=" + material.supported + " | passes=" + material.passCount);
                text.AppendLine("  Keywords: " + string.Join(", ", material.keywords));
                foreach (TextureRecord texture in material.textures)
                    text.AppendLine("  Texture " + texture.property + ": " + texture.path + " | used=" +
                        texture.shaderUsesProperty + " | savedGuid=" + texture.savedGuid + " | missing=" + texture.missingSavedReference);
                text.AppendLine("  Preview: " + material.previewPath + " | " + material.previewNote);
            }
            foreach (CompileRecord pass in report.compilations)
                text.AppendLine("PASS " + pass.shader + " / " + pass.passName + " / " + pass.mode +
                    " | compiled=" + pass.compiled + " | " + pass.exception);
            foreach (PrefabPreviewRecord prefab in report.prefabPreviews)
                text.AppendLine("PREFAB PREVIEW " + prefab.asset + " | success=" + prefab.success +
                    " | renderers=" + prefab.meshRenderers + " | submesh draws=" + prefab.submeshDraws +
                    " | " + prefab.previewPath + " | " + prefab.note);
            foreach (BackendCompileRecord backend in report.androidCompilations)
            {
                text.AppendLine("ANDROID " + backend.platform + " / " + backend.shader + " / " + backend.passName +
                    " / " + backend.mode + " | success=" + backend.success + " | bytes=" + backend.bytecodeLength + " | " + backend.exception);
                foreach (MessageRecord message in backend.messages)
                    text.AppendLine("  " + message.severity + ": " + message.message + " (" + message.file + ":" + message.line + ")");
            }
            File.WriteAllText(OutputRoot + "/UnityValidation.txt", text.ToString(), Encoding.UTF8);
        }
    }
}
#endif
