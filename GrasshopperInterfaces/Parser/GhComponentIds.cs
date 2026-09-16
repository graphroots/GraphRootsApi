using System;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// Ids of Grasshopper components that we need to handle in a special way during parsing, 
    /// e.g. because they have a custom serialization format (scripted components) or because 
    /// they represent important structural elements of the graph (clusters and groups). 
    /// </summary>
    public static class GhComponentIds
    {

        public static Guid ClusterInput = Guid.Parse("448de216-3a12-43cf-a135-e3bfafc87744");

        public static Guid ClusterOutput = Guid.Parse("a4b285fe-2e13-4204-b65c-189aa6704da5");

        public static Guid Group = Guid.Parse("c552a431-af5b-46a9-a8a4-0fcbc27ef596");

        public static Guid Scribble = Guid.Parse("7f5c6c55-f846-4a08-9c9a-cfdc285cc6fe");

        // cluster component
        public static Guid GRASSHOPPER_CLUSTER_COMPONENT_ID = new Guid("f31d8d7a-7536-4ac8-9c96-fde6ecda4d0a");

        // script components (legacy from Rhino 8)
        public static Guid GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID = new Guid("{a9a8ebd2-fff5-4c44-a8f5-739736d129ba}");
        public static Guid GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE = new Guid("{f5e3456b-dcfc-4faa-ac4e-7804cb75ee6d}");
        public static Guid GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE2 = new Guid("{88c3f2b5-27f7-48a2-9528-1397fad62b93}");
        public static Guid GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID = new Guid("{079bd9bd-54a0-41d4-98af-db999015f63d}");
        public static Guid GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE = new Guid("{1e9e08fc-c31e-49eb-a36c-90de5e62e5f5}");
        public static Guid GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE2 = new Guid("{fb6aba99-fead-4e42-b5d8-c6de5ff90ea6}");
        public static Guid GRASSHOPPER_LEGACY_PYTHON_SCRIPT_COMPONENT_ID = new Guid("410755b1-224a-4c1e-a407-bf32fb45ea7e");

        public static Guid PYTHONINTERPRETER_LEGACY_PLUGIN_ID = new Guid("16cf80bc-9018-cbdb-2238-976eb17fd30d");

        // new scripting components (from Rhino 8)
        public static Guid RHINOCODE_PLUGIN_ID = new Guid("066d0a87-236f-4eae-a0f4-9e42f5327962");

        public static Guid RHINOCODE_GENERIC_SCRIPT_COMPONENT_ID = new Guid("{c9b2d725-6f87-4b07-af90-bd9aefef68eb}");
        public static Guid RHINOCODE_CSHARP_SCRIPT_COMPONENT_ID = new Guid("{b6ba1144-02d6-4a2d-b53c-ec62e290eeb7}");
        public static Guid RHINOCODE_PYTHON2_SCRIPT_COMPONENT_ID = new Guid("{97aa26ef-88ae-4ba6-98a6-ed6ddeca11d1}");
        public static Guid RHINOCODE_PYTHON3_SCRIPT_COMPONENT_ID = new Guid("{719467e6-7cf5-4848-99b0-c5dd57e5442c}");

        public static bool IsLegacyScriptComponent(Guid guid) =>
            guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID ||
            guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE ||
            guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE2 ||
            guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID ||
            guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE ||
            guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE2 ||
            guid == GRASSHOPPER_LEGACY_PYTHON_SCRIPT_COMPONENT_ID;

        public static string GetLegacyScriptComponentLanguage(Guid guid)
        {
            if (guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID ||
                guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE ||
                guid == GRASSHOPPER_LEGACY_CSHARP_SCRIPT_COMPONENT_ID_OBSOLETE2)
            {
                return "csharp_legacy";
            }
            else if (guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID ||
                     guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE ||
                     guid == GRASSHOPPER_LEGACY_VB_SCRIPT_COMPONENT_ID_OBSOLETE2)
            {
                return "vb_legacy";
            }
            else if (guid == GRASSHOPPER_LEGACY_PYTHON_SCRIPT_COMPONENT_ID)
            {
                return "python2_legacy";
            }
            else
            {
                throw new ArgumentException("The provided GUID does not correspond to a legacy script component.");
            }
        }

        public static bool IsRhinoCodeScriptComponent(Guid guid) =>
           guid == RHINOCODE_GENERIC_SCRIPT_COMPONENT_ID ||
           guid == RHINOCODE_CSHARP_SCRIPT_COMPONENT_ID ||
           guid == RHINOCODE_PYTHON2_SCRIPT_COMPONENT_ID ||
           guid == RHINOCODE_PYTHON3_SCRIPT_COMPONENT_ID;

        public static string GetRhinoCodeScriptComponentLanguage(Guid guid)
        {
            if (guid == RHINOCODE_CSHARP_SCRIPT_COMPONENT_ID)
                return "csharp_rhinocode";
            else if (guid == RHINOCODE_PYTHON2_SCRIPT_COMPONENT_ID)
                return "python2_rhinocode";
            else if (guid == RHINOCODE_PYTHON3_SCRIPT_COMPONENT_ID)
                return "python3_rhinocode";
            else if (guid == RHINOCODE_GENERIC_SCRIPT_COMPONENT_ID)
                return "unknown_rhinocode";
            else
                throw new ArgumentException("The provided GUID does not correspond to a RhinoCode script component.");
        }

    }

}
