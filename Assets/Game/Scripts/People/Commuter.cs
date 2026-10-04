using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// A claimant at the window. The Blender model is split into parts (Body, Head, EyeL/EyeR, BrowL/BrowR,
    /// Mouth, HandL/HandR) and animated procedurally: walk-in bob, breathing, blinking, glances, a talk
    /// bob driven by the voice babble, and emotes. Cold visitors frost; strays are sepia; the Grey
    /// Gentleman is drained of colour.
    /// </summary>
    public class Commuter : MonoBehaviour
    {
        public CommuterDef def;
        public Transform body, head, mouth, browL, browR, eyeL, eyeR, handL, handR, hat;
        Vector3 headRest, mouthRestScale, browLRest, browRRest, eyeLScale, eyeRScale, handLRest, handRRest, bodyRest;
        Quaternion headRestRot;
        float talk, talkTarget, blinkTimer, blink, emoteTime, glanceTimer;
        Vector2 glance, glanceTarget;
        string emote = "";
        public bool walking;
        float walkPhase;
        float seed;
        public Vector3 standPos;
        readonly List<Material> instanced = new();

        public static Commuter Spawn(CommuterDef def, Transform parent)
        {
            var go = new GameObject("Commuter_" + def.id);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Commuter>();
            c.def = def;
            c.Build();
            return c;
        }

        void Build()
        {
            seed = Random.value * 100f;
            GameObject model;
            if (ModelLibrary.Exists("People/" + def.ModelName)) model = ModelLibrary.Spawn("People/" + def.ModelName, transform);
            else model = Fallback();
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * def.height;

            body = Find(model, "Body") ?? model.transform;
            head = Find(model, "Head");
            mouth = Find(model, "Mouth");
            browL = Find(model, "BrowL"); browR = Find(model, "BrowR");
            eyeL = Find(model, "EyeL"); eyeR = Find(model, "EyeR");
            handL = Find(model, "HandL"); handR = Find(model, "HandR");
            hat = Find(model, "Hat");
            // parts are exported flat under the root; rebuild the hierarchy the animation expects
            if (head != null && head != body) head.SetParent(body, true);
            foreach (var t in new[] { mouth, browL, browR, eyeL, eyeR, hat })
                if (t != null && head != null) t.SetParent(head, true);
            foreach (var t in new[] { handL, handR })
                if (t != null && body != null && t != body) t.SetParent(body, true);
            if (head != null) { headRest = head.localPosition; headRestRot = head.localRotation; }
            if (mouth != null) mouthRestScale = mouth.localScale;
            if (browL != null) browLRest = browL.localPosition;
            if (browR != null) browRRest = browR.localPosition;
            if (eyeL != null) eyeLScale = eyeL.localScale;
            if (eyeR != null) eyeRScale = eyeR.localScale;
            if (handL != null) handLRest = handL.localPosition;
            if (handR != null) handRRest = handR.localPosition;
            bodyRest = body.localPosition;
            blinkTimer = Random.Range(1f, 3f);

            if (def.sepia) Tint(model, new Color(0.72f, 0.58f, 0.42f), 0.55f);
            if (def.grey) Tint(model, new Color(0.55f, 0.56f, 0.58f), 0.85f);
            if (def.cold) Tint(model, new Color(0.7f, 0.85f, 0.95f), 0.5f, new Color(0.1f, 0.18f, 0.25f));
        }

        static Transform Find(GameObject m, string n) => ModelLibrary.Find(m.transform, n);

        void Tint(GameObject model, Color toward, float amount, Color? emission = null)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials; // instance per character
                foreach (var m in mats)
                {
                    var c = m.GetColor("_BaseColor");
                    float g = c.grayscale;
                    var target = new Color(g * toward.r * 1.4f, g * toward.g * 1.4f, g * toward.b * 1.4f, c.a);
                    m.SetColor("_BaseColor", Color.Lerp(c, target, amount));
                    if (emission.HasValue) MaterialLibrary.SetEmission(m, emission.Value);
                    instanced.Add(m);
                }
                r.materials = mats;
            }
        }

        GameObject Fallback()
        {
            var root = new GameObject("FallbackPerson");
            root.transform.SetParent(transform, false);
            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            b.name = "Body";
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0, 0.9f, 0);
            b.transform.localScale = new Vector3(0.5f, 0.6f, 0.3f);
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "Head";
            h.transform.SetParent(root.transform, false);
            h.transform.localPosition = new Vector3(0, 1.6f, 0);
            h.transform.localScale = Vector3.one * 0.26f;
            foreach (var c in root.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = MaterialLibrary.Get("cloth_6B5B4A");
            return root;
        }

        /// <summary>World position of the right hand (for taking items).</summary>
        public Vector3 HandPosition => handR != null ? handR.position : transform.position + Vector3.up * 1.1f;
        public Vector3 HeadPosition => head != null ? head.position + Vector3.up * 0.08f : transform.position + Vector3.up * 1.6f;

        public IEnumerator WalkTo(Vector3 to, float speed = 1.1f)
        {
            walking = true;
            Vector3 from = transform.position;
            float dist = Vector3.Distance(from, to);
            float dur = Mathf.Max(0.3f, dist / speed);
            // models face -z (towards the booth) at identity rotation
            Vector3 move = new Vector3(to.x - from.x, 0f, to.z - from.z);
            Quaternion face = move.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(-move.normalized, Vector3.up) : Quaternion.identity;
            Quaternion forward = Quaternion.identity;
            yield return Tween.Run(dur, k =>
            {
                transform.position = Vector3.Lerp(from, to, k);
                float turn = Mathf.Clamp01(Mathf.Min(k * 4f, (1 - k) * 4f));
                transform.rotation = Quaternion.Slerp(forward, face, turn * 0.75f);
            }, Ease.InOutSine);
            transform.rotation = forward;
            walking = false;
        }

        public void SetTalking(bool on) => talkTarget = on ? 1f : 0f;

        /// <summary>Called per babble syllable: open the mouth and bob the head.</summary>
        public void Syllable(float strength)
        {
            talk = Mathf.Max(talk, strength);
        }

        public void Emote(string e)
        {
            if (string.IsNullOrEmpty(e)) return;
            emote = e;
            emoteTime = 0f;
            if (e == "happy") AudioDirector.Play("emote_happy", 0.3f, def.pitch);
            if (e == "angry") AudioDirector.Play("emote_huff", 0.35f, def.pitch);
            if (e == "surprised") AudioDirector.Play("emote_gasp", 0.35f, def.pitch);
            if (e == "sad") AudioDirector.Play("emote_sigh", 0.35f, def.pitch);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time + seed;
            emoteTime += dt;

            // walking bob
            if (walking) walkPhase += dt * 9f;
            float bob = walking ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.035f : 0f;
            float sway = walking ? Mathf.Sin(walkPhase) * 3f : 0f;

            // breathing
            float breathe = Mathf.Sin(t * 1.7f) * 0.006f;

            // emote offsets
            Vector3 emoteOffset = Vector3.zero;
            float headTilt = 0f, headNod = 0f, headShake = 0f, browRaise = 0f, slump = 0f;
            float e = emoteTime;
            switch (emote)
            {
                case "happy":
                    emoteOffset.y = Mathf.Abs(Mathf.Sin(e * 10f)) * 0.03f * Mathf.Clamp01(1.2f - e);
                    browRaise = 0.01f * Mathf.Clamp01(2f - e);
                    headTilt = Mathf.Sin(e * 3f) * 6f * Mathf.Clamp01(2f - e);
                    break;
                case "sad":
                    slump = 0.025f * Mathf.Clamp01(e * 2f) * Mathf.Clamp01(4f - e);
                    headNod = 14f * Mathf.Clamp01(e * 2f) * Mathf.Clamp01(4f - e);
                    browRaise = 0.004f;
                    break;
                case "angry":
                    headShake = Mathf.Sin(e * 26f) * 7f * Mathf.Clamp01(1f - e);
                    browRaise = -0.008f * Mathf.Clamp01(3f - e);
                    break;
                case "surprised":
                    emoteOffset.y = Mathf.Sin(Mathf.Clamp01(e * 3f) * Mathf.PI) * 0.04f;
                    browRaise = 0.014f * Mathf.Clamp01(2.5f - e);
                    break;
                case "sly":
                    headTilt = 9f * Mathf.Clamp01(e * 3f) * Mathf.Clamp01(3f - e);
                    browRaise = 0.006f * Mathf.Clamp01(3f - e);
                    break;
                case "shy":
                    headNod = 8f * Mathf.Clamp01(e * 3f) * Mathf.Clamp01(3f - e);
                    headTilt = -6f * Mathf.Clamp01(e * 3f) * Mathf.Clamp01(3f - e);
                    break;
            }

            body.localPosition = bodyRest + Vector3.up * (bob + breathe - slump) + emoteOffset;
            body.localRotation = Quaternion.Euler(0f, 0f, sway);

            // glances: mostly at the clerk, sometimes away
            glanceTimer -= dt;
            if (glanceTimer <= 0f)
            {
                glanceTimer = Random.Range(1.2f, 3.5f);
                glanceTarget = Random.value < 0.65f ? Vector2.zero : new Vector2(Random.Range(-12f, 12f), Random.Range(-6f, 8f));
            }
            glance = Vector2.Lerp(glance, glanceTarget, 1f - Mathf.Exp(-4f * dt));

            // talk
            talk = Mathf.MoveTowards(talk, 0f, dt * 7f);
            if (head != null)
            {
                float talkNod = talk * 3.5f;
                head.localPosition = headRest + Vector3.up * (breathe * 0.5f);
                head.localRotation = headRestRot * Quaternion.Euler(-glance.y * 0.5f + headNod + talkNod, glance.x * 0.6f + headShake, headTilt + Mathf.Sin(t * 0.7f) * 1.5f);
            }
            if (mouth != null)
            {
                float open = 0.25f + talk * 1.6f;
                if (emote == "surprised" && e < 1.5f) open = 1.8f;
                mouth.localScale = new Vector3(mouthRestScale.x * (1f - talk * 0.15f), mouthRestScale.y * open, mouthRestScale.z);
            }

            // blink
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blink = 1f; blinkTimer = Random.Range(2f, 5f); }
            blink = Mathf.MoveTowards(blink, 0f, dt * 9f);
            float lid = 1f - Mathf.Sin(blink * Mathf.PI) * 0.92f;
            if (eyeL != null) eyeL.localScale = new Vector3(eyeLScale.x, eyeLScale.y * lid, eyeLScale.z);
            if (eyeR != null) eyeR.localScale = new Vector3(eyeRScale.x, eyeRScale.y * lid, eyeRScale.z);
            if (browL != null) browL.localPosition = browLRest + Vector3.up * (browRaise + talk * 0.003f);
            if (browR != null) browR.localPosition = browRRest + Vector3.up * (browRaise + talk * 0.002f);

            // hands gesture a little while talking
            if (handL != null) handL.localPosition = handLRest + new Vector3(0f, Mathf.Sin(t * 2.1f) * 0.01f * talkTarget, 0f);
            if (handR != null) handR.localPosition = handRRest + new Vector3(0f, Mathf.Sin(t * 2.6f + 1f) * 0.012f * talkTarget, 0f);
        }

        void OnDestroy()
        {
            foreach (var m in instanced) if (m != null) Destroy(m);
        }
    }
}
