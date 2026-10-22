using UnityEngine;

namespace StackIt.Game
{
    /// <summary>
    /// Materials created by Stack IT ▸ Setup Project in Resources/StackIt, so their URP shaders are
    /// included in builds. Shader.Find is only a fallback (works in the editor, not in players).
    /// </summary>
    public static class Materials
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private static Material _block;
        private static Material _sky;
        private static Material _particle;

        public static Material Block => _block != null ? _block : _block = Load("BlockMaterial", "Universal Render Pipeline/Lit", true);
        public static Material Sky => _sky != null ? _sky : _sky = Load("SkyMaterial", "Universal Render Pipeline/Unlit", false);
        public static Material Particle => _particle != null ? _particle : _particle = Load("ParticleMaterial", "Universal Render Pipeline/Particles/Unlit", false);

        private static Material Load(string name, string fallbackShader, bool emission)
        {
            var material = Resources.Load<Material>("StackIt/" + name);
            if (material != null) return material;
            Debug.LogWarning($"Missing Resources/StackIt/{name}.mat; run Stack IT ▸ Setup Project. Using {fallbackShader}.");
            material = new Material(Shader.Find(fallbackShader));
            if (emission) material.EnableKeyword("_EMISSION");
            return material;
        }
    }
}
