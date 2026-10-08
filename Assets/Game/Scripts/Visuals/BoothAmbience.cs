using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostAndFound
{
    /// <summary>
    /// What the booth's air and polish give back. A reflection probe over the desk, rendered by the game once the desk is
    /// built (and again when the fidelity step changes), so the brass, the bell, glaze, glass and varnish reflect the room
    /// instead of black (the scene has no sky). And dust drifting in the lamp's light. Both follow Settings > Graphics
    /// fidelity (FidelityStep.reflectionRes and .dust); Low has neither.
    /// </summary>
    public class BoothAmbience : MonoBehaviour
    {
        public static BoothAmbience I { get; private set; }
        ReflectionProbe probe;
        ParticleSystem dust;
        Light lamp;
        int probeRes = -1, dustCount = -1;
        Coroutine rendering;

        /// <summary>The probe's last render, for test logs.</summary>
        public static int ProbeResolution => I != null && I.probe != null && I.probe.enabled ? I.probe.resolution : 0;

        public void Init(Desk desk)
        {
            I = this;
            lamp = desk.props != null ? desk.props.lampLight : null;

            // the probe: at the desk's middle, a little below the eye, boxed to the booth so near things reflect where they are
            var go = new GameObject("BoothReflections");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 1.05f, 0.35f);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.boxProjection = true;
            probe.size = new Vector3(3.4f, 2.6f, 3.2f);   // wall to wall, floor to lintel, the clerk's back to past the window
            probe.center = new Vector3(0f, 0.05f, 0.25f);
            probe.blendDistance = 0.4f;
            probe.hdr = true;
            probe.intensity = 1f;
            probe.nearClipPlane = 0.05f;
            probe.farClipPlane = 30f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.06f, 0.08f, 0.1f);   // the camera's own background
            probe.shadowDistance = 7f;
            probe.enabled = false;

            if (lamp != null) MakeDust();
            GraphicsQuality.Applied += Apply;
            Apply();
        }

        void OnDestroy()
        {
            GraphicsQuality.Applied -= Apply;
            if (I == this) I = null;
            if (dust != null) Destroy(dust.GetComponent<ParticleSystemRenderer>().sharedMaterial);   // the game is rebuilt in place (Soak)
        }

        void Apply()
        {
            var s = GraphicsQuality.Current;
            if (s.reflectionRes != probeRes)
            {
                probeRes = s.reflectionRes;
                if (rendering != null) StopCoroutine(rendering);
                rendering = StartCoroutine(RenderProbe(probeRes));
            }
            if (dust != null && s.dust != dustCount)
            {
                dustCount = s.dust;
                var main = dust.main;
                main.maxParticles = Mathf.Max(1, dustCount);
                var em = dust.emission;
                em.rateOverTime = dustCount / main.startLifetime.constantMax * 1.6f;
                dust.Clear();
                if (dustCount > 0) { dust.Simulate(main.startLifetime.constantMax, true, true); dust.Play(); }
                else dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>Wait for the desk to be laid out (and the lights' first frame), then take the probe's picture of it.</summary>
        IEnumerator RenderProbe(int res)
        {
            if (res <= 0 || Game.Arg("-lafNoProbe") != null) { probe.enabled = false; yield break; }   // -lafNoProbe: test runs, to time it
            QualitySettings.realtimeReflectionProbes = true;   // off in the project's quality levels; the probe only renders when asked
            yield return null;
            yield return null;
            probe.resolution = res;
            probe.enabled = true;
            int id = probe.RenderProbe();
            for (int i = 0; i < 120 && !probe.IsFinishedRendering(id); i++) yield return null;
            Debug.Log(probe.IsFinishedRendering(id) ? $"[Reflections] the booth's reflection probe rendered at {res}" : $"[Reflections] the booth's reflection probe didn't finish rendering at {res}");
            rendering = null;
        }

        /// <summary>Motes in the lamp's pool, drifting slowly and catching the light: soft warm specks, additive, fading in
        /// and out, only inside the lamp's cone over the desk.</summary>
        void MakeDust()
        {
            var go = new GameObject("LampDust");
            go.transform.SetParent(transform, false);
            // the cone's middle stretch: from just under the shade to a hand's height above the felt
            Vector3 apex = lamp.transform.position, axis = lamp.transform.forward;
            go.transform.position = apex + axis * 0.18f;
            go.transform.rotation = Quaternion.LookRotation(axis);
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.004f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.006f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.62f, 0.55f), new Color(1f, 0.93f, 0.8f, 0.9f));
            main.gravityModifier = 0.0005f;
            main.useUnscaledTime = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = 0.05f;
            shape.length = 0.42f;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.012f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.08f;
            noise.damping = true;
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = dust.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var template = Resources.Load<Material>("Materials/M_Dust");
            if (template != null)
            {
                var m = new Material(template);
                m.SetTexture("_BaseMap", SoftDot());
                r.sharedMaterial = m;
            }
            else { Destroy(go); dust = null; }
        }

        static Texture2D softDot;

        /// <summary>A warm speck: bright at the middle, nothing at the edge.</summary>
        static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int N = 32;
            softDot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DustDot" };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f - 0.5f, N / 2f - 0.5f)) / (N / 2f);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f)));
                }
            softDot.Apply();
            return softDot;
        }
    }
}
