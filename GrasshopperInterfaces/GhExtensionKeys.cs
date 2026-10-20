namespace GraphRoots.Grasshopper;

/// <summary>
/// Well-known <c>gh.*</c> extension keys written by the Grasshopper loader.
/// </summary>
public static class GhExtensionKeys
{
    public const string Prefix = "gh.";

    public const string ComponentGuid = "gh.componentGuid";

    public const string ScriptSource = "gh.scriptSource";

    public const string UsingSource = "gh.usingSource";

    public const string AdditionalSource = "gh.additionalSource";

    public const string ScriptText = "gh.scriptText";

    public const string ClusterParamMap = "gh.clusterParamMap";

    public const string ClusterVersionId = "gh.clusterVersionId";

    public const string ClusterSourcePortId = "gh.clusterSourcePortId";

    public const string ClusterTargetPortId = "gh.clusterTargetPortId";

    public const string AssemblyName = "gh.assemblyName";

    /// <summary>
    /// SHA-256 (lowercase hex) of the component's archive chunk without name, nickname, layout and
    /// wiring items. Equal across two versions of a document means the component's settings and
    /// internal state (slider value, panel text, ...) did not change.
    /// </summary>
    public const string StateHash = "gh.stateHash";
}
