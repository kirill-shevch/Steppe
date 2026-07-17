using Steppe.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Steppe.Editor
{
    public static class SteppeVolumetricCloudRendererInstaller
    {
        private const string ShaderName = "Hidden/Steppe/Volumetric Clouds";

        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        [MenuItem("Steppe/Rendering/Install Volumetric Cloud Renderer")]
        public static void Install()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"Shader '{ShaderName}' was not found.");
            }

            for (var index = 0; index < RendererDataPaths.Length; index++)
            {
                InstallIntoRenderer(RendererDataPaths[index], shader);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Steppe volumetric cloud renderer is installed for PC and Mobile URP renderers.");
        }

        private static void InstallIntoRenderer(string rendererPath, Shader shader)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData == null)
            {
                throw new System.InvalidOperationException($"URP renderer data was not found at '{rendererPath}'.");
            }

            for (var index = 0; index < rendererData.rendererFeatures.Count; index++)
            {
                if (rendererData.rendererFeatures[index] is SteppeVolumetricCloudRendererFeature existing)
                {
                    existing.SetCloudShader(shader);
                    existing.SetActive(true);
                    EditorUtility.SetDirty(existing);
                    rendererData.SetDirty();
                    EditorUtility.SetDirty(rendererData);
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<SteppeVolumetricCloudRendererFeature>();
            feature.name = "Steppe Volumetric Clouds";
            feature.hideFlags = HideFlags.HideInHierarchy;
            feature.SetCloudShader(shader);
            feature.SetActive(true);
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                Object.DestroyImmediate(feature, true);
                throw new System.InvalidOperationException(
                    $"Could not obtain the local file id for the cloud renderer in '{rendererPath}'.");
            }

            var serializedRenderer = new SerializedObject(rendererData);
            var features = serializedRenderer.FindProperty("m_RendererFeatures");
            var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
        }
    }
}
