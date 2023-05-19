using Force.Crc32;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.Text;

namespace Zettai
{
#pragma warning disable 612, 618
    public class AvatarInfo
    {
        public string avatarName;
        public double profiledVRamUseMB;
        public double calculatedVRamUseMB;
        public long profiledVRamUse;
        public long calculatedVRamUse;
        public string AvatarInfoString;
        public double AudioClipSizeMB;
        public int AudioClipCount;
        public int AudioClipLength;
        public long AudioClipSize;
        public int AudioSources;
        public int FaceCount;
        public int meshRenderers;
        public int skinnedMeshRenderers;
        public int lineTrailRenderers;
        public ulong lineTrailRendererTriCount;
        public int clothNumber;
        public int clothVertCount;
        public float clothDiff;
        public int dbScriptCount;
        public int dbParticleCount;
        public int dbTransformCount;
        public int dbCollisionCount;
        public int MagicaBoneClothCount;
        public int MagicaBoneSpringCount;
        public int MagicaMeshClothCount;
        public int MagicaMeshSpringCount;
        public int MagicaSphereColliderCount;
        public int MagicaCapsuleColliderCount;
        public int MagicaPlaneColliderCount;
        public int MagicaRenderDeformerCount;
        public int MagicaVirtualDeformerCount;
        public int materialCount;
        public int passCount;
        public int TransformCount;
        public int RigidBodyCount;
        public int ColliderCount;
        public int JointCount;
        public int ConstraintCount;
        public int Animators;
        public int OtherFinalIKComponents;
        public int VRIKComponents;
        public int TwistRelaxers;
        public int MaxHiearchyDepth;
        public int Lights;
        public int skinnedBonesVRC;
        public int skinnedBones;
        public int particleSystems;
        public long maxParticles;
        public int otherRenderers;
        public int additionalShaderKeywordCount;
        public int readableTextures;
        public int nonMipmappedTextures;
        public int crunchedTextures;
        public List<string> shaderKeywords = new List<string>();
        public List<string> additionalShaderKeywords = new List<string>();
        public List<string> shaderNames = new List<string>();
        public List<string> materialNames = new List<string>();
        public List<MaterialInfo> materialInfo = new List<MaterialInfo>();
        public string LongestPath;
        public string _MillisecsTaken;
        public bool ShouldLog;
        public ulong VRAM_Textures;
        //     public ulong VRAM_Meshes;
        public ulong VRAM_MeshesProfiler;
        internal string dlId;

        //     public ulong VramBlendshapes;
        private static readonly StringBuilder stringBuilder = new StringBuilder();
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        public void StartWatch() => stopwatch.Start();
        public void StopWatch() => stopwatch.Stop();

        private bool HasMagica() 
        {
            if (MagicaBoneClothCount > 0) return true;
            if (MagicaBoneSpringCount > 0) return true;
            if (MagicaMeshClothCount > 0) return true;
            if (MagicaMeshSpringCount > 0) return true;
            if (MagicaSphereColliderCount > 0) return true;
            if (MagicaCapsuleColliderCount > 0) return true;
            if (MagicaPlaneColliderCount > 0) return true;
            if (MagicaRenderDeformerCount > 0) return true;
            if (MagicaVirtualDeformerCount > 0) return true;
            return false;
        }
        public override string ToString()
        {
            stringBuilder.Clear();

            if (!string.IsNullOrEmpty(avatarName))
            {
                stringBuilder.AppendLine();
                if (!string.IsNullOrEmpty(dlId))
                {
                    stringBuilder.Append("[DL-");
                    stringBuilder.Append(dlId);
                    stringBuilder.Append("] ");
                }
                stringBuilder.Append("Avatar name/id: ");
                stringBuilder.Append(avatarName); 
                stringBuilder.AppendLine();
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Rendering -------------------");
            stringBuilder.Append("Triangle count:                   ");
            stringBuilder.Append(FaceCount); stringBuilder.AppendLine();
            stringBuilder.Append("Trail renderers triangle count:   ");
            stringBuilder.Append(lineTrailRendererTriCount); stringBuilder.AppendLine();
            stringBuilder.Append("Material count:                   ");
            stringBuilder.Append(materialCount); stringBuilder.AppendLine();
            stringBuilder.Append("Texture VRAM use:                 ");
            stringBuilder.Append(calculatedVRamUseMB);
            stringBuilder.Append(" MB"); stringBuilder.AppendLine();
            if (VRAM_MeshesProfiler > 0)
            {
                stringBuilder.Append("Mesh VRAM use:                    ");
                stringBuilder.Append(Math.Round(VRAM_MeshesProfiler / 10485.76f) / 100f);
                stringBuilder.Append(" MB"); stringBuilder.AppendLine();
            }
            stringBuilder.Append("Textures marked as readable:      ");
            stringBuilder.Append(readableTextures); stringBuilder.AppendLine();
            stringBuilder.Append("Non-Mipmapped Textures count:     ");
            stringBuilder.Append(nonMipmappedTextures); stringBuilder.AppendLine();
            stringBuilder.Append("Crunched Textures count:          ");
            stringBuilder.Append(crunchedTextures); stringBuilder.AppendLine();


            stringBuilder.Append("SkinnedMeshRenderer count:        ");
            stringBuilder.Append(skinnedMeshRenderers); stringBuilder.AppendLine();
            stringBuilder.Append("MeshRenderer count:               ");
            stringBuilder.Append(meshRenderers); stringBuilder.AppendLine();
            stringBuilder.Append("Line/trail renderer count:        ");
            stringBuilder.Append(lineTrailRenderers); stringBuilder.AppendLine();
            stringBuilder.Append("Other renderer count:             ");
            stringBuilder.Append(otherRenderers); stringBuilder.AppendLine();
            stringBuilder.Append("Additional shader keyword count:  ");
            stringBuilder.Append(additionalShaderKeywordCount); stringBuilder.AppendLine();
            stringBuilder.Append("Lights:                           ");
            stringBuilder.Append(Lights); stringBuilder.AppendLine();

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Particle Systems ------------");
            if (particleSystems > 0)
            {
                stringBuilder.Append("Particle system count:            ");
                stringBuilder.Append(particleSystems); stringBuilder.AppendLine();
                stringBuilder.Append("Maximum number of particles:      ");
                stringBuilder.Append(maxParticles); stringBuilder.AppendLine();
            }
            else
            {
                stringBuilder.AppendLine("No Particle Systems.              ");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Audio -----------------------");
            if (AudioSources > 0)
            {
                stringBuilder.Append("AudioSource count:                ");
                stringBuilder.Append(AudioSources); stringBuilder.AppendLine();
                stringBuilder.Append("AudioClip count:                  ");
                stringBuilder.Append(AudioClipCount); stringBuilder.AppendLine();
                stringBuilder.Append("Total AudioClip length:           ");
                stringBuilder.Append(AudioClipLength); stringBuilder.AppendLine();
                stringBuilder.Append("Total AudioClip Size:             ");
                stringBuilder.Append(AudioClipSizeMB); stringBuilder.Append(" MB"); stringBuilder.AppendLine();
            }
            else
            {
                stringBuilder.AppendLine("No audio sources.                 ");
            }
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Cloth -----------------------");
            if (clothNumber > 0)
            {
                stringBuilder.Append("Number of Cloth components:       ");
                stringBuilder.Append(clothNumber); stringBuilder.AppendLine();
                stringBuilder.Append("Total cloth verticies:            ");
                stringBuilder.Append(clothVertCount); stringBuilder.AppendLine();
                //   stringBuilder.Append("");
                //   stringBuilder.Append(clothDiff); stringBuilder.AppendLine();
            }
            else
            {
                stringBuilder.AppendLine("No Cloth components               ");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Dynamic Bone ----------------");
            if (dbScriptCount > 0) 
            {
                stringBuilder.Append("Dynamic bone script count:        ");
                stringBuilder.Append(dbScriptCount); stringBuilder.AppendLine();
                stringBuilder.Append("Dynamic bone particle count:      ");
                stringBuilder.Append(dbParticleCount); stringBuilder.AppendLine();
                stringBuilder.Append("Dynamic bone transform count:     ");
                stringBuilder.Append(dbTransformCount); stringBuilder.AppendLine();
                stringBuilder.Append("Dynamic bone collision count:     ");
                stringBuilder.Append(dbCollisionCount); stringBuilder.AppendLine();
            }
            else
            {
                stringBuilder.AppendLine("No Dynamic bone scripts.          ");
            }
            
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Magica ----------------------");
            if (HasMagica())
            {
                stringBuilder.Append("Magica BoneCloth/Spring count:    ");
                stringBuilder.Append(MagicaBoneClothCount); stringBuilder.Append(" / ");
                stringBuilder.Append(MagicaBoneSpringCount); stringBuilder.AppendLine();
                stringBuilder.Append("Magica MeshCloth/Spring count:    ");
                stringBuilder.Append(MagicaMeshClothCount); stringBuilder.Append(" / ");
                stringBuilder.Append(MagicaMeshSpringCount); stringBuilder.AppendLine();
                stringBuilder.Append("Sphere/Capsule/PlaneColliders:    ");
                stringBuilder.Append(MagicaSphereColliderCount); stringBuilder.Append(" / ");
                stringBuilder.Append(MagicaCapsuleColliderCount); stringBuilder.Append(" / ");
                stringBuilder.Append(MagicaPlaneColliderCount); stringBuilder.AppendLine();
                stringBuilder.Append("Render/VirtualDeformer count:     ");
                stringBuilder.Append(MagicaRenderDeformerCount); stringBuilder.Append(" / ");
                stringBuilder.Append(MagicaVirtualDeformerCount); stringBuilder.AppendLine();
            }
            else 
            {
                stringBuilder.AppendLine("No Magica components.");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- FinalIK ---------------------");
            stringBuilder.Append("VRIK Components:                  ");
            stringBuilder.Append(VRIKComponents); stringBuilder.AppendLine();
            stringBuilder.Append("TwistRelaxers:                    ");
            stringBuilder.Append(TwistRelaxers); stringBuilder.AppendLine();
            stringBuilder.Append("Other FinalIK Components:         ");
            stringBuilder.Append(OtherFinalIKComponents); stringBuilder.AppendLine();


            stringBuilder.AppendLine();
            stringBuilder.AppendLine("----------- Other -----------------------");
            stringBuilder.Append("Total shader pass count:          ");
            stringBuilder.Append(passCount); stringBuilder.AppendLine();
            stringBuilder.Append("Transform count:                  ");
            stringBuilder.Append(TransformCount); stringBuilder.AppendLine();
            stringBuilder.Append("RigidBody count:                  ");
            stringBuilder.Append(RigidBodyCount); stringBuilder.AppendLine();
            stringBuilder.Append("Physics collider count:           ");
            stringBuilder.Append(ColliderCount); stringBuilder.AppendLine();
            stringBuilder.Append("Physics joint count:              ");
            stringBuilder.Append(JointCount); stringBuilder.AppendLine();
            stringBuilder.Append("Constraint count:                 ");
            stringBuilder.Append(ConstraintCount); stringBuilder.AppendLine();
            stringBuilder.Append("Animators:                        ");
            stringBuilder.Append(Animators); stringBuilder.AppendLine();         
            stringBuilder.Append("Max Hiearchy Depth:               ");
            stringBuilder.Append(MaxHiearchyDepth); stringBuilder.AppendLine();
            stringBuilder.Append("Skinned bone count (unique):      ");
            stringBuilder.Append(skinnedBonesVRC); stringBuilder.AppendLine();
            stringBuilder.Append("Skinned bone count (total):       ");
            stringBuilder.Append(skinnedBones); stringBuilder.AppendLine();

            stringBuilder.Append("Analysis took ");
            stringBuilder.Append(stopwatch.Elapsed.TotalMilliseconds);
            stringBuilder.AppendLine(" ms.");

            //  stringBuilder.Append(AvatarInfoString);

            var text = stringBuilder.ToString();
            stringBuilder.Clear();
            return text;
        }

    }
    public class MaterialInfo
    {
        public Material material { get; set; }
        public string name { get; set; }
        public string shaderName { get; set; }
        public uint shaderPassCount { get; set; }
        public uint renderQueue { get; set; }
        public override bool Equals(object obj)
        {
            if (obj.GetType().Equals(typeof(MaterialInfo)) && material != null && ((MaterialInfo)obj).material != null)
            {
                return material.Equals(((MaterialInfo)obj).material);
            }
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return material.GetHashCode();
        }
        public override string ToString()
        {
            return name + ": shaderName: " + shaderName + ", shaderPassCount: " + shaderPassCount + ", renderQueue: " + renderQueue;
        }
    }

    public class AvatarInfoCalc
    {
       
        public static AvatarInfoCalc Instance { get { return instance; } }
        internal static readonly AvatarInfoCalc instance = new AvatarInfoCalc();

        private const float FourThirds = (4f / 3f);
        private static bool MaterialRecursion = false;
        private readonly HashSet<string> AllAdditionalShaderKeywords = new HashSet<string>();
        public bool ShouldLog = true;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private static readonly long nanosecPerTick = 1_000_000_000L / System.Diagnostics.Stopwatch.Frequency;
        private static readonly List<int> outNames = new List<int>();
        private static readonly Dictionary<int, string> TexturePropertyNameIDs = new Dictionary<int, string>();
        private static readonly HashSet<int> tempIntMap = new HashSet<int>();

        private readonly List<Animator> animators = new List<Animator>();
        private readonly List<AudioSource> audioSources = new List<AudioSource>();
        private readonly List<AudioStatData> audioStatData = new List<AudioStatData>();
        private readonly List<Cloth> cloths = new List<Cloth>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly List<IConstraint> constraints = new List<IConstraint>();
        private readonly List<Joint> joints = new List<Joint>();
        private readonly List<Light> lights = new List<Light>();
        private readonly List<Rigidbody> rigidbodies = new List<Rigidbody>();
        private readonly List<Transform> tempTransforms = new List<Transform>();
        private readonly List<TextureStatData> textureStatData = new List<TextureStatData>();
        private readonly List<Transform> transforms = new List<Transform>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<CanvasRenderer> canvasRenderers = new List<CanvasRenderer>();

        //Dynamic Bone
        private readonly List<DynamicBone> dynamicBones = new List<DynamicBone>();

        //Magica
        private readonly List<MagicaCloth.MagicaBoneCloth>       MagicaBoneCloth        = new List<MagicaCloth.MagicaBoneCloth>();
        private readonly List<MagicaCloth.MagicaBoneSpring>      MagicaBoneSpring       = new List<MagicaCloth.MagicaBoneSpring>();
        private readonly List<MagicaCloth.MagicaMeshCloth>       MagicaMeshCloth        = new List<MagicaCloth.MagicaMeshCloth>();
        private readonly List<MagicaCloth.MagicaMeshSpring>      MagicaMeshSpring       = new List<MagicaCloth.MagicaMeshSpring>();
        private readonly List<MagicaCloth.MagicaSphereCollider>  MagicaSphereCollider   = new List<MagicaCloth.MagicaSphereCollider>();
        private readonly List<MagicaCloth.MagicaCapsuleCollider> MagicaCapsuleCollider  = new List<MagicaCloth.MagicaCapsuleCollider>();
        private readonly List<MagicaCloth.MagicaPlaneCollider>   MagicaPlaneCollider    = new List<MagicaCloth.MagicaPlaneCollider>();                                                                                
        private readonly List<MagicaCloth.MagicaRenderDeformer>  MagicaRenderDeformer   = new List<MagicaCloth.MagicaRenderDeformer>();
        private readonly List<MagicaCloth.MagicaVirtualDeformer> MagicaVirtualDeformer  = new List<MagicaCloth.MagicaVirtualDeformer>();

        // Final IK 
        private readonly List<RootMotion.FinalIK.VRIK> VRIKs = new List<RootMotion.FinalIK.VRIK>();
        private readonly List<RootMotion.FinalIK.IK> IKs = new List<RootMotion.FinalIK.IK>();

        private readonly List<RootMotion.FinalIK.ShoulderRotator> ShoulderRotators = new List<RootMotion.FinalIK.ShoulderRotator>();
        private readonly List<RootMotion.FinalIK.FBBIKArmBending> armBends = new List<RootMotion.FinalIK.FBBIKArmBending>();
        private readonly List<RootMotion.FinalIK.Grounder> grounders = new List<RootMotion.FinalIK.Grounder>();
        private readonly List<RootMotion.FinalIK.RotationLimit> RotationLimits = new List<RootMotion.FinalIK.RotationLimit>();
        private readonly List<RootMotion.FinalIK.IKExecutionOrder> ExecOrders = new List<RootMotion.FinalIK.IKExecutionOrder>();
        private readonly List<RootMotion.FinalIK.FBBIKHeadEffector> HeadEffectors = new List<RootMotion.FinalIK.FBBIKHeadEffector>();
        private readonly List<RootMotion.FinalIK.TwistRelaxer> twistRelaxers = new List<RootMotion.FinalIK.TwistRelaxer>();

        private readonly List<Material> materialsCache = new List<Material>();
        private readonly List<string> allTexturePropertyNames = new List<string>();
        private static readonly HashSet<string> shaderKeywords = new HashSet<string>();
        private readonly List<int> leafDepth = new List<int>();
        private readonly StringBuilder commonSb = new StringBuilder(1024*1024);
        private readonly HashSet<AudioClip> audioClips = new HashSet<AudioClip>();
        private readonly HashSet<Texture> textures = new HashSet<Texture>();
        private readonly Dictionary<Texture, Dictionary<string, List<string>>> texturesMaterials = new Dictionary<Texture, Dictionary<string, List<string>>>();
        private readonly HashSet<DynamicBoneColliderBase> dbColliders = new HashSet<DynamicBoneColliderBase>();

        private readonly HashSet<Mesh> meshes = new HashSet<Mesh>();
        private readonly Dictionary<Mesh, MeshStatData> meshStatDataDict = new Dictionary<Mesh, MeshStatData>();

        private readonly List<Matrix4x4> bindPoses = new List<Matrix4x4>();
        private readonly Dictionary<VertexAttributeFormat, byte> VertexAttributeFormatSize = new Dictionary<VertexAttributeFormat, byte>
        {
            { VertexAttributeFormat.SInt32, 4 },
            { VertexAttributeFormat.UInt32, 4 },
            { VertexAttributeFormat.Float32, 4 },
            { VertexAttributeFormat.Float16, 2 },
            { VertexAttributeFormat.SNorm16, 2 },
            { VertexAttributeFormat.UNorm16, 2 },
            { VertexAttributeFormat.SInt16, 2 },
            { VertexAttributeFormat.UInt16, 2 },
            { VertexAttributeFormat.SNorm8, 1 },
            { VertexAttributeFormat.UNorm8, 1 },
            { VertexAttributeFormat.UInt8, 1 },
            { VertexAttributeFormat.SInt8, 1 },
        }; 
        private static readonly char[] intNameChars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        static readonly string[] audioClipLoadTypes = new string[] { " Decompress on load ", "Compressed in memory", "     Streaming      " };
       
        public struct MeshStatData
        {
            public uint vertexCount;
            public uint blendShapeCount;
            public uint triangleCount;
            public uint bindPoseCount;
            public uint vertexAttributeCount;
            public ulong VramMeasured;
            public string name;

            public bool IsReadable { get; internal set; }
            public ulong VramCalculated { get; internal set; }

            public void ToStringBuilder(StringBuilder sbOut) 
            {
                sbOut.Append("Mesh name: '");
                sbOut.Append(name);
                sbOut.Append("', VramMeasured: ");
                GetBytesReadable(VramMeasured, sbOut);
                sbOut.Append(", vertexCount: ");
                Uint5digitToStringBuilder(vertexCount, sbOut);
                sbOut.Append(", blendShapeCount: ");
                Uint5digitToStringBuilder(blendShapeCount, sbOut);
                sbOut.Append(", triangleCount: ");
                Uint5digitToStringBuilder(triangleCount, sbOut);
                sbOut.Append(", bindPoseCount: ");
                Uint5digitToStringBuilder(bindPoseCount, sbOut);
                sbOut.Append(", vertexAttributeCount: ");
                Uint5digitToStringBuilder(vertexAttributeCount, sbOut);
                sbOut.AppendLine();
            }
        }
        public struct AudioStatData
        {
            public string clipName;
            public uint size;
            public bool loadInBackground;
            public AudioClipLoadType loadType;
            public float length;
            public int channels;
            public int frequency;
        }

        private struct TextureStatData
        {
            public int width;
            public int height;
            public TextureFormat format;
            public Type type;
            public long profiler_mem;
            public long _calc_mem;
            public bool isReadable;
            public bool isMipmapped;
            internal bool isStreaming;
            internal bool isSrgb;
        }
        
        public static readonly List<string> defaultKeywords = new List<string>(new string[]
        { 
        // Unity standard shaders
"_ALPHABLEND_ON",
"_ALPHAMODULATE_ON",
"_ALPHAPREMULTIPLY_ON",
"_ALPHATEST_ON",
"_COLORADDSUBDIFF_ON",
"_COLORCOLOR_ON",
"_COLOROVERLAY_ON",
"_DETAIL_MULX2",
"_EMISSION",
"_FADING_ON",
"_GLOSSYREFLECTIONS_OFF",
"_GLOSSYREFLECTIONS_OFF",
"_MAPPING_6_FRAMES_LAYOUT",
"_METALLICGLOSSMAP",
"_NORMALMAP",
"_PARALLAXMAP",
"_REQUIRE_UV2",
"_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
"_SPECGLOSSMAP",
"_SPECULARHIGHLIGHTS_OFF",
"_SPECULARHIGHLIGHTS_OFF",
"_SUNDISK_HIGH_QUALITY",
"_SUNDISK_NONE",
"_SUNDISK_SIMPLE",
"_TERRAIN_NORMAL_MAP",
"BILLBOARD_FACE_CAMERA_POS",
"EFFECT_BUMP",
"EFFECT_HUE_VARIATION",
"ETC1_EXTERNAL_ALPHA",
"GEOM_TYPE_BRANCH",
"GEOM_TYPE_BRANCH_DETAIL",
"GEOM_TYPE_FROND",
"GEOM_TYPE_LEAF",
"GEOM_TYPE_MESH",
"LOD_FADE_CROSSFADE",
"PIXELSNAP_ON",
"SOFTPARTICLES_ON",
"STEREO_INSTANCING_ON",
"STEREO_MULTIVIEW_ON",
"UNITY_HDR_ON",
"UNITY_SINGLE_PASS_STEREO",
"UNITY_UI_ALPHACLIP",
"UNITY_UI_CLIP_RECT",
        // Post Processing
"ANTI_FLICKER",
"APPLY_FORWARD_FOG",
"AUTO_EXPOSURE",
"AUTO_KEY_VALUE",
"BLOOM",
"BLOOM_LENS_DIRT",
"BLOOM_LOW",
"CHROMATIC_ABERRATION",
"CHROMATIC_ABERRATION_LOW",
"COLOR_GRADING",
"COLOR_GRADING_HDR",
"COLOR_GRADING_HDR_3D",
"COLOR_GRADING_LOG_VIEW",
"DEPTH_OF_FIELD",
"DEPTH_OF_FIELD_COC_VIEW",
"DISTORT",
"DITHERING",
"FINALPASS",
"FOG_EXP",
"FOG_EXP2",
"FOG_LINEAR",
"FOG_OFF",
"FXAA",
"FXAA_KEEP_ALPHA",
"FXAA_LOW",
"GRAIN",
"SOURCE_GBUFFER",
"STEREO_DOUBLEWIDE_TARGET",
"STEREO_INSTANCING_ENABLED",
"TONEMAPPING_ACES",
"TONEMAPPING_CUSTOM",
"TONEMAPPING_FILMIC",
"TONEMAPPING_NEUTRAL",
"UNITY_COLORSPACE_GAMMA",
"USER_LUT",
"VIGNETTE",
"VIGNETTE_CLASSIC",
"VIGNETTE_MASKED"
        });

        public static void GetBytesReadable(ulong i, StringBuilder stringBuilder)
        {
            if (i <= 0)
            {
                stringBuilder.Append("0 B");
                return;
            }
            if (i < 1024)
            {
                Uint5digitToStringBuilder((uint)i, stringBuilder);
                stringBuilder.Append(" B");
                return;
            }
            string suffix;
            if (i >= 0x40000000)
            {
                suffix = " GB";
                i >>= 20;
            }
            else if (i >= 0x100000)
            {
                suffix = " MB";
                i >>= 10;
            }
            else
            {
                suffix = " kB";
            }
            // 123 456
            ulong t = (i & 1023) * 1000 / 1024;
            i >>= 10;
            Uint5digitToStringBuilder((uint)i, stringBuilder);
            stringBuilder.Append('.');
            if (i >= 1000)
            {
                t += 500;
                t /= 1000;
            }
            else if (i >= 100)
            {
                t += 50;
                t /= 100;
            }
            else if (i >= 10)
            {
                t += 5;
                t /= 10;
            }
            Uint5digitToStringBuilder((uint)t, stringBuilder);
            stringBuilder.Append(suffix);
        }
        private static void Uint5digitToStringBuilder(uint number, StringBuilder sb, bool padLeftFive = false, bool padRightFive = false)
        {
            if (number > 99999)
            {
                UintToStringBuilder(number, sb);
                return;
            }
            if (padLeftFive)
                sb.Append(' ');
            if (number == 0)
            {
                if (padLeftFive)
                    sb.Append("   0");
                else if (padRightFive)
                    sb.Append("0    ");
                else
                    sb.Append('0');
                return;
            }
            var tenthousands = number / 10000;
            var decrement = tenthousands * 10000;
            var thousands = (number - decrement) / 1000;
            decrement += thousands * 1000;
            var hundresds = (number - decrement) / 100;
            decrement += hundresds * 100;
            var tens = (number - decrement) / 10;
            decrement += tens * 10;
            var ones = number - decrement;
            int significantDigits = 1;
            bool isSignificant = tenthousands > 0;
            significantDigits += isSignificant ? 1 : 0;
            AppendDigit(tenthousands, sb, isSignificant, padLeftFive);
            isSignificant |= thousands > 0;
            significantDigits += isSignificant ? 1 : 0;
            AppendDigit(thousands, sb, isSignificant, padLeftFive);
            isSignificant |= hundresds > 0;
            significantDigits += isSignificant ? 1 : 0;
            AppendDigit(hundresds, sb, isSignificant, padLeftFive);
            isSignificant |= tens > 0;
            significantDigits += isSignificant ? 1 : 0;
            AppendDigit(tens, sb, isSignificant, padLeftFive);
            AppendDigit(ones, sb, true, padLeftFive);
            if (!padRightFive)
                return;
            for (int i = 0; i < 5 - significantDigits; i++)
                sb.Append(' ');
        }
        private static void AppendDigit(uint digit, StringBuilder sb, bool significant, bool padLeftFive)
        {
            if (significant)
                sb.Append(intNameChars[digit]);
            else if (padLeftFive)
                sb.Append(' ');
        }
        private static readonly ulong[] PowersOf10 = new ulong[]
        {
            1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000,
            1000000000, 10000000000, 100000000000, 1000000000000, 10000000000000,
            100000000000000, 1000000000000000, 10000000000000000, 100000000000000000, 
            1000000000000000000, 10000000000000000000
        };
        private static void UintToStringBuilder(ulong i, StringBuilder sb)
        {
            ulong decrease = 0;
            bool firstSignificant = false;
            for (int j = 19; j >= 0; j--)
            {
                ulong value = PowersOf10[j];
                var digit = (i - decrease) / value; 
                if (!firstSignificant && digit == 0)
                    continue;
                decrease += digit * value;
                firstSignificant = true;
                sb.Append(intNameChars[digit]);
            }
        }
        public void CheckCloth(GameObject ava, AvatarInfo _AvatarInfo)
        {
            _AvatarInfo.clothNumber = 0;
            _AvatarInfo.clothDiff = 0;
            _AvatarInfo.clothVertCount = 0;
            ava.GetComponentsInChildren(true, cloths);
            foreach (Cloth cloth in cloths)
            {
                int clothVertCount = cloth.vertices.Length;
                _AvatarInfo.clothVertCount += clothVertCount;
                _AvatarInfo.clothDiff += clothVertCount * cloth.clothSolverFrequency;
                _AvatarInfo.clothNumber++;
            }
            cloths.Clear();
        }
        public void CheckDB(GameObject ava, AvatarInfo _AvatarInfo)
        {
            CheckMagica(ava, _AvatarInfo);
            var dbJobsAvatarManager = ava.GetComponent<DbJobsAvatarManager>();

            if (dbJobsAvatarManager) 
            {
                _AvatarInfo.dbScriptCount = dbJobsAvatarManager.stats.scriptCount;
                _AvatarInfo.dbTransformCount = dbJobsAvatarManager.stats.transformCount;
                _AvatarInfo.dbParticleCount = dbJobsAvatarManager.stats.particleCount;
                _AvatarInfo.dbCollisionCount = dbJobsAvatarManager.stats.collisionCheckCount;
                return;
            }

            int dbCount = 0;
            int collCount = 0;
            int totalTransformCount = 0;
            ava.GetComponentsInChildren(true, dynamicBones);
            _AvatarInfo.dbScriptCount = dynamicBones.Count;
            foreach (DynamicBone db in dynamicBones)
            {
                if (!db)
                    continue;
                try
                {
                    dbColliders.Clear();
                        CountDb(ref dbCount, ref collCount, ref totalTransformCount, db);
                  
                }
                catch (Exception e)
                {
                 //   Debug.LogError(e);
                }
            }
            dynamicBones.Clear();
            dbColliders.Clear();
            _AvatarInfo.dbTransformCount = totalTransformCount;
            _AvatarInfo.dbParticleCount = dbCount;
            _AvatarInfo.dbCollisionCount = collCount;
        }
        private void CheckMagica(GameObject ava, AvatarInfo _AvatarInfo)
        {
            ava.GetComponentsInChildren(true, MagicaBoneCloth);
            ava.GetComponentsInChildren(true, MagicaBoneSpring);
            ava.GetComponentsInChildren(true, MagicaMeshCloth);
            ava.GetComponentsInChildren(true, MagicaMeshSpring);
            ava.GetComponentsInChildren(true, MagicaSphereCollider);
            ava.GetComponentsInChildren(true, MagicaCapsuleCollider);
            ava.GetComponentsInChildren(true, MagicaPlaneCollider);
            ava.GetComponentsInChildren(true, MagicaRenderDeformer);
            ava.GetComponentsInChildren(true, MagicaVirtualDeformer);

            _AvatarInfo.MagicaBoneClothCount = MagicaBoneCloth.Count;
            _AvatarInfo.MagicaBoneSpringCount = MagicaBoneSpring.Count;
            _AvatarInfo.MagicaMeshClothCount = MagicaMeshCloth.Count;
            _AvatarInfo.MagicaMeshSpringCount = MagicaMeshSpring.Count;
            _AvatarInfo.MagicaSphereColliderCount = MagicaSphereCollider.Count;
            _AvatarInfo.MagicaCapsuleColliderCount = MagicaCapsuleCollider.Count;
            _AvatarInfo.MagicaPlaneColliderCount = MagicaPlaneCollider.Count;
            _AvatarInfo.MagicaRenderDeformerCount = MagicaRenderDeformer.Count;
            _AvatarInfo.MagicaVirtualDeformerCount = MagicaVirtualDeformer.Count;

            MagicaBoneCloth.Clear();
            MagicaBoneSpring.Clear();
            MagicaMeshCloth.Clear();
            MagicaMeshSpring.Clear();
            MagicaSphereCollider.Clear();
            MagicaCapsuleCollider.Clear();
            MagicaPlaneCollider.Clear();
            MagicaRenderDeformer.Clear();
            MagicaVirtualDeformer.Clear();
        }
        private void CountDb(ref int dbCount, ref int collCount, ref int totalTransformCount, DynamicBone db)
        {
            int transformCount = db.transformCount;
            totalTransformCount += transformCount;
            var dbParticlesList = db.particlesArray;
            dbCount += (int)dbParticlesList?.Count;
            if (db.m_Colliders == null || db.m_Colliders.Count <= 0)
                return;
            dbColliders.UnionWith(db.m_Colliders);
            dbColliders.RemoveWhere(a => a == null);
            if (transformCount > 0) // Endbones apparently don't count as colliders?
                collCount += (Mathf.Max(transformCount - 1, 0) * dbColliders.Count); // Root doesn't count as collider either
        }
        public void CountObjects(GameObject gameObject, AvatarInfo avatarInfo)
        {
            gameObject.GetComponentsInChildren(true, transforms);
            gameObject.GetComponentsInChildren(true, joints);
            gameObject.GetComponentsInChildren(true, rigidbodies);
            gameObject.GetComponentsInChildren(true, constraints);
            gameObject.GetComponentsInChildren(true, animators);
            gameObject.GetComponentsInChildren(true, lights);
            gameObject.GetComponentsInChildren(true, colliders);

            // Final IK
            gameObject.GetComponentsInChildren(true, VRIKs);
            gameObject.GetComponentsInChildren(true, IKs);
            gameObject.GetComponentsInChildren(true, twistRelaxers);
            gameObject.GetComponentsInChildren(true, ShoulderRotators);
            gameObject.GetComponentsInChildren(true, armBends);
            gameObject.GetComponentsInChildren(true, grounders);
            gameObject.GetComponentsInChildren(true, RotationLimits);
            gameObject.GetComponentsInChildren(true, ExecOrders);
            gameObject.GetComponentsInChildren(true, HeadEffectors);

            avatarInfo.VRIKComponents = VRIKs.Count;
            avatarInfo.OtherFinalIKComponents = IKs.Count - avatarInfo.VRIKComponents + twistRelaxers.Count + ShoulderRotators.Count +
                armBends.Count + grounders.Count + RotationLimits.Count + ExecOrders.Count + HeadEffectors.Count;
            avatarInfo.TwistRelaxers = twistRelaxers.Count;

            avatarInfo.TransformCount = transforms.Count;
            avatarInfo.JointCount = joints.Count;
            avatarInfo.RigidBodyCount = rigidbodies.Count;
            avatarInfo.ConstraintCount = constraints.Count;
            avatarInfo.Animators = animators.Count;
            avatarInfo.Lights = lights.Count;
            avatarInfo.ColliderCount = colliders.Count;
            leafDepth.Clear();
            transforms.Clear();
            DFSGetChildren(gameObject.transform, 0);
            avatarInfo.MaxHiearchyDepth = GetMaxInList(leafDepth, out int index);
            avatarInfo.LongestPath = GetLeafPath(index);           
            commonSb.Clear();
            tempTransforms.Clear();
            leafDepth.Clear();
            transforms.Clear();
            joints.Clear();
            rigidbodies.Clear();
            constraints.Clear();
            animators.Clear();
            lights.Clear();
            colliders.Clear();

            // Final IK
            VRIKs.Clear();
            IKs.Clear();
            twistRelaxers.Clear();
        }
        private int GetMaxInList(IList<int> list, out int index)
        {
            int max = 0;
            index = 0;
            for (int i = 0, length = list.Count; i < length; i++)
            {
                if (max < list[i])
                {
                    max = list[i];
                    index = i;
                }
            }
            return max + 1;
        }
        public static string GetHashString(string inputString)
        {
            var crc = Crc32Algorithm.Compute(Encoding.ASCII.GetBytes(inputString));
            return crc.ToString("X8");
        }
        private static string SanitizeName(string name) 
        {
            if (IsAnyCharacterRightToLeft(name))
            {
                name = GetHashString(name);
            }
            return name;
        }
        static bool IsRandALCat(int c) 
        {
            if (c >= 0x5BE && c <= 0x10B7F)
            {
                if (c <= 0x85E)
                {
                    if (c == 0x5BE) return true;
                    else if (c == 0x5C0) return true;
                    else if (c == 0x5C3) return true;
                    else if (c == 0x5C6) return true;
                    else if (0x5D0 <= c && c <= 0x5EA) return true;
                    else if (0x5F0 <= c && c <= 0x5F4) return true;
                    else if (c == 0x608) return true;
                    else if (c == 0x60B) return true;
                    else if (c == 0x60D) return true;
                    else if (c == 0x61B) return true;
                    else if (0x61E <= c && c <= 0x64A) return true;
                    else if (0x66D <= c && c <= 0x66F) return true;
                    else if (0x671 <= c && c <= 0x6D5) return true;
                    else if (0x6E5 <= c && c <= 0x6E6) return true;
                    else if (0x6EE <= c && c <= 0x6EF) return true;
                    else if (0x6FA <= c && c <= 0x70D) return true;
                    else if (c == 0x710) return true;
                    else if (0x712 <= c && c <= 0x72F) return true;
                    else if (0x74D <= c && c <= 0x7A5) return true;
                    else if (c == 0x7B1) return true;
                    else if (0x7C0 <= c && c <= 0x7EA) return true;
                    else if (0x7F4 <= c && c <= 0x7F5) return true;
                    else if (c == 0x7FA) return true;
                    else if (0x800 <= c && c <= 0x815) return true;
                    else if (c == 0x81A) return true;
                    else if (c == 0x824) return true;
                    else if (c == 0x828) return true;
                    else if (0x830 <= c && c <= 0x83E) return true;
                    else if (0x840 <= c && c <= 0x858) return true;
                    else if (c == 0x85E) return true;
                }
                else if (c == 0x200F) return true;
                else if (c >= 0xFB1D)
                {
                    if (c == 0xFB1D) return true;
                    else if (0xFB1F <= c && c <= 0xFB28) return true;
                    else if (0xFB2A <= c && c <= 0xFB36) return true;
                    else if (0xFB38 <= c && c <= 0xFB3C) return true;
                    else if (c == 0xFB3E) return true;
                    else if (0xFB40 <= c && c <= 0xFB41) return true;
                    else if (0xFB43 <= c && c <= 0xFB44) return true;
                    else if (0xFB46 <= c && c <= 0xFBC1) return true;
                    else if (0xFBD3 <= c && c <= 0xFD3D) return true;
                    else if (0xFD50 <= c && c <= 0xFD8F) return true;
                    else if (0xFD92 <= c && c <= 0xFDC7) return true;
                    else if (0xFDF0 <= c && c <= 0xFDFC) return true;
                    else if (0xFE70 <= c && c <= 0xFE74) return true;
                    else if (0xFE76 <= c && c <= 0xFEFC) return true;
                    else if (0x10800 <= c && c <= 0x10805) return true;
                    else if (c == 0x10808) return true;
                    else if (0x1080A <= c && c <= 0x10835) return true;
                    else if (0x10837 <= c && c <= 0x10838) return true;
                    else if (c == 0x1083C) return true;
                    else if (0x1083F <= c && c <= 0x10855) return true;
                    else if (0x10857 <= c && c <= 0x1085F) return true;
                    else if (0x10900 <= c && c <= 0x1091B) return true;
                    else if (0x10920 <= c && c <= 0x10939) return true;
                    else if (c == 0x1093F) return true;
                    else if (c == 0x10A00) return true;
                    else if (0x10A10 <= c && c <= 0x10A13) return true;
                    else if (0x10A15 <= c && c <= 0x10A17) return true;
                    else if (0x10A19 <= c && c <= 0x10A33) return true;
                    else if (0x10A40 <= c && c <= 0x10A47) return true;
                    else if (0x10A50 <= c && c <= 0x10A58) return true;
                    else if (0x10A60 <= c && c <= 0x10A7F) return true;
                    else if (0x10B00 <= c && c <= 0x10B35) return true;
                    else if (0x10B40 <= c && c <= 0x10B55) return true;
                    else if (0x10B58 <= c && c <= 0x10B72) return true;
                    else if (0x10B78 <= c && c <= 0x10B7F) return true;
                }
                else if (c > 0x2000 && c < 0x2200) return true;
            }
            return false;
        }
        static bool IsAnyCharacterRightToLeft(string s)
        {
            for (var i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
            {
                var codepoint = char.ConvertToUtf32(s, i);
                if (IsRandALCat(codepoint))
                    return true;
            }
            return false;
        }
        private string GetLeafPath(int index)
        {
            if (transforms.Count == 0 || transforms.Count < index)
            {
                return "";
            }
            transforms[index].GetComponentsInParent(true, tempTransforms);
            if (tempTransforms.Count == 0)
            {
                return "";
            }
            commonSb.Clear();
            commonSb.Append(SanitizeName(tempTransforms[tempTransforms.Count - 1].name));
            for (int i = tempTransforms.Count - 2; i >= 0; i--)
            {
                commonSb.Append("\\");
                commonSb.Append(SanitizeName(tempTransforms[i].name));
            }
            return commonSb.ToString();
        }
        private void DFSGetChildren(Transform transform, int level)
        {
            var childCount = transform.childCount;
            if (childCount == 0 && level == 0)
            {
                leafDepth.Add(0);
                transforms.Add(transform);
                return;
            }
            level++;
            for (var index = 0; index < childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child.childCount > 0)
                {
                    DFSGetChildren(child, level);
                }
                else
                {
                    leafDepth.Add(level);
                    transforms.Add(child);
                }
            }
        }
        public void CheckRenderers(GameObject ava, AvatarInfo _AvatarInfo)
        {
            _AvatarInfo.FaceCount = 0;
            _AvatarInfo.lineTrailRenderers = 0;
            _AvatarInfo.skinnedMeshRenderers = 0;
            _AvatarInfo.meshRenderers = 0;
            _AvatarInfo.materialCount = 0;
            _AvatarInfo.skinnedBones = 0;
            _AvatarInfo.skinnedBonesVRC = 0;
            _AvatarInfo.particleSystems = 0;
            _AvatarInfo.otherRenderers = 0;
            _AvatarInfo.maxParticles = 0;
            _AvatarInfo.lineTrailRendererTriCount = 0;
            transforms.Clear();
            renderers.Clear();
            ava.GetComponentsInChildren(true, renderers);
            meshes.Clear();
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                _AvatarInfo.materialCount += renderer.sharedMaterials.Length;
                switch (renderer)
                {
                    case SkinnedMeshRenderer skinnedMeshRenderer:
                        {
                            var mesh = skinnedMeshRenderer.sharedMesh;
                            meshes.Add(mesh);
                            _AvatarInfo.FaceCount += CountMesh(mesh, out int _);
                            transforms.AddRange(skinnedMeshRenderer.bones);
                            _AvatarInfo.skinnedMeshRenderers++;
                            break;
                        }

                    case MeshRenderer meshRenderer:
                        _AvatarInfo.FaceCount += CountMeshRendererTris(meshRenderer);
                        _AvatarInfo.meshRenderers++;
                        break;
                    case TrailRenderer trailRenderer:
                        _AvatarInfo.lineTrailRendererTriCount += CountTrailRendererTris(trailRenderer);
                        _AvatarInfo.lineTrailRenderers++;
                        break;
                    case LineRenderer lineRenderer:
                        _AvatarInfo.lineTrailRendererTriCount += CountLineRendererTris(lineRenderer);
                        _AvatarInfo.lineTrailRenderers++;
                        break;
                    case ParticleSystemRenderer particleSystemRenderer:
                        {
                            _AvatarInfo.particleSystems++;
                            if (particleSystemRenderer)
                            {
                                var mesh = particleSystemRenderer.mesh;
                                meshes.Add(mesh);
                                _AvatarInfo.FaceCount += CountMesh(mesh, out int _);
                            }
                            var particleSystem = renderer.GetComponent<ParticleSystem>();
                            if (particleSystem)
                                _AvatarInfo.maxParticles += particleSystem.main.maxParticles;
                            break;
                        }

                    //case BillboardRenderer _:
                    //case SpriteRenderer _:
                    //case UnityEngine.Tilemaps.TilemapRenderer _:
                    default:
                        _AvatarInfo.otherRenderers++;
                        break;
                }
            }
            meshStatDataDict.Clear();
            ulong meshVram = 0;
            ulong vramProfiler = 0;
            ulong vramBlendshapes = 0;
            foreach (var mesh in meshes)
            {
                meshVram += CountMeshVram(mesh, out ulong _vramProfiler, out ulong _vramBlendshapes);
                vramProfiler += _vramProfiler;
                vramBlendshapes += _vramBlendshapes;
            }
       //     _AvatarInfo.VramBlendshapes = vramBlendshapes;
            _AvatarInfo.VRAM_MeshesProfiler = vramProfiler;
      //      _AvatarInfo.VRAM_Meshes = meshVram;

            //LogMeshVram(_AvatarInfo);

            canvasRenderers.Clear();
            ava.GetComponentsInChildren(true, canvasRenderers);
            foreach (var item in canvasRenderers)
            {
                _AvatarInfo.otherRenderers++;
                _AvatarInfo.materialCount += item.materialCount;
            }
            canvasRenderers.Clear();

            _AvatarInfo.skinnedBones = transforms.Count;
            _AvatarInfo.skinnedBonesVRC = CountDistinct(transforms);
            transforms.Clear();
        }
        private unsafe int CountDistinct(List<Transform> collection)
        {
            if (collection == null || collection.Count < 2)
            {
                return collection == null ? 0 : collection.Count;
            }
            tempIntMap.Clear();
            var listCount = collection.Count;
            for (int i = 0; i < listCount; i++)
                tempIntMap.Add(collection[i]?.GetInstanceID() ?? 0);
            return tempIntMap.Count();
        }
        private ulong CountMeshVram(Mesh mesh, out ulong vramProfiler, out ulong vramBlendshapes)
        {
            if (!mesh)
            {
                vramBlendshapes = 0;
                vramProfiler = 0;
                return 0;
            }

            ulong vram = 0;
            int vertexCount = mesh.vertexCount;
            int bytesPerVert = 0;
            int vertexAttributeCount = mesh.vertexAttributeCount;
            for (int i = 0; i < vertexAttributeCount; i++)
            {
                var attribs = mesh.GetVertexAttribute(i);
                var dim = attribs.dimension;
                var size = VertexAttributeFormatSize[attribs.format];
                bytesPerVert += size * dim;
            }
            vram += (ulong)(vertexCount * bytesPerVert);
            var blendShapeCount = mesh.blendShapeCount;

            mesh.GetBindposes(bindPoses);
            int bindPoseCount = bindPoses.Count;
            vram += (ulong)(bindPoseCount * 64);
            bindPoses.Clear();
            var allBones = mesh.GetAllBoneWeights();
            vram += (ulong)(allBones.Length * 8);
            var triCount = CountMesh(mesh, out int indiciesCount);
            vram += (ulong)(indiciesCount * 4);
            vramProfiler = (ulong)Profiler.GetRuntimeMemorySizeLong(mesh);
            if (mesh.isReadable)
                vramProfiler /= 2;
            vramBlendshapes = 0;
            meshStatDataDict[mesh] = new MeshStatData
            {
                bindPoseCount = (uint)bindPoseCount,
                blendShapeCount = (uint)blendShapeCount,
                name = SanitizeName(mesh.name),
                triangleCount = (uint)triCount,
                vertexAttributeCount = (uint)vertexAttributeCount,
                vertexCount = (uint)vertexCount,
                VramCalculated = vram,
                VramMeasured = vramProfiler,
                IsReadable = mesh.isReadable
            };
            return vram;
        }
        private int CountMesh(Mesh mesh, out int indiciesCount)
        {
            indiciesCount = 0;
            if (!mesh)
            {
                return 0;
            }
            int faceCounter = 0;
            for (int i = 0, length = mesh.subMeshCount; i < length; i++)
            {
                MeshTopology topology;
                SubMeshDescriptor submesh = mesh.GetSubMesh(i);
                topology = submesh.topology;

                switch (topology)
                {
                    case MeshTopology.Lines:
                    case MeshTopology.LineStrip:
                    case MeshTopology.Points:
                        //wtf
                        { continue; }
                    case MeshTopology.Quads:
                        {
                            indiciesCount += submesh.indexCount;
                            faceCounter += (submesh.indexCount / 4);
                            continue;
                        }
                    case MeshTopology.Triangles:
                        {
                            indiciesCount += submesh.indexCount;
                            faceCounter += (submesh.indexCount / 3);
                            continue;
                        }
                }
            }
            return faceCounter;
        }
        private int CountMeshRendererTris(MeshRenderer renderer)
        {
            MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
            if (meshFilter)
            {
                var mesh = meshFilter.sharedMesh;
                meshes.Add(mesh);
                return CountMesh(mesh, out int _);
            }
            return 0;
        }
        private ulong CountTrailRendererTris(TrailRenderer renderer)
        {
            if (renderer)
            {
                return (ulong)(renderer.time * 100);
            }
            return 0;
        }
        private ulong CountLineRendererTris(LineRenderer renderer)
        {
            if (renderer)
                return (ulong)(renderer.positionCount * 2); // idk..
            return 0;
        }
        public void CheckAudio(GameObject ava, AvatarInfo _AvatarInfo)
        {
            audioClips.Clear();
            ava.GetComponentsInChildren(true, audioSources);
            _AvatarInfo.AudioSources = audioSources.Count;
            for (int i = 0; i < audioSources.Count; i++)
            {
                AudioSource audioSource = audioSources[i];
                if (audioSource != null && audioSource.clip != null)
                    audioClips.Add(audioSource.clip);
            }
            long _totalSize = 0;
            if (ShouldLog) 
                commonSb.Clear();
            float length = 0;
            audioStatData.Clear();
            _AvatarInfo.AudioClipCount = audioClips.Count;
            foreach (AudioClip audioClip in audioClips)
            {
                uint _size = (uint)(audioClip.samples * audioClip.channels) * 2; // 16 bit samples = 2 Bytes
                _totalSize += _size;
                length += audioClip.length;
                if (ShouldLog) 
                    audioStatData.Add(new AudioStatData {
                    clipName = SanitizeName(audioClip.name),
                    size = _size,
                    length = audioClip.length,
                    channels = audioClip.channels,
                    frequency = audioClip.frequency,
                    loadInBackground = audioClip.loadInBackground,
                    loadType = audioClip.loadType
                });
            }
            _AvatarInfo.AudioClipLength = (int)length;
            _AvatarInfo.AudioClipSize = _totalSize;
            _AvatarInfo.AudioClipSizeMB = (Math.Round(_totalSize / 1024f / 1024f * 100f) / 100f);
            if (ShouldLog)
            {
                _AvatarInfo.AvatarInfoString += AddAudioInfoToLog(_AvatarInfo);
            }
            audioClips.Clear();
            audioSources.Clear();
        }
        private string AddAudioInfoToLog(AvatarInfo _AvatarInfo)
        {
            commonSb.Clear();
            commonSb.Append(_AvatarInfo.AudioClipCount);
            commonSb.Append(" audio clips");
            if (audioClips.Count > 0)
            {
                commonSb.Append(" with ");
                GetBytesReadable((ulong)_AvatarInfo.AudioClipSize, commonSb);
                commonSb.Append(" estimated size");
            }
            commonSb.AppendLine(".");
            var _audioStatData = audioStatData.OrderByDescending(a => a.size);
            foreach (var audioClip in _audioStatData)
            {
                commonSb.Append("Size: ");
                GetBytesReadable(audioClip.size, commonSb);
                //sb.Append(GetBytesReadable(audioClip.size).PadRight(8)); 
                commonSb.Append(", loadInBackground: ");
                commonSb.Append(audioClip.loadInBackground);
                commonSb.Append(", loadType: ");
                commonSb.Append(audioClipLoadTypes[(int)audioClip.loadType]);
                commonSb.Append(", length: ");
                Uint5digitToStringBuilder((uint)audioClip.length, commonSb);
                commonSb.Append(" sec");
                //sb.Append(audioClip.length.ToString("0.## sec", CultureInfo.InvariantCulture).PadLeft(10));
                commonSb.Append(", channels: ");
                Uint5digitToStringBuilder((uint)audioClip.channels, commonSb);
                // sb.Append(audioClip.channels);
                commonSb.Append(", frequency: ");
                Uint5digitToStringBuilder((uint)audioClip.frequency, commonSb);
                //sb.Append(audioClip.frequency);
                commonSb.Append(" name: ");
                commonSb.Append(audioClip.clipName);
                commonSb.AppendLine();
            }
            return commonSb.ToString();
        }
        public void CheckTextures(GameObject ava, AvatarInfo _AvatarInfo)
        {
            if (ShouldLog) 
            {
                stopwatch.Restart();
            }
            _AvatarInfo.passCount = 0;
            textures.Clear();
            texturesMaterials.Clear();
            shaderKeywords.Clear();
            ava.GetComponentsInChildren(true, renderers);
            foreach (var rend in renderers)
            {
                string rendererName = SanitizeName(rend.name);
                rend.GetSharedMaterials(materialsCache);
                foreach (var material in materialsCache)
                {
                    try
                    {
                        CheckMaterial(material, _AvatarInfo, rendererName);
                    }
                    catch (Exception e) {
                        Debug.LogError(e);
                    }
                }
            }
            renderers.Clear();
            _AvatarInfo.additionalShaderKeywords.AddRange(shaderKeywords.Except(defaultKeywords));
            AllAdditionalShaderKeywords.UnionWith(_AvatarInfo.additionalShaderKeywords);
            _AvatarInfo.additionalShaderKeywordCount = _AvatarInfo.additionalShaderKeywords.Count;
            _AvatarInfo.profiledVRamUse = 0;
            _AvatarInfo.calculatedVRamUse = 0;
            long profiler_mem = 0;
            ulong vram = 0;
            textureStatData.Clear();
            foreach (Texture texture in textures)
            {
                CheckTexture(texture, _AvatarInfo, ref profiler_mem, ref vram);
            }
            _AvatarInfo.profiledVRamUseMB = Math.Round(_AvatarInfo.profiledVRamUse / 10485.76f ) / 100f;
            _AvatarInfo.calculatedVRamUseMB = Math.Round(_AvatarInfo.calculatedVRamUse / 10485.76f) / 100f;
            _AvatarInfo.VRAM_Textures = vram;
            if (ShouldLog)
            {
                stopwatch.Stop();
                _AvatarInfo.AvatarInfoString += AddTextureInfoToLog(_AvatarInfo.profiledVRamUse, _AvatarInfo.calculatedVRamUse, vram, stopwatch.ElapsedTicks);
            }
            textures.Clear();
            shaderKeywords.Clear();
        }
        private void CheckMaterial(Material material, AvatarInfo _AvatarInfo, string rendererName) 
        {
            if (!material)
                return;

            _AvatarInfo.passCount += material.passCount;
            string _materialName = SanitizeName(material.name);
            _AvatarInfo.materialNames.Add(_materialName);
            var shader = material.shader;       
            var materialShaderName = SanitizeName(material.shader.name);
            _AvatarInfo.shaderNames.Add(materialShaderName);
            _AvatarInfo.materialInfo.Add(new MaterialInfo
            {
                name = _materialName,
                renderQueue = (uint)material.renderQueue,
                shaderName = materialShaderName,
                shaderPassCount = (uint)shader.passCount,
                material = material
            });
            shaderKeywords.UnionWith(material.shaderKeywords);
            var texturePropertyNames = MaterialRecursion ? new List<int>() : outNames;
            texturePropertyNames.Clear();
            material.GetTexturePropertyNameIDs(texturePropertyNames);
            if (ShouldLog)
            {
                LogMaterialDetails(material, texturePropertyNames);
            }
            var prevE = Application.GetStackTraceLogType(LogType.Error);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            bool prevLog = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                GatherTextures(material, _AvatarInfo, rendererName, _materialName, texturePropertyNames);
            }
            finally
            {
                Debug.unityLogger.logEnabled = prevLog;
                Application.SetStackTraceLogType(LogType.Error, prevE);
            }
        }
        private void GatherTextures(Material material, AvatarInfo _AvatarInfo, string rendererName, string _materialName, List<int> texturePropertyNames)
        {
            foreach (var textureID in texturePropertyNames)
            {
                var texture = material.GetTexture(textureID);
                if (texture == null)
                {
                    continue;
                }
                if (texture is CustomRenderTexture crt)
                {
                    var prevMaterialRecursion = MaterialRecursion;
                    MaterialRecursion = true;
                    CheckMaterial(crt.initializationMaterial, _AvatarInfo, rendererName + " CRT: " + SanitizeName(crt.name));
                    CheckMaterial(crt.material, _AvatarInfo, rendererName + " CRT: " + SanitizeName(crt.name));
                    MaterialRecursion = prevMaterialRecursion;
                }
                textures.Add(texture);
                if (!ShouldLog)
                    continue;
                string materialName = rendererName + "\\" + _materialName;
                if (!TexturePropertyNameIDs.TryGetValue(textureID, out string textureName))
                {
                    textureName = textureID.ToString();
                }
                if (texturesMaterials.ContainsKey(texture))
                {
                    if (!texturesMaterials[texture].ContainsKey(materialName))
                        texturesMaterials[texture].Add(materialName, new List<string>());
                    if (!texturesMaterials[texture][materialName].Contains(textureName))
                        texturesMaterials[texture][materialName].Add(textureName);
                }
                else
                {
                    texturesMaterials.Add(texture, new Dictionary<string, List<string>>());
                    texturesMaterials[texture].Add(materialName, new List<string>());
                    texturesMaterials[texture][materialName].Add(textureName);
                }
            }
        }
        private void LogMaterialDetails(Material material, List<int> texturePropertyNames)
        {
            bool hasMissing = false;
            for (int i = 0; i < texturePropertyNames.Count; i++)
                if (!TexturePropertyNameIDs.ContainsKey(texturePropertyNames[i]))
                    hasMissing = true;
            allTexturePropertyNames.Clear();
            if (hasMissing)
                material.GetTexturePropertyNames(allTexturePropertyNames);
            if (texturePropertyNames.Count == allTexturePropertyNames.Count)
                for (int i = 0; i < texturePropertyNames.Count; i++)
                    if (!TexturePropertyNameIDs.ContainsKey(texturePropertyNames[i]))
                        TexturePropertyNameIDs[texturePropertyNames[i]] = allTexturePropertyNames[i];
        }
        private void CheckTexture(Texture texture, AvatarInfo _AvatarInfo, ref long profilerMemory, ref ulong vram)
        {
            if (Profiler.supported)
            {
                profilerMemory = Profiler.GetRuntimeMemorySizeLong(texture);
                _AvatarInfo.profiledVRamUse += profilerMemory;
            }
            long calculatedMemory = 0;
            bool isReadable = texture.isReadable;
            if (isReadable)
                _AvatarInfo.readableTextures++;
            var dimension = texture.dimension;
            var type = texture.GetType();
            bool isMipmapped = texture.mipmapCount > 1;
            var textureFormat = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetTextureFormat(texture.graphicsFormat);           
            int width = texture.width;
            int height = texture.height;
            if (!isMipmapped && (width > 256 || height > 256))
                _AvatarInfo.nonMipmappedTextures++;
            bool isStreaming = false;
            bool isSrgb = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
            switch (dimension)
            {
                case TextureDimension.Tex2D:
                    {
                        switch (texture)
                        {
                            case Texture2D texture2D:
                                textureFormat = texture2D.format;
                                width = texture2D.width;
                                height = texture2D.height;
                                isStreaming = texture2D.streamingMipmaps;
                                _AvatarInfo.calculatedVRamUse += calculatedMemory = CalculateMaxMemUse(textureFormat, width, height, isMipmapped, isReadable);
                                if (textureFormat == TextureFormat.DXT1Crunched || textureFormat == TextureFormat.DXT5Crunched)
                                    _AvatarInfo.crunchedTextures++;
                                break;
                            case CustomRenderTexture crt:

                                width = crt.width;
                                height = crt.height;
                                isMipmapped = crt.useMipMap;
                                calculatedMemory = CalculateMaxRTMemUse(crt.format, width, height, isMipmapped, profilerMemory, crt.antiAliasing, crt.depth);
                                _AvatarInfo.calculatedVRamUse += calculatedMemory;
                                break;
                            case RenderTexture rt:
                                width = rt.width;
                                height = rt.height;
                                isMipmapped = rt.useMipMap;
                                calculatedMemory = CalculateMaxRTMemUse(rt.format, width, height, isMipmapped, profilerMemory, rt.antiAliasing, rt.depth);
                                _AvatarInfo.calculatedVRamUse += calculatedMemory;
                                break;

                            default:
                                break;
                        }
                        break;
                    }
                case TextureDimension.Cube:
                    {
                        if (!(texture is Cubemap cubemap))
                        {
                            break;
                        }
                        width = cubemap.width;
                        height = cubemap.height;
                        isStreaming = cubemap.streamingMipmaps;
                        calculatedMemory = (CalculateMaxMemUse(cubemap.format, width, height, isMipmapped, isReadable) * 6);
                        _AvatarInfo.calculatedVRamUse += calculatedMemory;
                        break;
                    }
                case TextureDimension.CubeArray:
                    {
                        var cubemapArray = (CubemapArray)texture; 
                        bool mipmapped = false;
                        width = cubemapArray.width;
                        height = cubemapArray.height;
                        calculatedMemory = (CalculateMaxMemUse(cubemapArray.format, width, height, mipmapped, isReadable) * 6) * cubemapArray.cubemapCount;
                        _AvatarInfo.calculatedVRamUse += calculatedMemory;
                        break;
                    }
                case TextureDimension.Tex2DArray:
                    {
                        var texture2DArray = (Texture2DArray)texture;
                        bool mipmapped = texture2DArray.mipmapCount > 0;
                        width = texture2DArray.width;
                        height = texture2DArray.height;
                        _AvatarInfo.calculatedVRamUse += calculatedMemory = CalculateMaxMemUse(texture2DArray.format, width, height, mipmapped, false) * texture2DArray.depth;
                        break;
                    }
                case TextureDimension.Tex3D:
                    {
                        var texture3D = (Texture3D)texture;
                        bool mipmapped  = texture3D.mipmapCount > 0;
                        width = texture3D.width;
                        height = texture3D.height;
                        _AvatarInfo.calculatedVRamUse += calculatedMemory = CalculateMaxMemUse(texture3D.format, width, height, mipmapped, isReadable) * texture3D.depth;
                        break;
                    }
                default:
                        break;
                   
            }
            if (ShouldLog)
            {
                textureStatData.Add(new TextureStatData
                {
                    type = type,
                    width = width,
                    height = height,
                    format = textureFormat,
                    profiler_mem = profilerMemory,
                    _calc_mem = calculatedMemory,
                    isReadable = isReadable,
                    isMipmapped = isMipmapped,
                    isStreaming = isStreaming,
                    isSrgb = isSrgb,
                });
            }
            vram += (ulong)calculatedMemory;           
        }
         private string AddTextureInfoToLog(long textureMem, long calc_mem, ulong vram, long ElapsedTicks) 
        {
            commonSb.Clear();
            commonSb.Append("Textures use ");
            GetBytesReadable(vram, commonSb);
            commonSb.Append(" VRAM. (");
            GetBytesReadable((ulong)textureMem, commonSb);
            commonSb.Append(" RAM + VRAM, calculated max: ");
            GetBytesReadable((ulong)calc_mem, commonSb);
            commonSb.AppendLine(")");
            commonSb.Append("Analysis took ");
            commonSb.Append((ElapsedTicks * nanosecPerTick / 1_000_000f).ToString(CultureInfo.InvariantCulture));
            commonSb.Append(" ms (");
            commonSb.Append(ElapsedTicks);
            commonSb.AppendLine(" ticks)");
            int readableTextures = 0;
            int nonMipmappedTextures = 0;
            int crunchedTextures = 0;
            var _textureStatData = textureStatData.OrderByDescending(a => a._calc_mem).ToList();
            for (int i = 0; i < _textureStatData.Count; i++)
            {
                var texture = _textureStatData[i];
                if (texture.isReadable)
                    readableTextures++;
                if (!texture.isMipmapped && (texture.width > 256 || texture.height > 256))
                    nonMipmappedTextures++;
                if (texture.format == TextureFormat.DXT1Crunched || texture.format == TextureFormat.DXT5Crunched)
                    crunchedTextures++;
            }
            if (readableTextures > 0)
            {
                commonSb.Append("Textures marked as readable: ");
                commonSb.Append(readableTextures);
                commonSb.AppendLine();
            }
            if (nonMipmappedTextures > 0)
            {
                commonSb.Append("Textures with no mipmapping enabled: ");
                commonSb.Append(nonMipmappedTextures);
                commonSb.AppendLine();
            }
            if (crunchedTextures > 0)
            {
                commonSb.Append("Textures with crunch compression enabled: ");
                commonSb.Append(crunchedTextures);
                commonSb.AppendLine();
            }
            return commonSb.ToString();
        }
        private long CalculateMaxRTMemUse(RenderTextureFormat format, int width, int height, bool mipmapped, long _default, int antiAlias, int depth)
        {
            long _calc_mem;
            switch (format)
            {
                //// 4 bit/pixel
                //    {
                //        _calc_mem = (long)(width * height / 2f * (mipmapped ? FourThird : 1f));
                //
                //        break;
                //    }
                case RenderTextureFormat.R8:  // 8 bit/pixel
                    {
                        _calc_mem = (width * height);
                        break;
                    }

                case RenderTextureFormat.ARGB4444: //2B/px
                case RenderTextureFormat.ARGB1555:          // 16 bit
                case RenderTextureFormat.R16:
                case RenderTextureFormat.RG16:
                case RenderTextureFormat.RHalf:
                case RenderTextureFormat.RGB565:
                    {
                        _calc_mem = (long)(width * height * 2f);
                        break;
                    }

                // 3B/px
                //   {
                //       _calc_mem = (long)(width * height * 3f);
                //       break;
                //   }
                case RenderTextureFormat.ARGB32: // 4B/px
                case RenderTextureFormat.BGRA32:
                case RenderTextureFormat.RGHalf:
                case RenderTextureFormat.RFloat:
                case RenderTextureFormat.RInt:              // 32 bit
                case RenderTextureFormat.RGB111110Float:    // 32 bit
                case RenderTextureFormat.RG32:              // 32 bit
                case RenderTextureFormat.Default:           // ARGB32
                case RenderTextureFormat.ARGB2101010:       // 32 bit
                    {
                        _calc_mem = (long)(width * height * 4f);
                        break;
                    }
                case RenderTextureFormat.ARGBHalf:
                case RenderTextureFormat.DefaultHDR:        // ARGBHalf
                case RenderTextureFormat.ARGB64:
                case RenderTextureFormat.RGBAUShort:
                case RenderTextureFormat.RGFloat:           // 8B/px
                case RenderTextureFormat.RGInt:             // 64 bit
                    {
                        _calc_mem = (long)(width * height * 8f);
                        break;
                    }
                case RenderTextureFormat.ARGBInt:
                case RenderTextureFormat.ARGBFloat: //16B/px
                    {
                        _calc_mem = (long)(width * height * 16f);
                        break;
                    }
                case RenderTextureFormat.Depth:
                    {
                        _calc_mem = 0; // Depth will be added later
                        break;
                    }
                case RenderTextureFormat.Shadowmap:         // ?
                case RenderTextureFormat.BGRA10101010_XR:   // 40 bit?
                case RenderTextureFormat.BGR101010_XR:      // 30 bit?
                default:
                    {
                        _calc_mem = _default;
                        break;
                    }
            }
            int _AA = (antiAlias == 1) ? 1 : antiAlias + 1;
            /*
             *  MSAA level  |   texture size multiplier 
                MSAA1	    |   x
                MSAA2	    |   3x
                MSAA4	    |   5x
                MSAA8	    |   9x

                    depth buffer:
                MSAA level  |   texture size increment per pixel (Format independent)
                ------------|   16b     24/32b
                MSAA1	    |   2B	    4B 
                MSAA2	    |   4B	    8B 
                MSAA4	    |   8B	    16B 
                MSAA8	    |   16B 	32B 

                eg. 100×100px R8G8B8A8_Unorm: 4 * 10000 B
                MSAA8: 40 000 * 9 = 360 000 (B)
                24b depth = 10 000 * 4 = 40 000 (B) 
                MSAA8 + 24b depth = 360 000 + (10 000 * 4 * 8 = 320 000) = 680 000 (B)
                MipMaps don't affect stencils.

             */
            _calc_mem = (long)(_calc_mem * _AA * (mipmapped ? FourThirds : 1f));
            switch (depth)
            {
                case 16:
                    {
                        _calc_mem += width * height * antiAlias * 2;
                        break;
                    }
                case 24:
                case 32:
                    {
                        _calc_mem += width * height * antiAlias * 4;
                        break;
                    }
            }
            return _calc_mem;
        }
        private long CalculateMaxMemUse(TextureFormat format, int width, int height, bool mipmapped, bool readWriteEnabled)
        {
            long _calc_mem = 0;
            switch (format)
            {
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB2:      // 2 bit/pixel
                    {
                        _calc_mem = (long)(width * height / 4f);

                        break;
                    }
                case TextureFormat.BC4:
                case TextureFormat.DXT1:
                case TextureFormat.DXT1Crunched:
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4Crunched:
                case TextureFormat.ETC_RGB4_3DS:      // obsolete but doesn't throw error in 2019.4

                case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA1:        // tested in editor to be the same as ETC2_RGB
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:       // 4 bit/pixel
                    {
                        _calc_mem = (long)(width * height / 2f);

                        break;
                    }
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.BC5:
                case TextureFormat.BC6H:
                case TextureFormat.BC7:
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ETC_RGBA8_3DS:       // obsolete but doesn't throw error in 2019.4
                case TextureFormat.ETC2_RGBA8Crunched:  // 8 bit/pixel
                    {
                        _calc_mem = width * height;
                        break;
                    }                
                case TextureFormat.ARGB4444: 
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGB565:
                case TextureFormat.RGBA4444:        // 2 Bytes/pixel
                    {
                        _calc_mem = (long)(width * height * 2f);
                        break;
                    }
                case TextureFormat.RGB24:
                case TextureFormat.ARGB32: 
                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                case TextureFormat.RG32:
                case TextureFormat.RGB9e5Float:
                case TextureFormat.RGHalf:
                case TextureFormat.RFloat:          // 4 Bytes/pixel
                    {
                        _calc_mem = (long)(width * height * 4f);
                        break;
                    }
                case TextureFormat.RGBAHalf:
                case TextureFormat.RGB48:
                case TextureFormat.RGBA64:
                case TextureFormat.RGFloat:         // 8 Bytes/pixel
                    {
                        _calc_mem = (long)(width * height * 8f);
                        break;
                    }

                case TextureFormat.RGBAFloat:       // 16 Bytes/pixel
                    {
                        _calc_mem = (long)(width * height * 16f);
                        break;
                    }

                // ASTC is using 128 bits to store n×n pixels
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_HDR_4x4:
#endif
                case TextureFormat.ASTC_RGBA_4x4:
                    {
                        _calc_mem = (long)(width * height / (4 * 4) * 16f);
                        break;
                    }
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGBA_5x5:
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_HDR_5x5:
#endif
                    {
                        _calc_mem = (long)(width * height / (5 * 5) * 16f);
                        break;
                    }
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGBA_6x6:
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_HDR_6x6:
#endif
                    {
                        _calc_mem = (long)(width * height / (6 * 6) * 16f);
                        break;
                    }
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGBA_8x8:
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_HDR_8x8:
#endif
                    {
                        _calc_mem = (long)(width * height / (8 * 8) * 16f);
                        break;
                    }
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGBA_10x10:
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_HDR_10x10:
#endif
                    {
                        _calc_mem = (long)(width * height / (10 * 10) * 16f);
                        break;
                    }
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_12x12:
#if UNITY_2019_1_OR_NEWER
                case TextureFormat.ASTC_HDR_12x12:
#endif
                    {
                        _calc_mem = (long)(width * height / (12 * 12) * 16f);
                        break;
                    }
                case TextureFormat.YUY2: 
                    // "Currently, this texture format is only useful for native code plugins
                    // as there is no support for texture importing or pixel access for this format.
                    // YUY2 is implemented for Direct3D 9, Direct3D 11, and Xbox One."
                default:
                    {
                        _calc_mem = 0;
                        break;
                    }
            }
            return (long)(_calc_mem * (readWriteEnabled ? 2L : 1L) * (mipmapped ? FourThirds : 1f));
        }
    }
}
