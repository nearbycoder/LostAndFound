using UnityEngine;

namespace LostAndFound
{
    /// <summary>The glass between you and them: frosts over when a cold visitor stands there.</summary>
    public class WindowFX : MonoBehaviour
    {
        public static WindowFX I { get; private set; }
        Material glass;
        Color baseColor;
        float frost, frostTarget;
        ParticleSystem breath;

        void Awake() => I = this;

        public void Init(Transform booth)
        {
            var g = ModelLibrary.Find(booth, "Glass");
            if (g == null) return;
            var r = g.GetComponent<Renderer>();
            if (r == null) return;
            glass = r.material;
            baseColor = glass.GetColor("_BaseColor");
            var tex = Resources.Load<Texture2D>("Textures/frost_glass");
            if (tex != null) glass.SetTexture("_BaseMap", tex);
            glass.mainTextureScale = Vector2.one;
        }

        public void SetFrost(bool on)
        {
            frostTarget = on ? 1f : 0f;
            if (on) AudioDirector.Play("frost_creep", 0.7f);
        }

        void Update()
        {
            frost = Mathf.MoveTowards(frost, frostTarget, Time.deltaTime * (frostTarget > frost ? 0.35f : 0.6f));
            if (glass == null) return;
            var c = Color.Lerp(baseColor, new Color(0.82f, 0.92f, 1f, 0.55f), frost);
            glass.SetColor("_BaseColor", c);
            MaterialLibrary.SetEmission(glass, new Color(0.05f, 0.09f, 0.12f) * frost);
        }
    }
}
