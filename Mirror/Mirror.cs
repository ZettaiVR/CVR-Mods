using MelonLoader;
using System;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.XR;
using ABI_RC.Core.Savior;
using System.Linq;

[assembly: MelonInfo(typeof(Zettai.Mirror), "Mirror", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    public class Mirror : MelonMod
    {
        private static readonly MelonPreferences_Category category = MelonPreferences.CreateCategory("Zettai");
        private static readonly MelonPreferences_Entry<bool> MirrorPref = category.CreateEntry("Mirror", true, "Mirror");
        private static readonly MelonPreferences_Entry<bool> MirrorVram = category.CreateEntry("MirrorVram", true, "Mirror VRAM saver");
        private static readonly MelonPreferences_Entry<Occlusion> MirrorOcclusion = category.CreateEntry("MirrorOcclusion", Occlusion.Default, "Mirror occlusion culling");
        private static readonly MelonPreferences_Entry<Optimization> MirrorOptimization = category.CreateEntry("MirrorOptimization", Optimization.Frustum, "Mirror Optimization type");
        private static readonly MelonPreferences_Entry<MirrorReflection.CullDistance> MirrorCullDistanceType = category.CreateEntry("MirrorCullDistanceType", MirrorReflection.CullDistance.Off, "Mirror far cull distance type");
        private static readonly MelonPreferences_Entry<int> MirrorCullDistanceValue = category.CreateEntry("MirrorCullDistanceValue", 1000, "Mirror far cull distance [10 - 10000 m]");
        private static readonly MelonPreferences_Entry<bool> MirrorResOverride = category.CreateEntry("MirrorResOverride", true, "Mirror resolution unlimiter");
        private static readonly MelonPreferences_Entry<int> MirrorResPercent = category.CreateEntry("MirrorResPercent", 100, "Mirror resolution [1-150%]");
        private static readonly Dictionary<CVRMirror, MirrorReflection> mirrors = new Dictionary<CVRMirror, MirrorReflection>();
        
        public enum Occlusion 
        {
            Default,
            ForcedOn,
            ForcedOnNotTransparent,
            ForcedOff
        }
        public enum Optimization
        { 
            None,
            Frustum,
            Depth,
            Both,
        }
        public override void OnInitializeMelon()
        {
            MirrorCullDistanceValue.OnEntryValueChanged.Subscribe(MirrorCullDistanceValue_OnValueChanged);
            if (MirrorOptimization.Value == Optimization.None)
                MirrorOptimization.Value = Optimization.Frustum;
        }
        private static void MirrorCullDistanceValue_OnValueChanged(int oldValue, int newValue)
        {
            if (oldValue == newValue || (newValue >= 10 && newValue <= 10000))
                return;

            if (newValue == 0)
            {
                MelonLogger.Msg($"Resetting mirror far cull distance value to {MirrorCullDistanceValue.DefaultValue}.");
                MirrorCullDistanceValue.ResetToDefault();
                return;
            }
            MelonLogger.Msg($"Clamping mirror far cull distance value to 10 - 10000. Original: {newValue}");
            MirrorCullDistanceValue.Value = Mathf.Clamp(newValue, 10, 10000);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MetaPort.Instance.settings.settingIntChanged.RemoveListener(MirrorPatch.Settings);
            MetaPort.Instance.settings.settingIntChanged.AddListener(MirrorPatch.Settings);
            ClearMirrorCache();
        }
        private static void ClearMirrorCache() 
        {
            var _mirrors = mirrors.Keys.ToArray();
            foreach (var m in _mirrors) 
            {
                if (!m) 
                {
                    mirrors.Remove(m);
                }
            }
        }
        [HarmonyPatch(typeof(CVRMirror), nameof(CVRMirror.OnWillRenderObject))]
        class MirrorPatch
        {
            public static bool Prefix(CVRMirror __instance)
            {
                if (!MirrorPref.Value)
                {
                    return true;
                }
                var cvrMirror = __instance;
                if (!mirrors.TryGetValue(cvrMirror, out var mirror)) 
                {
                    mirrors[cvrMirror] = mirror = new MirrorReflection(cvrMirror.gameObject, cvrMirror.m_ReflectLayers, cvrMirror.m_DisablePixelLights);
                    mirror.Start();
                }
                cvrMirror.CleanupMirrorObjects();
                var mirrorPercentValue = MirrorResPercent.Value;
                var mirrorPercent = Mathf.Clamp(mirrorPercentValue, 1, 150);
                if (mirrorPercent != mirrorPercentValue)
                {
                    MirrorResPercent.Value = mirrorPercent;
                }
                mirror.MaxTextureSizePercent = mirrorPercent;
                mirror.useVRAMOptimization = MirrorVram.Value;
                mirror.resolutionLimit = MirrorResOverride.Value ? maxRes : Mathf.Min(cvrMirror.m_TextureSize, maxRes);
                mirror.enabled = cvrMirror.enabled;
                mirror.MockMSAALevel = (MirrorReflection.AntiAliasing)msaa;
                mirror.clearFlags = (MirrorReflection.ClearFlags)cvrMirror.m_ClearFlags;
                mirror.backgroundColor = cvrMirror.m_CustomColor;
                bool isTransparent = cvrMirror.m_CustomColor.a < 0.99f && cvrMirror.m_ClearFlags == CVRMirror.MirrorClearFlags.Color;
                mirror.useOcclusionCulling = GetMirrorOcclusion(cvrMirror.m_UseOcclusionCulling, isTransparent);
                mirror.cullDistance = MirrorCullDistanceType.Value;
                mirror.maxCullDistance = MirrorCullDistanceValue.Value;
                var opt = MirrorOptimization.Value;
                mirror.useFrustum = opt == Optimization.Frustum || opt == Optimization.Both;
                mirror.useMasking = opt == Optimization.Both || opt == Optimization.Depth;
                mirror.OnWillRenderObject();

                return false;
            }
            private static bool GetMirrorOcclusion(bool mirrorSetting, bool isTransparent) 
            {
                return MirrorOcclusion.Value switch
                {
                    Occlusion.Default => mirrorSetting,
                    Occlusion.ForcedOff => false,
                    Occlusion.ForcedOn => true,
                    Occlusion.ForcedOnNotTransparent => !isTransparent,
                    _ => mirrorSetting,
                };
            }
            private static int msaa = 8;
            private static int maxRes = 4096;
            public static void Settings(string arg0, int arg1)
            {
                var resChange = string.Equals(arg0, "GraphicsMaxMirrorResolution");
                var msaaChange = string.Equals(arg0, "MirrorMsaaLevel");
                if (!resChange && !msaaChange)
                    return;
                if (resChange)
                    maxRes = MetaPort.Instance.settings.GetSettingInt("GraphicsMaxMirrorResolution");
                if (msaaChange)
                    msaa = MetaPort.Instance.settings.GetSettingInt("MirrorMsaaLevel");
                //MelonLogger.Msg($"resChange: {resChange} msaaChange: {msaaChange} maxRes: {maxRes}, msaa: {msaa} arg1: {arg1}");
            }
        }
        [HarmonyPatch(typeof(CVRMirror), nameof(CVRMirror.OnDisable))]
        class MirrorOnDisable 
        {
            public static void Postfix(CVRMirror __instance) 
            {
                if (mirrors.TryGetValue(__instance, out var mirror)) 
                {
                    mirror.OnDisable();
                }
            }
        }
        [HarmonyPatch(typeof(CVRMirror), nameof(CVRMirror.OnDestroy))]
        class MirrorOnDestroy
        {
            public static void Postfix(CVRMirror __instance)
            {
                if (mirrors.TryGetValue(__instance, out var mirror))
                {
                    mirror.OnDestroy();
                }
            }
        }
    }
    public class MirrorReflection
    {
        private const string LeftEyeTextureName = "_ReflectionTexLeft";
        private const string RightEyeTextureName = "_ReflectionTexRight";
        private const string ReflectionCameraName = "MirrorReflection Camera";
        private const string CullingCameraName = "Mirror culling camera";
        private const string MirrorScaleOffset = "Scale offset";
        internal const string GlobalShaderVectorNameCameraRect = "CameraRectSize";
        private const int MirrorLayer = 14;
        private const int MirrorLayerExcludeMask = ~(1 << MirrorLayer);

        public MirrorReflection(GameObject gameObject, LayerMask layerMask, bool disablePixelLights)
        {
            this.gameObject = gameObject;
            transform = gameObject.transform;
            m_ReflectLayers = layerMask;
            m_DisablePixelLights = disablePixelLights;
        }
        private readonly GameObject gameObject;
        public bool enabled = true;
        private readonly Transform transform;

        public AntiAliasing MockMSAALevel = AntiAliasing.MSAA_x8;
        public int resolutionLimit = 4096;
        public bool useVRAMOptimization = true;
        public bool m_DisablePixelLights = true;
        public bool useOcclusionCulling = true;
        public bool disableOcclusionWhenTransparent = false;
        public ClearFlags clearFlags = ClearFlags.Default;
        public Color backgroundColor = Color.clear;
        [Range(0.01f, 1f)]
        public float MaxTextureSizePercent = 100.0f;  
        public LayerMask m_ReflectLayers = -1;
        public bool useAverageNormals = false;
        public bool useFrustum = true;
        public bool keepNearClip = true;
        public bool useMasking = false;
        public CullDistance cullDistance = CullDistance.Off;
        public float maxCullDistance = 1000f;

        private bool hasCorners;
        private int actualMsaa;
        private bool useMsaaTexture;
        private int width;
        private int height;
        private Camera m_ReflectionCamera;
        private Camera m_CullingCamera;
        private Material[] m_MaterialsInstanced;
        private MaterialPropertyBlock materialPropertyBlock;
        private RenderTexture m_ReflectionTextureMSAA;
        public RenderTexture m_ReflectionTextureLeft;
        public RenderTexture m_ReflectionTextureRight;
        private Renderer m_Renderer;
        private Mesh m_Mesh;
        private Vector3 mirrorNormal = Vector3.up;
        private Vector3 mirrorNormalAvg = Vector3.up;
        private Vector3 mirrorPlaneOffset = Vector3.zero;
        private Vector3 meshMid;
        private Bounds boundsLocalSpace;
        private Vector3x4 meshCorners;
        private Matrix4x4 m_LocalToWorldMatrix;
        private Matrix4x4 meshTrs;
        private Transform scaleOffset;


        private CommandBuffer commandBufferClearRenderTargetToZero;
        private CommandBuffer commandBufferClearRenderTargetToOne;
        private CommandBuffer commandBufferSetRectFull;
        private CommandBuffer commandBufferSetRectLimited;

        private readonly List<Material> m_savedSharedMaterials = new List<Material>();   // Only relevant for the editor

        private static int LeftEyeTextureID = -1;
        private static int RightEyeTextureID = -1;
        internal static int CameraRectSizeShaderID = -1;
        private static bool s_InsideRendering = false;
        private static bool copySupported;
        private static RenderTexture cullingTex;
        private static Texture2D clearPixel;
        private static readonly Dictionary<Camera, Skybox> SkyboxDict = new Dictionary<Camera, Skybox>();
        private static readonly List<Material> m_tempSharedMaterials = new List<Material>();
        private static readonly List<Vector3> vertices = new List<Vector3>();
        private static readonly List<Vector3> mirrorVertices = new List<Vector3>();
        private static readonly List<int> indicies = new List<int>();
        private static readonly HashSet<int> ints = new HashSet<int>();
        private static readonly Vector3[] outCorners = new Vector3[4];
        private static readonly Plane[] planes = new Plane[6];

        private static readonly Quaternion rotateCamera = Quaternion.Euler(Vector3.up * 180f);
        private static readonly Matrix4x4 m_InversionMatrix = new Matrix4x4 { m00 = -1, m11 = 1, m22 = 1, m33 = 1 };
        private static readonly Vector4 MaxVector4 = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
        private static readonly Vector4 MinMaxVector4 = new Vector4(float.MaxValue, float.MinValue, float.MaxValue, float.MinValue);

        public enum AntiAliasing
        {
            Default = 0,
            MSAA_Off = 1,
            MSAA_x2 = 2,
            MSAA_x4 = 4,
            MSAA_x8 = 8,
        }
        public enum ClearFlags
        {
            Default = 0,
            Skybox = 1,
            Color = 2,
        }
        public enum CullDistance
        {
            Off,
            CameraFarClip,
            DistanceFromMirror,
        //    PerLayerDistancesFromMirror,  //not used in the mod
        }

        public void Start()
        {
            copySupported = SystemInfo.copyTextureSupport.HasFlag(CopyTextureSupport.Basic);

            // Move mirror to it's own layer
            gameObject.layer = MirrorLayer; //CVRReserved3

            materialPropertyBlock = new MaterialPropertyBlock();

            if (!cullingTex)
            {
                clearPixel = new Texture2D(1, 1, TextureFormat.ARGB32, 0, true);
                clearPixel.SetPixel(0, 0, Color.clear);
                clearPixel.name = "A single transparent pixel";
                clearPixel.Apply();
                cullingTex = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
                cullingTex.Create();
            }

            m_Renderer = gameObject.GetComponent<Renderer>();
            if (!m_Renderer)
            {
                enabled = false;
                MelonLogger.Error($"No renderer for mirror {gameObject.name}");
                return;
            }
            if (m_Renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                m_Mesh = filter.sharedMesh;
            }
            SetGlobalIdIfNeeded();
            if (m_Mesh)
            {
                // find the first material that has mirror shader properties
                m_Renderer.GetSharedMaterials(m_savedSharedMaterials);
                m_tempSharedMaterials.Clear();
                m_tempSharedMaterials.AddRange(m_savedSharedMaterials);
                int index = -1;
                for (int i = 0; i < m_tempSharedMaterials.Count; i++)
                {
                    var material = m_tempSharedMaterials[i];
                    if (material.HasProperty(LeftEyeTextureID) && material.HasProperty(RightEyeTextureID))
                    {
                        index = i;
                        break;
                    }
                }
                m_tempSharedMaterials.Clear();
                var readable = m_Mesh.isReadable;
                var mesh = m_Mesh;
                try
                {
                    if (!readable)
                    {
                        mesh = ReadItAnyway(mesh);
                    }
                    ReadMesh(mesh, index);
                    FindMeshCorners();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error during setup for mirror '{gameObject.name}'!", ex);
                }
                if (!readable)
                {
                    MelonLogger.Msg($"Mirror {gameObject.name} has a mesh that's non-readable, optimization might not work.");
                    mirrorNormal = mirrorNormalAvg = Vector3.up;
                }
            }

            // Make sure the mirror's material is unique
            m_MaterialsInstanced = m_Renderer.materials;
            
           // MelonLogger.Msg($"Mirror {gameObject.name} setup done.");
        }
        internal static void SetGlobalIdIfNeeded()
        {
            if (LeftEyeTextureID == -1)
                LeftEyeTextureID = Shader.PropertyToID(LeftEyeTextureName);
            if (RightEyeTextureID == -1)
                RightEyeTextureID = Shader.PropertyToID(RightEyeTextureName);
            if (CameraRectSizeShaderID == -1)
                CameraRectSizeShaderID = Shader.PropertyToID(GlobalShaderVectorNameCameraRect);
        }
        private Mesh ReadItAnyway(Mesh mesh)
        {
            GameObject go = new GameObject();
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            Mesh meshCopy = new Mesh();
            smr.BakeMesh(meshCopy);
            meshCopy.RecalculateBounds();
            GameObject.Destroy(go);
            return meshCopy;
        }
        private void FindMeshCorners()
        {
            (var min, var max, var mid) = GetMinMax(mirrorVertices);
            mirrorPlaneOffset = mid;

            var plane = new Plane(mirrorNormalAvg, mirrorPlaneOffset);
            var rot = Quaternion.FromToRotation(mirrorNormalAvg, Vector3.up);
            meshTrs = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);

            vertices.Add(plane.ClosestPointOnPlane(min));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(min.x, min.y, max.z)));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(min.x, max.y, min.z)));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(min.x, max.y, max.z)));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(max.x, min.y, min.z)));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(max.x, min.y, max.z)));
            vertices.Add(plane.ClosestPointOnPlane(new Vector3(max.x, max.y, min.z)));
            vertices.Add(plane.ClosestPointOnPlane(max));

            for (int i = 0; i < vertices.Count; i++)
            {
                // make them face forward on the Z axis
                vertices[i] = meshTrs.MultiplyPoint3x4(vertices[i]);
            }
            (min, max, meshMid) = GetMinMax(vertices);

            // the four corners of the mesh along it's plane in local space, 

            plane = new Plane(Vector3.up, meshTrs.MultiplyPoint3x4(mid));
            var meshC0 = plane.ClosestPointOnPlane(min);
            var meshC1 = plane.ClosestPointOnPlane(new Vector3(min.x, min.y, max.z));
            var meshC2 = plane.ClosestPointOnPlane(max);
            var meshC3 = plane.ClosestPointOnPlane(new Vector3(max.x, min.y, min.z));
            meshCorners = new Vector3x4 { c0 = meshC0, c1 = meshC1, c2 = meshC2, c3 = meshC3 };

            boundsLocalSpace = new Bounds(meshCorners[0], Vector3.zero);
            boundsLocalSpace.Encapsulate(meshCorners[0]);
            boundsLocalSpace.Encapsulate(meshCorners[1]);
            boundsLocalSpace.Encapsulate(meshCorners[2]);
            boundsLocalSpace.Encapsulate(meshCorners[3]);
            var size = boundsLocalSpace.size;
            size.y = size.x;
            boundsLocalSpace.Expand(Vector3.one * (0.001f / size.magnitude));
            hasCorners = true;
            mirrorVertices.Clear();
        }

        private void ReadMesh(Mesh mesh, int index)
        {
            mirrorNormal = mirrorNormalAvg = Vector3.up;
            Vector3 allNormals = Vector3.zero;
            if (mesh.subMeshCount > 1 && index >= 0 && index < mesh.subMeshCount)
            {
                ints.Clear();
                mesh.GetIndices(indicies, index);
                for (int i = 0; i < indicies.Count; i++)
                {
                    ints.Add(indicies[i]);
                }
                mesh.GetVertices(vertices);
                foreach (var item in ints)
                {
                    mirrorVertices.Add(vertices[item]);
                }
                vertices.Clear();
                var normals = vertices;
                mesh.GetNormals(normals);
                if (normals.Count > 0)
                    mirrorNormal = normals[0];

                foreach (var item in ints)
                {
                    allNormals += normals[item];
                }

                normals.Clear();
                ints.Clear();
            }
            else
            {
                var normals = vertices;
                mesh.GetVertices(mirrorVertices);
                mesh.GetNormals(normals);
                foreach (var item in normals)
                {
                    allNormals += item;
                }
                // Get the mirror mesh normal from the first vertex
                if (normals.Count > 0)
                    mirrorNormal = normals[0];
                normals.Clear();
            }

            mirrorNormalAvg = mirrorNormal;
            if (float.IsNaN(mirrorNormalAvg.x) || float.IsInfinity(mirrorNormalAvg.x) || float.IsNaN(mirrorNormalAvg.y) ||
                float.IsInfinity(mirrorNormalAvg.y) || float.IsNaN(mirrorNormalAvg.z) || float.IsInfinity(mirrorNormalAvg.z))
            {
                mirrorNormalAvg = mirrorNormal;
            }
        }

        private (Vector3 min, Vector3 max, Vector3 mid) GetMinMax(List<Vector3> list)
        {
            int count = list.Count;
            Vector3 min = Vector3.one * float.PositiveInfinity;
            Vector3 max = Vector3.one * float.NegativeInfinity;
            Vector3 mid = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var v = list[i];
                mid += v;
                if (v.x < min.x) min.x = v.x;
                if (v.y < min.y) min.y = v.y;
                if (v.z < min.z) min.z = v.z;
                if (v.x > max.x) max.x = v.x;
                if (v.y > max.y) max.y = v.y;
                if (v.z > max.z) max.z = v.z;
            }
            return (min, max, mid / count);
        }

        public void OnDestroy()
        {
            if (m_MaterialsInstanced == null || m_MaterialsInstanced.Length == 0)
                return;
            for (int i = 0; i < m_MaterialsInstanced.Length; i++)
            {
                GameObject.Destroy(m_MaterialsInstanced[i]);
            }
        }

        // This is called when it's known that the object will be rendered by some
        // camera. We render reflections and do other updates here.
        // Because the script executes in edit mode, reflections for the scene view
        // camera will just work!
        public void OnWillRenderObject()
        {
            Camera currentCam = Camera.current;
            if (!enabled || !m_Renderer || !m_Renderer.enabled || !currentCam)
            {
                return;
            }

            bool isStereo = currentCam.stereoEnabled;

            // Safeguard from recursive reflections.        
            if (s_InsideRendering || currentCam.orthographic)
            {
                materialPropertyBlock.SetTexture(LeftEyeTextureID, clearPixel);
                materialPropertyBlock.SetTexture(RightEyeTextureID, clearPixel);
                m_Renderer.SetPropertyBlock(materialPropertyBlock);
                return;
            }

            // Rendering will happen.

            // Force mirrors to their own layer
            if (gameObject.layer != MirrorLayer)
                gameObject.layer = MirrorLayer;

            actualMsaa = GetActualMSAA(currentCam, (int)MockMSAALevel);

            // Maximize the mirror texture resolution
            var res = UpdateRenderResolution(currentCam.pixelWidth, currentCam.pixelHeight, MaxTextureSizePercent, resolutionLimit);
            var widthHeightMsaa = new Vector3Int(res.x, res.y, GetActualMSAA(currentCam, (int)MockMSAALevel));
            width = res.x;
            height = res.y;
            var currentCamLtwm = currentCam.transform.localToWorldMatrix;

            useMsaaTexture = useVRAMOptimization && currentCam.actualRenderingPath == RenderingPath.Forward && actualMsaa > 1 && copySupported;

            // Set flag that we're rendering to a mirror
            s_InsideRendering = true;

            // Optionally disable pixel lights for reflection
            int oldPixelLightCount = QualitySettings.pixelLightCount;

            m_LocalToWorldMatrix = transform.localToWorldMatrix;
            Vector3 mirrorPos = m_LocalToWorldMatrix.GetColumn(3);  // transform.position
            Vector3 normal = GetMirrorNormal();

            if (m_DisablePixelLights)
                QualitySettings.pixelLightCount = 0;
            try
            {
                if (isStereo)
                {
                    var eye = currentCam.stereoActiveEye;
                    bool singlePass = XRSettings.stereoRenderingMode != XRSettings.StereoRenderingMode.MultiPass;
                    bool renderLeft = singlePass || eye == Camera.MonoOrStereoscopicEye.Left;
                    bool renderRight = singlePass || eye == Camera.MonoOrStereoscopicEye.Right;
                    if (renderLeft)
                        RenderCamera(currentCam, currentCamLtwm, mirrorPos, normal, widthHeightMsaa, Camera.MonoOrStereoscopicEye.Left);
                    if (renderRight)
                        RenderCamera(currentCam, currentCamLtwm, mirrorPos, normal, widthHeightMsaa, Camera.MonoOrStereoscopicEye.Right);
                }
                else
                {
                    RenderCamera(currentCam, currentCamLtwm, mirrorPos, normal, widthHeightMsaa, Camera.MonoOrStereoscopicEye.Mono);
                }
                if (m_ReflectionTextureLeft)
                    materialPropertyBlock.SetTexture(LeftEyeTextureID, m_ReflectionTextureLeft);
                if (m_ReflectionTextureRight)
                    materialPropertyBlock.SetTexture(RightEyeTextureID, m_ReflectionTextureRight);
                m_Renderer.SetPropertyBlock(materialPropertyBlock);
            }
            finally
            {
                s_InsideRendering = false;
                if (m_DisablePixelLights)
                    QualitySettings.pixelLightCount = oldPixelLightCount;
                if (useMsaaTexture || m_ReflectionTextureMSAA != null)
                {
                    try
                    {
                        RenderTexture.ReleaseTemporary(m_ReflectionTextureMSAA);
                    }
                    catch (NullReferenceException)
                    { }
                }
            }
        }

        private Vector3 GetMirrorNormal()
        {
            var normal = m_LocalToWorldMatrix.MultiplyVector(useAverageNormals ? mirrorNormalAvg : mirrorNormal).normalized;    // transform.TransformDirection

            if (IsSheared(m_LocalToWorldMatrix, 0.0002f))
            {
                var ltwm = m_Renderer.localToWorldMatrix * meshTrs;
                var _worldCorners = meshCorners.MultiplyPoint3x4(ltwm);
                var _plane = new Plane(_worldCorners[0], _worldCorners[1], _worldCorners[2]);
                var _normal = _plane.normal;
                _normal *= Mathf.Sign(Vector3.Dot(normal, _normal));
                normal = _normal;
            }

            return normal;
        }

        private bool IsSheared(Matrix4x4 ltwm, float limit)
        {
            var fw = ltwm.MultiplyVector(Vector3.forward);
            var up = ltwm.MultiplyVector(Vector3.up);
            var right = ltwm.MultiplyVector(Vector3.right);
            var a = Math.Abs(Vector3.Dot(fw, right));
            var b = Math.Abs(Vector3.Dot(fw, up));
            var c = Math.Abs(Vector3.Dot(up, right));
            return a > limit || b > limit || c > limit;
        }
        /// <summary>
        /// Get actual MSAA level from render texture (if the current camera renders to one) or QualitySettings
        /// </summary>
        private static int GetActualMSAA(Camera currentCam, int maxMSAA)
        {
            var targetTexture = currentCam.targetTexture;
            int cameraMsaa;
            if (targetTexture != null)
            {
                cameraMsaa = targetTexture.antiAliasing;
            }
            else
            {
                cameraMsaa = QualitySettings.antiAliasing == 0 ? 1 : QualitySettings.antiAliasing;
            }
            var actualMsaa = Mathf.Max(cameraMsaa, 1);
            if (maxMSAA > 0)
            {
                actualMsaa = Mathf.Min(actualMsaa, maxMSAA);
            }
            return actualMsaa;
        }

        private static Vector2Int UpdateRenderResolution(int width, int height, float MaxTextureSizePercent, int resolutionLimit)
        {
            var max = Mathf.Clamp(MaxTextureSizePercent, 1, 150) / 100f; 
            var size = width * height;
            var limit = resolutionLimit * resolutionLimit;
            double _maxRes = 0;
            if (size > limit && resolutionLimit < 4096) 
            {
                _maxRes = Math.Sqrt(limit / (float)size);
                max *= (float)_maxRes;
            }
            max = Mathf.Clamp(max, 0.001f, 1.5f);
            int w = (int)(width * max + 0.5f);
            int h = (int)(height * max + 0.5f);
            return new Vector2Int(w, h);
        }
        internal static void SetupMsaaTexture(ref RenderTexture rt, bool useMsaaTexture, Vector3Int widthHeightMsaa, bool allowHDR)
        {
            if (!useMsaaTexture && rt)
            {
                RenderTexture.ReleaseTemporary(rt);
                rt = null;
                return;
            }
            int width = widthHeightMsaa.x;
            int height = widthHeightMsaa.y;
            int msaa = widthHeightMsaa.z;
            if (rt && rt.IsCreated() && rt.width == width && rt.height == height && rt.antiAliasing == msaa &&
                rt.format == (allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32))
                return;
            SetupRenderTexture(ref rt, width, height, true, msaa, allowHDR);
        }
        private static void SetupRenderTexture(ref RenderTexture rt, int width, int height, bool depth, int msaa, bool allowHDR)
        {
            if (rt)
                RenderTexture.ReleaseTemporary(rt);
            rt = RenderTexture.GetTemporary(width, height, depth ? 24 : 0,
                allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, msaa);
        }
        private static bool IsVisible(Matrix4x4 localToWorldMatrix, float stereoSep, Camera.MonoOrStereoscopicEye eye, Vector3 mirrorPos, Vector3 normal, bool isStereo)
        {
            Vector4 currentCamPos = localToWorldMatrix.GetColumn(3);
            if (!isStereo)
            {
                if (!Visible(currentCamPos, mirrorPos, normal))
                    return false;
            }        // Replace with proper eye positions if possible
            else
            {
                var v3_eye = currentCamPos + localToWorldMatrix * ((eye == Camera.MonoOrStereoscopicEye.Left ? Vector3.left : Vector3.right) * stereoSep / 2f);
                var v3_eye2 = currentCamPos + localToWorldMatrix * ((eye == Camera.MonoOrStereoscopicEye.Left ? Vector3.left : Vector3.right) * stereoSep);

                if (!(Visible(v3_eye, mirrorPos, normal) || Visible(v3_eye2, mirrorPos, normal)))
                    return false;
            }
            return true;
        }
        private void RenderCamera(Camera currentCam, in Matrix4x4 localToWorldMatrix, Vector3 mirrorPos, Vector3 normal, Vector3Int widthHeightMsaa, Camera.MonoOrStereoscopicEye eye)
        {
            bool isStereo = eye != Camera.MonoOrStereoscopicEye.Mono;
            bool isRightEye = eye == Camera.MonoOrStereoscopicEye.Right;
            if (!IsVisible(localToWorldMatrix, currentCam.stereoSeparation, eye, mirrorPos, normal, isStereo))
                return;

            SetupMsaaTexture(ref m_ReflectionTextureMSAA, useMsaaTexture, widthHeightMsaa, currentCam.allowHDR);

            var reflectionTexture = isRightEye ? m_ReflectionTextureRight : m_ReflectionTextureLeft;
            var _mirrorNormal = useAverageNormals ? mirrorNormalAvg : mirrorNormal;
            CreateMirrorObjects(transform, ref reflectionTexture, ref scaleOffset, ref m_CullingCamera, ref m_ReflectionCamera, currentCam.allowHDR, widthHeightMsaa, _mirrorNormal, useMsaaTexture);
            var cullingRotation = m_CullingCamera.transform.localRotation;

            if (isRightEye)
                m_ReflectionTextureRight = reflectionTexture;
            else
                m_ReflectionTextureLeft = reflectionTexture;

            UpdateCameraModes(currentCam, m_ReflectionCamera, m_CullingCamera, clearFlags, backgroundColor);

            var targetTexture = useMsaaTexture ? m_ReflectionTextureMSAA : reflectionTexture;

            m_ReflectionCamera.useOcclusionCulling = useOcclusionCulling;
            m_ReflectionCamera.depthTextureMode = currentCam.depthTextureMode | DepthTextureMode.Depth;
            m_ReflectionCamera.stereoTargetEye = StereoTargetEyeMask.None;
            m_ReflectionCamera.targetTexture = targetTexture;
            m_ReflectionCamera.cullingMask = m_ReflectLayers.value & MirrorLayerExcludeMask; // mirrors never render their own layer mask: 1111 1111 1111 1111 1101 1111 1111 1111

            // set mirror to zero pos
            scaleOffset.transform.position -= mirrorPos;
            // camera mirrored to other side of mirror
            var worldToCameraMatrix = GetViewMatrix(m_CullingCamera, Vector3.zero, normal, eye, isStereo);
            m_CullingCamera.transform.localRotation = cullingRotation;
            m_ReflectionCamera.worldToCameraMatrix = worldToCameraMatrix;
            var wtinv = worldToCameraMatrix.inverse;
            var reflectedRot = (wtinv * m_InversionMatrix).rotation * rotateCamera;
            var reflectedPos = wtinv.MultiplyPoint(Vector3.zero);
            m_ReflectionCamera.transform.SetPositionAndRotation(reflectedPos, reflectedRot);

            var projectionMatrix = isStereo ? currentCam.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye) : currentCam.projectionMatrix;
            m_ReflectionCamera.projectionMatrix = m_InversionMatrix * projectionMatrix * m_InversionMatrix;

            var ltwm = m_Renderer.localToWorldMatrix;
            ltwm = Matrix4x4.TRS(mirrorPos, Quaternion.identity, Vector3.one).inverse * ltwm;
            ltwm *= meshTrs.inverse;
            var worldCorners = meshCorners.MultiplyPoint3x4(ltwm);
            if (!TryGetRectPixel(m_ReflectionCamera, worldCorners, boundsLocalSpace, ltwm, keepNearClip, out var frustum, out float nearDistance, out Rect pixelRect, out var mirrorPlane))
            {
                scaleOffset.transform.localPosition = Vector3.zero;
                return;
            }

            bool willUseFrustum = false;
            bool validRect = false;

            if (hasCorners && pixelRect.height >= 1 && pixelRect.width >= 1)
            {
                validRect = true;
            }
            if (useFrustum && validRect)
            {
                m_ReflectionCamera.pixelRect = pixelRect;
                m_ReflectionCamera.projectionMatrix = frustum;
                willUseFrustum = true;
                if (!keepNearClip)
                    m_ReflectionCamera.nearClipPlane = nearDistance;
            }

            if (cullDistance != CullDistance.Off)
            {
                var layerCullDistances = m_ReflectionCamera.layerCullDistances;
                switch (cullDistance)
                {
                    case CullDistance.CameraFarClip:
                        Array.Fill(layerCullDistances, m_ReflectionCamera.farClipPlane);
                        break;
                    case CullDistance.DistanceFromMirror:
                        Array.Fill(layerCullDistances, maxCullDistance + reflectedPos.magnitude + 1f);
                        break;
                }
                m_ReflectionCamera.layerCullSpherical = true;
                m_ReflectionCamera.layerCullDistances = layerCullDistances;
            }

            // Setup oblique projection matrix so that near plane is our reflection plane.
            // This way we clip everything below/above it for free.
            var clipPlane = CameraSpacePlane(worldToCameraMatrix, Vector3.zero, normal, 1.0f);
            m_ReflectionCamera.projectionMatrix = m_ReflectionCamera.CalculateObliqueMatrix(clipPlane);
            var useOcclusion = useOcclusionCulling;
            if (useOcclusionCulling)
            {
                ResetCullingCamera(m_CullingCamera, reflectedPos);
                var farclip = m_CullingCamera.farClipPlane = currentCam.farClipPlane;
                useOcclusion = SetCullingCameraProjectionMatrix(m_CullingCamera, m_ReflectionCamera, worldCorners, mirrorPlane, ltwm.MultiplyPoint3x4(meshMid), farclip);
            }
            scaleOffset.transform.localPosition = Vector3.zero;
            if (useOcclusion)
            {
                m_ReflectionCamera.cullingMatrix = m_CullingCamera.cullingMatrix;
            }
            m_ReflectionCamera.useOcclusionCulling = useOcclusion;
            m_ReflectionCamera.worldToCameraMatrix = GetViewMatrix(currentCam, mirrorPos, normal, eye, isStereo);
            SetGlobalShaderRect(m_ReflectionCamera.rect);


            var pixelWidth = targetTexture.width;
            var pixelHeight = targetTexture.height;
            var shouldUseMasking = ShouldUseMasking(pixelWidth, pixelHeight, pixelRect, useMasking, validRect);
            if (shouldUseMasking)
            {

                RenderWithCommanBuffers(pixelWidth, pixelHeight, pixelRect, willUseFrustum, validRect);
            }
            else
            {
                m_ReflectionCamera.Render();
            }
            ResetGlobalShaderRect();

            if (useMsaaTexture)
                CopyTexture(reflectionTexture, pixelRect, validRect);
        }
        private static bool ShouldUseMasking(int pixelWidth, int pixelHeight, Rect mirrorRect, bool useMasking, bool validRect)
        {
            if (!useMasking)
                return false;
            if (!validRect)
                return true;
            if ((mirrorRect.width / pixelWidth) > 0.95f && (mirrorRect.height / pixelHeight) > 0.95f)
                return false;
            return true;
        }

        private void RenderWithCommanBuffers(int pixelWidth, int pixelHeight, Rect pixelRect, bool willUseFrustum, bool validRect)
        {
            var cameraEvent = CameraEvent.BeforeForwardOpaque;
            CreateCommandBuffers(pixelWidth, pixelHeight, pixelRect);
            AddBuffers(cameraEvent, willUseFrustum, validRect);
            m_ReflectionCamera.Render();
            RemoveBuffers(cameraEvent);
        }

        private void AddBuffers(CameraEvent cameraEvent, bool willUseFrustum, bool validRect)
        {
            m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferSetRectFull);
            m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferClearRenderTargetToZero);

            if (validRect)
            {
                m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferSetRectLimited);
                m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferClearRenderTargetToOne);
                m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferSetRectFull);
            }

            if (willUseFrustum)
                m_ReflectionCamera.AddCommandBuffer(cameraEvent, commandBufferSetRectLimited);
        }
        private void RemoveBuffers(CameraEvent cameraEvent)
        {
            m_ReflectionCamera.RemoveCommandBuffer(cameraEvent, commandBufferClearRenderTargetToZero);
            m_ReflectionCamera.RemoveCommandBuffer(cameraEvent, commandBufferSetRectFull);
            m_ReflectionCamera.RemoveCommandBuffer(cameraEvent, commandBufferSetRectLimited);
            m_ReflectionCamera.RemoveCommandBuffer(cameraEvent, commandBufferClearRenderTargetToOne);
        }
        private void CreateCommandBuffers(int pixelWidth, int pixelHeight, Rect pixelRectLimited)
        {
            var zeroCreated = CreateCommandBuffer(ref commandBufferClearRenderTargetToZero, "Occlusion buffer - ClearRenderTarget 0", clear: false);
            var oneCreate = CreateCommandBuffer(ref commandBufferClearRenderTargetToOne, "Occlusion buffer - ClearRenderTarget 1", clear: false);
            CreateCommandBuffer(ref commandBufferSetRectFull, "Occlusion buffer - SetRect full", clear: true);
            CreateCommandBuffer(ref commandBufferSetRectLimited, "Occlusion buffer - SetRect limited", clear: true);

            if (zeroCreated)
                commandBufferClearRenderTargetToZero.ClearRenderTarget(clearDepth: true, clearColor: false, backgroundColor: Color.blue, depth: 0);
            if (oneCreate)
                commandBufferClearRenderTargetToOne.ClearRenderTarget(clearDepth: true, clearColor: false, backgroundColor: Color.blue, depth: 1);

            commandBufferSetRectFull.SetViewport(new Rect(0, 0, pixelWidth, pixelHeight));
            commandBufferSetRectLimited.SetViewport(pixelRectLimited);
        }
        private bool CreateCommandBuffer(ref CommandBuffer _commandBuffer, string name, bool clear)
        {
            if (_commandBuffer == null)
            {
                _commandBuffer = new CommandBuffer { name = name };
                return true;
            }
            else if (clear)
            {
                _commandBuffer.Clear();
            }
            return false;
        }
        internal static void SetGlobalShaderRect(Rect rect)
        {
            var value = new Vector4(rect.x, rect.y, 1f - rect.width, 1f - rect.height);
            Shader.SetGlobalVector(CameraRectSizeShaderID, value);
        }
        internal static void ResetGlobalShaderRect()
        {
            Shader.SetGlobalVector(CameraRectSizeShaderID, Vector4.zero);
        }

        private static void ResetCullingCamera(Camera m_CullingCamera, Vector3 cullingPos)
        {
            m_CullingCamera.Reset();
            m_CullingCamera.transform.position = cullingPos;
            m_CullingCamera.ResetWorldToCameraMatrix();
            m_CullingCamera.fieldOfView = 179f;
            m_CullingCamera.targetTexture = cullingTex;
        }

        private static Matrix4x4 GetViewMatrix(Camera currentCam, Vector3 mirrorPos, Vector3 normal, Camera.MonoOrStereoscopicEye eye, bool isStereo)
        {
            var viewMatrix = isStereo ? currentCam.GetStereoViewMatrix((Camera.StereoscopicEye)eye) : currentCam.worldToCameraMatrix;

            // find out the reflection plane: position and normal in world space
            float d = -Vector3.Dot(normal, mirrorPos);
            Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            // Reflect camera around reflection plane
            Matrix4x4 worldToCameraMatrix = CalculateReflectionMatrix(reflectionPlane);

            worldToCameraMatrix = m_InversionMatrix * viewMatrix * worldToCameraMatrix;

            return worldToCameraMatrix;
        }

        private void CopyTexture(RenderTexture rt, Rect rect, bool validRect)
        {
            if (validRect)
            {
                var pos = rect.position;
                var size = rect.size;
                int x = Math.Max(0, (int)(pos.x - 0.01f));
                int y = Math.Max(0, (int)(pos.y - 0.01f));
                int w = (int)(size.x + 0.01f) + 2;
                int h = (int)(size.y + 0.01f) + 2;
                w = Math.Min(x + w, m_ReflectionTextureMSAA.width) - x;
                h = Math.Min(y + h, m_ReflectionTextureMSAA.height) - y;
                Graphics.CopyTexture(m_ReflectionTextureMSAA, 0, 0, x, y, w, h, rt, 0, 0, x, y);
            }
            else
            {
                Graphics.CopyTexture(m_ReflectionTextureMSAA, rt);
            }
        }


        // Cleanup all the objects we possibly have created
        public void OnDisable()
        {
            if (m_ReflectionTextureLeft)
            {
                RenderTexture.ReleaseTemporary(m_ReflectionTextureLeft);
                m_ReflectionTextureLeft = null;
            }
            if (m_ReflectionTextureRight)
            {
                RenderTexture.ReleaseTemporary(m_ReflectionTextureRight);
                m_ReflectionTextureRight = null;
            }
            if (m_ReflectionTextureMSAA)
            {
                RenderTexture.ReleaseTemporary(m_ReflectionTextureMSAA);
                m_ReflectionTextureMSAA = null;
            }
            materialPropertyBlock.SetTexture(LeftEyeTextureID, clearPixel);
            materialPropertyBlock.SetTexture(RightEyeTextureID, clearPixel);
            m_Renderer.SetPropertyBlock(materialPropertyBlock);
        }
        internal static void UpdateCameraModes(Camera src, Camera dest, Camera dest2, ClearFlags clearFlags, Color backgroundColor)
        {
            if (!dest)
                return;
            // set camera to clear the same way as current camera
            dest.CopyFrom(src);
            dest.enabled = false;
            if (dest2)
            {
                dest2.CopyFrom(src);
                dest2.enabled = false;
                var srcTransform = src.transform;
                dest2.transform.SetPositionAndRotation(srcTransform.position, srcTransform.rotation);
                dest2.transform.localScale = srcTransform.lossyScale;
                dest2.ResetWorldToCameraMatrix();
            }
            if (clearFlags != ClearFlags.Color && src.clearFlags == CameraClearFlags.Skybox)
            {
                Skybox sky = GetSkybox(src);
                Skybox mysky = GetSkybox(dest);
                if (!mysky)
                    return;
                if (!sky || !sky.material)
                {
                    mysky.enabled = false;
                }
                else
                {
                    mysky.enabled = true;
                    mysky.material = sky.material;
                }
            }
            else if (clearFlags == ClearFlags.Color)
            {
                dest.clearFlags = CameraClearFlags.Color;
                dest.backgroundColor = backgroundColor;
            }
        }
        private static Skybox GetSkybox(Camera camera)
        {
            if (!SkyboxDict.TryGetValue(camera, out Skybox skybox))
            {
                SkyboxDict[camera] = camera.GetComponent<Skybox>();
            }
            return skybox;
        }

        // On-demand create any objects we need
        internal static void CreateMirrorObjects(Transform transform, ref RenderTexture reflectionTexture, ref Transform scaleOffset, ref Camera m_CullingCamera, ref Camera m_ReflectionCamera,
         bool allowHDR, Vector3Int widthHeightMsaa, Vector3 normal, bool useMsaaTexture)
        {
            int width = widthHeightMsaa.x;
            int height = widthHeightMsaa.y;
            int actualMsaa = widthHeightMsaa.z;
            // Reflection render texture
            int msaa = useMsaaTexture ? 1 : actualMsaa;
            SetupRenderTexture(ref reflectionTexture, width, height, !useMsaaTexture, msaa, allowHDR);
            var hideFlags = Application.isEditor ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            if (!scaleOffset)
            {
                scaleOffset = new GameObject(MirrorScaleOffset).transform;
                scaleOffset.SetParent(transform, true);
                scaleOffset.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                scaleOffset.gameObject.hideFlags = hideFlags;
            }
            var scale = transform.lossyScale;
            var newScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            if (IsGood(newScale))
            {
                scaleOffset.localScale = newScale;
            }
            if (!m_CullingCamera)
            {
                GameObject _culling = new GameObject(CullingCameraName);
                _culling.transform.SetParent(scaleOffset);
                _culling.transform.localPosition = -normal;
                _culling.transform.LookAt(transform.position);
                _culling.SetActive(false);
                m_CullingCamera = _culling.AddComponent<Camera>();
                _culling.hideFlags = hideFlags;
            }
            m_CullingCamera.enabled = false;
            if (m_ReflectionCamera)
            {
                m_ReflectionCamera.enabled = false;
                return; // Camera already exists
            }
            // Create Camera for reflection
            GameObject cameraGameObject = new GameObject(ReflectionCameraName);
            cameraGameObject.transform.SetParent(scaleOffset);
            cameraGameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_ReflectionCamera = cameraGameObject.AddComponent<Camera>();
            SkyboxDict[m_ReflectionCamera] = cameraGameObject.AddComponent<Skybox>();
            m_ReflectionCamera.enabled = false;
            m_ReflectionCamera.gameObject.AddComponent<FlareLayer>();
            m_ReflectionCamera.clearFlags = CameraClearFlags.Nothing;
            cameraGameObject.hideFlags = hideFlags;
        }
        private static bool IsGood(Vector3 v3)
        {
            return !float.IsNaN(v3.x) && !float.IsNaN(v3.y) && !float.IsNaN(v3.z) && !float.IsInfinity(v3.x) && !float.IsInfinity(v3.y) && !float.IsInfinity(v3.z);
        }

        public static bool Visible(in Vector3 viewPosition, in Vector3 objectPosition, in Vector3 objectNormal)
        {
            return Vector3.Dot(viewPosition - objectPosition, objectNormal) > 0;
        }


        // Given position/normal of the plane, calculates plane in camera space.
        public static Vector4 CameraSpacePlane(Matrix4x4 worldToCameraMatrix, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 lhs = worldToCameraMatrix.MultiplyPoint(pos);
            Vector3 rhs = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(rhs.x, rhs.y, rhs.z, -Vector3.Dot(lhs, rhs));
        }

        // Calculates reflection matrix around the given plane
        public static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            float x = plane.x;
            float y = plane.y;
            float z = plane.z;
            float w = plane.w;

            column0.x = 1f - 2f * x * x;
            column1.x = -2f * x * y;
            column2.x = -2f * x * z;
            column3.x = -2f * w * x;

            column0.y = -2f * y * x;
            column1.y = 1f - 2f * y * y;
            column2.y = -2f * y * z;
            column3.y = -2f * w * y;

            column0.z = -2f * z * x;
            column1.z = -2f * z * y;
            column2.z = 1f - 2f * z * z;
            column3.z = -2f * w * z;

            return new Matrix4x4(column0, column1, column2, column3);
        }
        private static Vector4 column0 = new Vector4(0, 0, 0, 0);
        private static Vector4 column1 = new Vector4(0, 0, 0, 0);
        private static Vector4 column2 = new Vector4(0, 0, 0, 0);
        private static Vector4 column3 = new Vector4(0, 0, 0, 1);

        private static bool TryGetRectPixel(Camera camera, Vector3x4 worldCorners, Bounds boundsLocalSpace, Matrix4x4 ltwm, bool keepNear,
          out Matrix4x4 frustum, out float nearDistance, out Rect pixelRect, out Plane mirrorPlane)
        {
            Span<Vector3> allCorners = stackalloc Vector3[24];
            frustum = Matrix4x4.identity;
            pixelRect = new Rect(0, 0, 0, 0);
            mirrorPlane = new Plane(worldCorners.c0, worldCorners.c1, worldCorners.c2);
            var cameraPos = camera.cameraToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            var index = CheckCorners(camera, cameraPos, worldCorners, mirrorPlane, boundsLocalSpace, ltwm, allCorners);
            allCorners = allCorners.Slice(0, index);

            bool negativeZ = FindExtremesZ(allCorners, out Vector4 extremes, out nearDistance);
            if (negativeZ)
                return false;

            if (index > 0 && index < 4)
                return true;

            var left = extremes.x;
            var right = extremes.y;
            var bottom = extremes.z;
            var top = extremes.w;
            var width = right - left;
            var height = top - bottom;

            if (width <= 0 || height <= 0)
            {
                // mirror not visible
                return false;
            }

            var texture = camera.targetTexture;
            int textureWidth = texture.width;
            var textureHeight = texture.height;

            var leftInt = (int)((left * textureWidth) + 0.5f);
            var rightInt = (int)((right * textureWidth) + 0.5f);
            var topInt = (int)((top * textureHeight) + 0.5f);
            var bottomInt = (int)((bottom * textureHeight) + 0.5f);
            var widthInt = rightInt - leftInt;
            var heightInt = topInt - bottomInt;

            //pixelRect = new Rect(left + 0.0001f, bottom + 0.0001f, width - 0.0002f, height - 0.0002f);
            pixelRect = new Rect(leftInt, bottomInt, widthInt, heightInt);

            left = (float)leftInt / textureWidth;
            right = (float)rightInt / textureWidth;
            top = (float)topInt / textureHeight;
            bottom = (float)bottomInt / textureHeight;
            width = right - left;
            height = top - bottom;

            var frustumRect = new Rect(left, bottom, width, height);
            var nearPlane = keepNear ? camera.nearClipPlane : nearDistance;
            camera.CalculateFrustumCorners(frustumRect, nearPlane, Camera.MonoOrStereoscopicEye.Mono, outCorners);
            FindExtremes(outCorners, out extremes);
            frustum = Matrix4x4.Frustum(extremes.x, extremes.y, extremes.z, extremes.w, nearPlane, camera.farClipPlane);
            return true;
        }

        private bool SetCullingCameraProjectionMatrix(Camera cullingCamera, Camera m_ReflectionCamera, Vector3x4 worldCorners, Plane mirrorPlane, Vector3 midPoint, float farClipPlane)
        {
            Span<Vector3> span = stackalloc Vector3[4];
            var far = m_ReflectionCamera.farClipPlane;
            var mirrorPos = m_ReflectionCamera.cameraToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
            var mirrorNormal = mirrorPlane.normal;

            // get the 4 corners of the viewport in world space at the far clip distance. the order is important.

            span[0] = m_ReflectionCamera.ViewportToWorldPoint(new Vector3(0.00001f, 1f - 0.00001f, far));
            span[1] = m_ReflectionCamera.ViewportToWorldPoint(new Vector3(0.00001f, 0.00001f, far));
            span[2] = m_ReflectionCamera.ViewportToWorldPoint(new Vector3(1 - 0.00001f, 0.00001f, far));
            span[3] = m_ReflectionCamera.ViewportToWorldPoint(new Vector3(1 - 0.00001f, 1 - 0.00001f, far));

            // 4 lines (far points to mirror camera) intersecting the mirror plane

            for (int i = 0; i < 4; i++)
            {
                if (mirrorPlane.GetSide(span[i]))
                {
                    // line segment (far point to mirror camera) intersecting the mirror plane
                    bool c = SegmentPlane(mirrorPos, span[i], midPoint, mirrorNormal, out var point);
                    // translate these points from world space to viewport points on the culling camera
                    span[i] = cullingCamera.WorldToViewportPoint(point);
                }
                else
                {
                    // if the point is behind the mirror, use the corresponding worldspace corner. 
                    span[i] = cullingCamera.WorldToViewportPoint(worldCorners[i]);
                }
            }

            var left = Min4(span[0].x, span[1].x, span[2].x, span[3].x);
            var right = Max4(span[0].x, span[1].x, span[2].x, span[3].x);
            var bottom = Min4(span[0].y, span[1].y, span[2].y, span[3].y);
            var top = Max4(span[0].y, span[1].y, span[2].y, span[3].y);
            var width = right - left;
            var height = top - bottom;
            var viewport = new Rect(left, bottom, width, height);
            var cullDistance = Min4(span[0].z, span[1].z, span[2].z, span[3].z);

            cullingCamera.CalculateFrustumCorners(viewport, cullDistance, Camera.MonoOrStereoscopicEye.Mono, outCorners);
            cullingCamera.projectionMatrix = Matrix4x4.Frustum(outCorners[0].x, outCorners[2].x, outCorners[0].y, outCorners[2].y, cullDistance, farClipPlane);
            return true;
        }
        private static float Min4(float a, float b, float c, float d) => Mathf.Min(Mathf.Min(Mathf.Min(a, b), c), d);
        private static float Max4(float a, float b, float c, float d) => Mathf.Max(Mathf.Max(Mathf.Max(a, b), c), d);
        private static void FindExtremes(Span<Vector3> outCorners, out Vector4 result)
        {
            result = MinMaxVector4;
            for (int i = 0; i < outCorners.Length; i++)
            {
                var current = outCorners[i];
                if (result.x > current.x) { result.x = current.x; }
                if (result.y < current.x) { result.y = current.x; }
                if (result.z > current.y) { result.z = current.y; }
                if (result.w < current.y) { result.w = current.y; }
            }
        }
        /// <param name="result">left, right, bottom, top</param>
        private static bool FindExtremesZ(Span<Vector3> allCorners, out Vector4 result, out float minZ)
        {
            result = MinMaxVector4;
            Vector4 min = MaxVector4;
            for (int i = 0; i < allCorners.Length; i++)
            {
                var current = allCorners[i];
                if (result.x > current.x && current.z > 0.001f) { result.x = current.x; if (current.z < min.x) min.x = current.z; }
                if (result.y < current.x && current.z > 0.001f) { result.y = current.x; if (current.z < min.y) min.y = current.z; }
                if (result.z > current.y && current.z > 0.001f) { result.z = current.y; if (current.z < min.z) min.z = current.z; }
                if (result.w < current.y && current.z > 0.001f) { result.w = current.y; if (current.z < min.w) min.w = current.z; }
            }
            minZ = Min4(min.x, min.y, min.z, min.w);
            return minZ <= 0f;
        }

        /// <summary>
        /// Check if any point has a negative depth, and generate new points from the intersection of camera frustum planes and the edges of the mirror
        /// </summary>
        private static int CheckCorners(Camera camera, Vector3 cameraPos, Vector3x4 worldCorners, Plane mirrorPlane, Bounds boundsLocalSpace, Matrix4x4 ltwm, Span<Vector3> allCornersSpan)
        {
            int index = 0;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            Span<Vector3> span = stackalloc Vector3[4];
            span[0] = worldCorners[0];
            span[1] = worldCorners[1];
            span[2] = worldCorners[2];
            span[3] = worldCorners[3];
            index += GetScreenCorners(camera, span, allCornersSpan);
            if (index < 4)
                index += CalculateAdditionalPoints(ltwm, boundsLocalSpace, worldCorners, cameraPos, mirrorPlane, camera, allCornersSpan.Slice(index));
            return index;
        }
        private static int CalculateAdditionalPoints(Matrix4x4 ltwm, Bounds boundsLocalSpace, Vector3x4 corners, Vector3 cameraPos, Plane mirrorPlane, Camera camera, Span<Vector3> allCornersSpan)
        {
            Span<Vector3> span = stackalloc Vector3[20];
            int index = 0;
            var wtlm = ltwm.inverse;
            Vector3 I;
            if (PlanesIntersectAtSinglePoint(mirrorPlane, planes[0], planes[2], out I) && boundsLocalSpace.Contains(wtlm.MultiplyPoint3x4(I))) span[index++] = I;
            if (PlanesIntersectAtSinglePoint(mirrorPlane, planes[0], planes[3], out I) && boundsLocalSpace.Contains(wtlm.MultiplyPoint3x4(I))) span[index++] = I;
            if (PlanesIntersectAtSinglePoint(mirrorPlane, planes[1], planes[2], out I) && boundsLocalSpace.Contains(wtlm.MultiplyPoint3x4(I))) span[index++] = I;
            if (PlanesIntersectAtSinglePoint(mirrorPlane, planes[1], planes[3], out I) && boundsLocalSpace.Contains(wtlm.MultiplyPoint3x4(I))) span[index++] = I;
            for (int i = 0; i < 4; i++)
            {
                var corner = corners[i];
                var nextCorner = corners[i + 1];
                var prevCorner = corners[i - 1];
                for (int j = 0; j < 4; j++)
                {
                    if (SegmentPlane(corner, nextCorner, cameraPos, planes[j].normal, out I) && boundsLocalSpace.Contains(wtlm.MultiplyPoint3x4(I))) span[index++] = I;
                }
            }
            if (index > 0)
                return GetScreenCorners(camera, span.Slice(0, index), allCornersSpan);
            return 0;
        }
        private static int GetScreenCorners(Camera camera, Span<Vector3> worldPoints, Span<Vector3> allCornersSpan)
        {
            int index = 0;
            for (int i = 0; i < worldPoints.Length; i++)
            {
                var value = camera.WorldToViewportPoint(worldPoints[i]);
                if (CornerWithin01(value))
                {
                    allCornersSpan[index++] = value;
                }
            }
            return index;
        }
        private static bool CornerWithin01(Vector3 point) =>
            point.z >= 0f
            && point.x < 1.0001f && point.x > -0.0001f
            && point.y <= 1.0001f && point.y >= -0.0001f;

        /// <summary>
        /// Find the intersection between a line segment and a plane.
        /// </summary>
        /// <param name="p0">Start point of the line segment</param>
        /// <param name="p1">End point of the line segment</param>
        /// <param name="planeCenter">Position of the plane</param>
        /// <param name="planeNormal">Normal of the plane</param>
        /// <param name="I">Intersection point</param>
        /// <returns>true if there is an intersection</returns>
        /// https://gamedev.stackexchange.com/questions/178477/whats-wrong-with-my-line-segment-plane-intersection-code
        private static bool SegmentPlane(Vector3 p0, Vector3 p1, Vector3 planeCenter, Vector3 planeNormal, out Vector3 I)
        {
            Vector3 u = p1 - p0;
            Vector3 w = p0 - planeCenter;

            float D = Vector3.Dot(planeNormal, u);
            float N = -Vector3.Dot(planeNormal, w);

            if (Mathf.Abs(D) < Mathf.Epsilon)
            {
                // segment is parallel to plane
                if (N == 0)                         // segment lies in plane
                {
                    I = p0;                         // We could return anything between p0 and p1 here, all points are on the plane
                    return true;
                }
                else
                {
                    I = Vector3.zero;
                    return false;                   // no intersection
                }
            }

            // they are not parallel
            // compute intersect param
            float sI = N / D;
            if (sI < 0 || sI > 1)
            {
                I = Vector3.zero;
                return false;                       // no intersection
            }
            I = p0 + sI * u;                        // compute segment intersect point
            return true;
        }
        // https://gist.github.com/StagPoint/2eaa878f151555f9f96ae7190f80352e
        private static bool PlanesIntersectAtSinglePoint(in Plane p0, in Plane p1, in Plane p2, out Vector3 intersectionPoint)
        {
            //const float EPSILON = 1e-4f;

            var p0normal = p0.normal;
            var p1normal = p1.normal;
            var p2normal = p2.normal;

            var det = (Vector3.Dot(Vector3.Cross(p0normal, p1normal), p2normal));
            if (Mathf.Abs(det) < Mathf.Epsilon)
            {
                intersectionPoint = Vector3.zero;
                return false;
            }

            intersectionPoint =
                (-(p0.distance * Vector3.Cross(p1normal, p2normal)) -
                (p1.distance * Vector3.Cross(p2normal, p0normal)) -
                (p2.distance * Vector3.Cross(p0normal, p1normal))) / det;

            return true;
        }

        private struct Vector3x4
        {
            public Vector3 c0;
            public Vector3 c1;
            public Vector3 c2;
            public Vector3 c3;
            public int Length => 4;

            public Vector3 this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case -1: return c3;
                        case 0: return c0;
                        case 1: return c1;
                        case 2: return c2;
                        case 3: return c3;
                        case 4: return c0;
                        default: return Vector3.zero;
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0: c0 = value; return;
                        case 1: c1 = value; return;
                        case 2: c2 = value; return;
                        case 3: c3 = value; return;
                        default: return;
                    }
                }
            }
            public Vector3x4 MultiplyPoint3x4(Matrix4x4 ltwm) => new Vector3x4 { c0 = ltwm.MultiplyPoint3x4(c0), c1 = ltwm.MultiplyPoint3x4(c1), c2 = ltwm.MultiplyPoint3x4(c2), c3 = ltwm.MultiplyPoint3x4(c3) };
        }
    }
}
