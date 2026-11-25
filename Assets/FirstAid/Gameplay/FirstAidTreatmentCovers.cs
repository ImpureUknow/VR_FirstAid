using UnityEngine;
using UnityEngine.Rendering;

namespace FirstAid
{
    internal static class FirstAidTreatmentCovers
    {
        private const string ResourcePath = "TreatmentCovers/";
        private static readonly string[] CaseNames = { "Abrasion", "Cut", "Scald" };

        public static void Install(PatientCase[] primary, PatientCase[] alternate)
        {
            var dressing = new[]
            {
                Resources.Load<Material>(ResourcePath + "GauzePad"),
                Resources.Load<Material>(ResourcePath + "DressingEdge")
            };
            var film = new[]
            {
                Resources.Load<Material>(ResourcePath + "FilmSurface"),
                Resources.Load<Material>(ResourcePath + "FilmEdge")
            };

            for (int index = 0; index < CaseNames.Length; index++)
            {
                var materials = index == 2 ? film : dressing;
                InstallSite(primary[index], CaseNames[index] + "1", materials);
                InstallSite(alternate[index], CaseNames[index] + "2", materials);
            }
        }

        private static void InstallSite(PatientCase site, string name, Material[] materials)
        {
            var mesh = Resources.Load<Mesh>(ResourcePath + name);
            if (mesh == null || materials[0] == null || materials[1] == null)
            {
                Debug.LogError("A treatment-cover asset could not be loaded: " + name, site.covering);
                return;
            }

            site.covering.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = site.covering.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
