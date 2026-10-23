using StackIt.Core.Simulation;
using UnityEngine;

namespace StackIt.Game
{
    /// <summary>Glowing square outline floating at the rival's height: the duel target.</summary>
    public sealed class RivalGhostView : MonoBehaviour
    {
        private const float Thickness = 0.06f;
        private readonly Transform[] _edges = new Transform[4];
        private MaterialPropertyBlock _props; // Unity forbids creating it in a field initializer

        public string RivalName { get; private set; }
        public int RivalHeight { get; private set; }
        public bool Visible => gameObject.activeSelf;

        // Runs inside AddComponent (the object is still active), so Create can use _props.
        private void Awake() => _props = new MaterialPropertyBlock();

        public static RivalGhostView Create(Transform parent)
        {
            var go = new GameObject("RivalGhost");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RivalGhostView>();
            for (int i = 0; i < 4; i++)
            {
                GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(edge.GetComponent<Collider>());
                edge.transform.SetParent(go.transform, false);
                var renderer = edge.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.Block;
                view._edges[i] = edge.transform;
            }
            go.SetActive(false);
            return view;
        }

        public void Show(string rivalName, int rivalHeight)
        {
            RivalName = rivalName;
            RivalHeight = rivalHeight;
            gameObject.SetActive(rivalHeight > 0);
            float size = SimConstants.BlockSize * StackView.UnitsToWorld + 0.6f;
            float half = size / 2f;
            transform.localPosition = new Vector3(0f, StackView.Y(rivalHeight) + StackView.BlockHeight / 2f + 0.02f, 0f);
            _edges[0].localPosition = new Vector3(0f, 0f, half);
            _edges[1].localPosition = new Vector3(0f, 0f, -half);
            _edges[2].localPosition = new Vector3(half, 0f, 0f);
            _edges[3].localPosition = new Vector3(-half, 0f, 0f);
            _edges[0].localScale = _edges[1].localScale = new Vector3(size, Thickness, Thickness);
            _edges[2].localScale = _edges[3].localScale = new Vector3(Thickness, Thickness, size);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The rival's frame in the look's accent2, glowing a little (Neon's bloom catches it).</summary>
        public void SetColor(Color accent2, float glow)
        {
            foreach (Transform edge in _edges)
            {
                var renderer = edge.GetComponent<MeshRenderer>();
                renderer.GetPropertyBlock(_props);
                _props.SetColor(Materials.Top, accent2);
                _props.SetColor(Materials.Left, accent2);
                _props.SetColor(Materials.Right, accent2);
                _props.SetFloat(Materials.Glow, glow);
                renderer.SetPropertyBlock(_props);
            }
        }

        private void Update()
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
            transform.localScale = new Vector3(pulse, 1f, pulse);
        }
    }
}
