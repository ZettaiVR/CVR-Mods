using MelonLoader;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System.Collections.Concurrent;
using Unity.Collections;
using ABI.CCK.Components;

[assembly: MelonInfo(typeof(Zettai.CVRTexturePropertyParserPatches), "CVRTexturePropertyParserPatches", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]
namespace Zettai
{
    [HarmonyPatch(typeof(CVRTexturePropertyParser))]
    class CVRTexturePropertyParserPatches :MelonMod
    {
        private static readonly MelonPreferences_Category category = MelonPreferences.CreateCategory("Zettai");
        private static readonly MelonPreferences_Entry<bool> Pref = category.CreateEntry("CVRTexturePropertyParserPatches", true, "CVRTexturePropertyParserPatches");
        private static readonly MelonPreferences_Entry<bool> syncGetTextureColorPref = category.CreateEntry("syncGetTextureColor", true, "syncGetTextureColor");

        // --------------------------------------------------------------- supportsAsyncGPUReadback false --------------------------------------------------------------------------

        private static readonly ConcurrentDictionary<RenderTexture, Color32[]> RT_ColorArray = new ConcurrentDictionary<RenderTexture, Color32[]>();
        private static readonly ConcurrentDictionary<RenderTexture, Texture2D> RT_outputTexture2D = new ConcurrentDictionary<RenderTexture, Texture2D>();

        // --------------------------------------------------------------- supportsAsyncGPUReadback true ---------------------------------------------------------------------------

        private static readonly Dictionary<RenderTexture, NativeArray<Color32>> RT_ColorNativeArray = new Dictionary<RenderTexture, NativeArray<Color32>>();
        private static readonly Dictionary<RenderTexture, AsyncGPUReadbackRequest> readbackRequests = new Dictionary<RenderTexture, AsyncGPUReadbackRequest>();
        private static readonly Dictionary<RenderTexture, HashSet<CVRTexturePropertyParser>> users = new Dictionary<RenderTexture, HashSet<CVRTexturePropertyParser>>();
        private static readonly HashSet<RenderTexture> startedUpdate = new HashSet<RenderTexture>();

        private static bool syncGetTextureColor;
        private static bool enableMod;
        public static void ClearStarted()
        {
            foreach (var item in readbackRequests)
            {
                if (!item.Value.done)
                    item.Value.WaitForCompletion();
            }
            startedUpdate.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CVRTexturePropertyParser.Start))]
        private static void Start(CVRTexturePropertyParser __instance)
        {
            var rt = __instance.texture;
            if (!rt)
                return;
            GetCachedColorArray(rt);
            var usersSet = GetUsersSet(rt);
            usersSet.Add(__instance);
        }

        private static HashSet<CVRTexturePropertyParser> GetUsersSet(RenderTexture rt)
        {
            if (!users.TryGetValue(rt, out var usersSet))
            {
                users[rt] = usersSet = new HashSet<CVRTexturePropertyParser>();
            }
            return usersSet;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CVRTexturePropertyParser.OnDestroy))]
        private static void OnDestroy(CVRTexturePropertyParser __instance)
        {
            if (!__instance || !__instance.texture)
                return;
            var rt = __instance.texture;
            var usersSet = GetUsersSet(rt);
            usersSet.Remove(__instance);
            if (usersSet.Count != 0)
                return;
            users.Remove(rt);
            if (RT_outputTexture2D.TryRemove(rt, out var existingTexture2D))
                GameObject.DestroyImmediate(existingTexture2D);
            RT_ColorArray.TryRemove(rt, out var _);
            if (RT_ColorNativeArray.TryGetValue(rt, out var nativeArray))
            {
                nativeArray.Dispose();
                RT_ColorNativeArray.Remove(rt);
            }
            readbackRequests.Remove(rt);
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRTexturePropertyParserTask), nameof(CVRTexturePropertyParserTask.Update))]
        public static bool Update(CVRTexturePropertyParserTask __instance, CVRTexturePropertyParser parser)
        {
            enableMod = Pref.Value;
            syncGetTextureColor = syncGetTextureColorPref.Value;
            if (!enableMod)
                return true;

            if (__instance._get == null || __instance._set == null || __instance._isUpdating)
                return false;

            __instance._isUpdating = true;
            GetTextureColorAsync(__instance, parser, __instance.x, __instance.y);
            return false;
        }

        public static bool GetTextureColorAsync(CVRTexturePropertyParserTask task, CVRTexturePropertyParser parser, int x, int y)//, Action<Color> onComplete)
        {
            if (!enableMod)
                return true;

            if (parser.texture == null)
            {
                task.InnerUpdate(Black);
                return false;
            }
            Color32 c32 = Black32;
            var rt = parser.texture;
            int index = rt.width * y + x;

            if (!startedUpdate.Contains(rt))
                ConvertRenderTextureToTexture2DAsync(rt);

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var nativeArray = GetCachedColorNativeArray(rt);
                if (index < nativeArray.Length)
                {
                    c32 = nativeArray[index];
                }
            }
            else
            {
                var array = GetCachedColorArray(rt);
                if (index < array.Length)
                    c32 = array[index];
            }
            task.InnerUpdate(c32);
            return false;
        }
        private static Color32 Black32 = new Color32(0, 0, 0, 255);
        private static Color Black = new Color(0, 0, 0, 255);
        public static void ConvertRenderTextureToTexture2DAsync(RenderTexture renderTexture)
        {
            startedUpdate.Add(renderTexture);
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                var texture = GetCachedTexture2D(renderTexture);
                var colorArray = GetCachedColorArray(renderTexture);
                UseFallback(renderTexture, texture, colorArray);
                return;
            }
            if (!readbackRequests.TryGetValue(renderTexture, out var request))
            {
                var nativeArray = GetCachedColorNativeArray(renderTexture);
                request = readbackRequests[renderTexture] = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, renderTexture, 0, TextureFormat.RGBA32);
            }
            request.Update();
            if (syncGetTextureColor)
                readbackRequests[renderTexture].WaitForCompletion();
        }

        private static Texture2D GetCachedTexture2D(RenderTexture renderTexture, bool createNew = true)
        {
            Texture2D output;
            if (!RT_outputTexture2D.TryGetValue(renderTexture, out output) && createNew)
            {
                output = RT_outputTexture2D[renderTexture] = new Texture2D(renderTexture.width, renderTexture.height,
                    TextureFormat.RGBA32, 0, false);
            }
            return output;
        }

        private static NativeArray<Color32> GetCachedColorNativeArray(RenderTexture renderTexture, bool createNew = true)
        {
            if (!RT_ColorNativeArray.TryGetValue(renderTexture, out var colorNativeArray) && createNew)
            {
                int size = renderTexture.width * renderTexture.height;
                colorNativeArray = RT_ColorNativeArray[renderTexture] = new NativeArray<Color32>(size, Allocator.Persistent);
            }
            return colorNativeArray;
        }
        private static Color32[] GetCachedColorArray(RenderTexture renderTexture, bool createNew = true)
        {
            if (!RT_ColorArray.TryGetValue(renderTexture, out var colorArray) && createNew)
            {
                int size = renderTexture.width * renderTexture.height;
                colorArray = RT_ColorArray[renderTexture] = new Color32[size];
            }
            return colorArray;
        }

        private static void UseFallback(RenderTexture renderTexture, Texture2D output, Color32[] colorArray)
        {
            var active = RenderTexture.active;
            RenderTexture.active = renderTexture;
            output.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = active;
            var rawColorData = output.GetRawTextureData<Color32>();
            if (colorArray.Length != rawColorData.Length)
                return;
            rawColorData.CopyTo(colorArray);
            rawColorData.Dispose();
        }
    }
    
}
