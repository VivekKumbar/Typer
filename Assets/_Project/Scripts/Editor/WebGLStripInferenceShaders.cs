using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

// com.unity.ai.inference (installed for the editor AI tooling) ships its compute and
// pixel shaders in a Resources folder, so they land in every player build (~9 MB).
// The game never runs inference, so strip every variant of them from WebGL builds.
class WebGLStripInferenceShaders : IPreprocessShaders, IPreprocessComputeShaders
{
    const string InferencePackage = "Packages/com.unity.ai.inference/";

    public int callbackOrder => 0;

    static bool Strip(Object shader) =>
        EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL &&
        AssetDatabase.GetAssetPath(shader).StartsWith(InferencePackage);

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (Strip(shader)) data.Clear();
    }

    public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
    {
        if (Strip(shader)) data.Clear();
    }
}
