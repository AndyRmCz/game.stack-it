using UnityEngine;
using UnityEngine.Rendering;

namespace StackIt.Game
{
    /// <summary>
    /// Materials created by Stack IT ▸ Setup Project in Resources/StackIt, so their URP shaders (and
    /// the variants used here) are included in builds. Shader.Find is only a fallback (works in the
    /// editor, not in players). Runtime tweaks go on copies so play mode never edits the assets.
    /// </summary>
    public static class Materials
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        private static Material _block;
        private static Material _sky;
        private static Material _fx;
        private static Material _dust;

        /// <summary>Matte porcelain: soft highlight on the bevels, no gloss, emission only for flashes.</summary>
        public static Material Block
        {
            get
            {
                if (_block != null) return _block;
                _block = new Material(Load("BlockMaterial", "Universal Render Pipeline/Lit"));
                _block.EnableKeyword("_EMISSION");
                _block.SetFloat(Smoothness, 0.22f);
                _block.SetColor(EmissionColor, Color.black);
                _block.enableInstancing = true;
                return _block;
            }
        }

        public static Material Sky => _sky != null ? _sky : _sky = Load("SkyMaterial", "Universal Render Pipeline/Unlit");

        /// <summary>Unlit, alpha-blended: rings and glows that fade out.</summary>
        public static Material Fx
        {
            get
            {
                if (_fx != null) return _fx;
                // Setup Project makes the asset transparent (so builds keep that variant); the copy is
                // made transparent too, in case the asset predates that.
                _fx = new Material(Load("ParticleMaterial", "Universal Render Pipeline/Particles/Unlit"));
                MakeTransparent(_fx);
                return _fx;
            }
        }

        /// <summary>Fx with a soft round dot, for dust particles.</summary>
        public static Material Dust
        {
            get
            {
                if (_dust != null) return _dust;
                _dust = new Material(Fx);
                _dust.SetTexture(BaseMap, SoftDot());
                return _dust;
            }
        }

        private static Material Load(string name, string fallbackShader)
        {
            var material = Resources.Load<Material>("StackIt/" + name);
            if (material != null) return material;
            Debug.LogWarning($"Missing Resources/StackIt/{name}.mat; run Stack IT ▸ Setup Project. Using {fallbackShader}.");
            return new Material(Shader.Find(fallbackShader));
        }

        /// <summary>URP alpha blending, as the material inspector sets it (shared with Setup Project).</summary>
        public static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }

        private static Texture2D SoftDot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d)));
            }
            tex.Apply();
            return tex;
        }
    }
}
