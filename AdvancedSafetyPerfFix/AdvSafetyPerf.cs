// Copyright © 2023 - All Rights Reserved
using Assets.ABI_RC.Systems.Safety.AdvancedSafety;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zettai;

[assembly: MelonInfo(typeof(AdvSafetyPerf), "Advanced Safety performance fix", "1.0", "Zettai")]
[assembly: MelonGame(null, null)]

namespace Zettai
{
    public class AdvSafetyPerf : MelonMod
    {
        private static MelonPreferences_Entry<bool> enableAdvancedSafetyMaterialFix;
        private static MelonPreferences_Entry<bool> filterShaderNames;
        public override void OnApplicationStart()
        {
            var category = MelonPreferences.CreateCategory("Zettai");
            enableAdvancedSafetyMaterialFix = category.CreateEntry("enableAdvancedSafetyMaterialFix", true, "Advanced Safety lag fix");
            filterShaderNames = category.CreateEntry("filterShaderNames", true, "Advanced Safety: filter shader names");
        }
        [HarmonyPatch(typeof(ComponentAdjustment), nameof(ComponentAdjustment.VisitRenderer))]
        class AdvSafetyMaterialsPatch
        {
            private static readonly HashSet<Shader> goodShaderNames = new HashSet<Shader>();
            private static readonly HashSet<Shader> badShaderNames = new HashSet<Shader>();

            private static bool ShaderNameAcceptable(Shader shader)
            {
                if (!filterShaderNames.Value)
                    return true;
                if (goodShaderNames.Contains(shader))
                    return true;
                if (badShaderNames.Contains(shader))
                    return false;
                string name = shader?.name;
                if (string.IsNullOrEmpty(name) || name.Length <= 255 && !name.Contains("\r") && !name.Contains("\n"))
                {
                    goodShaderNames.Add(shader);
                    return true;
                }
                badShaderNames.Add(shader);
                return false;
            }
            static bool Prefix(Renderer renderer, ref int totalCount, ref int deletedCount, ref int polyCount, ref int materialCount, ref int meshesCount, GameObject obj, List<SkinnedMeshRenderer> skinnedRendererList, int maxMeshes, int maxPolygons)
            {
                if (!enableAdvancedSafetyMaterialFix.Value)
                    return true;
                try
                {
                    FindFallbackShader();
                    VisitRendererFix(renderer, ref totalCount, ref deletedCount, ref polyCount, ref materialCount, ref meshesCount, obj, skinnedRendererList, maxMeshes, maxPolygons);
                }
                catch (Exception e)
                {
                    MelonLogger.Error(e);
                    MelonLogger.Error(e.StackTrace);
                }
                return false;
            }

            private static void VisitRendererFix(Renderer renderer, ref int totalCount, ref int deletedCount, ref int polyCount, ref int materialCount, ref int meshesCount, GameObject obj, List<SkinnedMeshRenderer> skinnedRendererList, int maxMeshes, int maxPolygons)
            {
                if (renderer == null || obj == null || skinnedRendererList == null)
                    return;
                renderer.sortingOrder = 0;
                renderer.sortingLayerID = 0;
                totalCount++;
                var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
                var meshFilter = obj.GetComponent<MeshFilter>();
                if (skinnedMeshRenderer != null)
                {
                    skinnedRendererList.Add(skinnedMeshRenderer);
                    if (BoundsAreBad(skinnedMeshRenderer))
                    {
                        deletedCount++;
                        UnityEngine.Object.DestroyImmediate(skinnedMeshRenderer);
                        return;
                    }
                }
                renderer.GetSharedMaterials(ComponentAdjustment.OurMaterialsList);
                if (ComponentAdjustment.OurMaterialsList.Count == 0)
                     return;

                    foreach (var ourMaterial in ComponentAdjustment.OurMaterialsList)
                    {
                        if (ourMaterial == null || ourMaterial.shader == ComponentAdjustment._standardShader)
                            continue;
                        if (!ShaderNameAcceptable(ourMaterial?.shader))
                            ourMaterial.shader = ComponentAdjustment._standardShader;
                    }
                var mesh = skinnedMeshRenderer ? skinnedMeshRenderer.sharedMesh : null;
                if (!mesh)
                    mesh = meshFilter ? meshFilter.sharedMesh : null;
                int subMeshCount = 0;
                if (mesh) 
                {
                    meshesCount++;
                    if (meshesCount >= maxMeshes)
                    {
                        UnityEngine.Object.DestroyImmediate(renderer);
                        return;
                    }
                    subMeshCount = mesh.subMeshCount;
                    var (meshPolyCount, firstSubmeshOverLimit) = ComponentAdjustment.CountMeshPolygons(mesh, maxPolygons - polyCount);
                    if (firstSubmeshOverLimit >= 0)
                    {
                        ComponentAdjustment.OurMaterialsList.RemoveRange(firstSubmeshOverLimit, ComponentAdjustment.OurMaterialsList.Count - firstSubmeshOverLimit);
                        renderer.materials = ComponentAdjustment.OurMaterialsList.ToArray();
                    }
                    polyCount += meshPolyCount;
                }
                subMeshCount += 2;
                if (subMeshCount < ComponentAdjustment.OurMaterialsList.Count)
                {
                    UnityEngine.Object.Destroy(renderer.gameObject);
                }
                subMeshCount = Math.Min(750 - materialCount, subMeshCount);
                if (subMeshCount < ComponentAdjustment.OurMaterialsList.Count)
                {
                    renderer.GetSharedMaterials(ComponentAdjustment.OurMaterialsList);
                    deletedCount += ComponentAdjustment.OurMaterialsList.Count - subMeshCount;
                    ComponentAdjustment.OurMaterialsList.RemoveRange(subMeshCount, ComponentAdjustment.OurMaterialsList.Count - subMeshCount);
                    renderer.materials = ComponentAdjustment.OurMaterialsList.ToArray();
                }
                materialCount += ComponentAdjustment.OurMaterialsList.Count;
            }

            private static void FindFallbackShader()
            {
                if (ComponentAdjustment._standardShader == null)
                    ComponentAdjustment._standardShader = Shader.Find("Standard");
            }

            private static bool BoundsAreBad(SkinnedMeshRenderer skinnedMeshRenderer)
            {
                Bounds bounds = skinnedMeshRenderer.bounds;
                Bounds localBounds = skinnedMeshRenderer.localBounds;
                return bounds.min.IsBad() || bounds.min.IsAbsurd() || bounds.max.IsBad() ||
                       bounds.max.IsAbsurd() || bounds.extents.IsBad() || bounds.extents.IsAbsurd() ||
                       localBounds.min.IsBad() || localBounds.min.IsAbsurd() || localBounds.max.IsBad() ||
                       localBounds.max.IsAbsurd() || localBounds.extents.IsBad() || localBounds.extents.IsAbsurd();
            }
        }
    }
}