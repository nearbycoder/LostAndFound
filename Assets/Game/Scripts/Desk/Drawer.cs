using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// A sliding drawer. Spring-driven with a little overshoot, wood sounds on open and close, and the
    /// contents rattle when it stops. Items inside are children of the drawer so they ride along.
    /// </summary>
    public class Drawer : Interactable
    {
        public string letter;
        public Vector3 slideDir = Vector3.right; // in parent space
        public float travel = 0.25f;
        public Transform floor;
        public bool isIron;
        public bool locked;
        public TextMeshPro label;

        Vector3 home;
        Spring spring;
        bool open;
        float lastVelocity;
        readonly List<ItemView> contents = new();
        float humAmount;

        public bool IsOpen => open;
        public IReadOnlyList<ItemView> Contents => contents;
        public override CursorKind Cursor => CursorKind.Hand;
        public override string Hint => locked ? "Locked" : open ? "Close drawer" : $"Open drawer {letter}";

        void Awake() => home = transform.localPosition;

        public void Init()
        {
            home = transform.localPosition;
        }

        public void Add(ItemView item) { if (!contents.Contains(item)) contents.Add(item); }
        public void Remove(ItemView item) => contents.Remove(item);

        public override void OnClick()
        {
            if (locked)
            {
                AudioDirector.Play("iron_rattle", 0.6f, Random.Range(0.95f, 1.05f));
                StartCoroutine(Tween.Run(0.3f, k => transform.localPosition = home + slideDir * Mathf.Sin(k * Mathf.PI * 3f) * 0.006f * (1 - k), Ease.Linear));
                return;
            }
            SetOpen(!open);
        }

        public void SetOpen(bool value, bool silent = false)
        {
            if (open == value) return;
            open = value;
            if (!silent)
            {
                if (isIron) AudioDirector.Play(open ? "iron_open" : "iron_close", 0.8f, Random.Range(0.94f, 1.04f));
                else AudioDirector.Play(open ? "drawer_open" : "drawer_close", 0.75f, Random.Range(0.9f, 1.1f));
            }
            Desk.I?.OnDrawerToggled(this, open);
        }

        public void SetHum(float amount) => humAmount = amount;

        void Update()
        {
            float target = open ? travel : 0f;
            float before = spring.velocity;
            spring.Step(target, open ? 2.6f : 3.2f, open ? 0.55f : 0.62f, Time.deltaTime);
            Vector3 jitter = Vector3.zero;
            if (humAmount > 0.01f)
            {
                float t = Time.time;
                jitter = new Vector3(Mathf.Sin(t * 61f), Mathf.Sin(t * 47f) * 0.5f, Mathf.Sin(t * 53f)) * 0.0012f * humAmount;
            }
            transform.localPosition = home + slideDir * spring.value + jitter;

            // contents rattle when the drawer decelerates hard (hits its stop)
            float decel = Mathf.Abs(spring.velocity - before) / Mathf.Max(Time.deltaTime, 1e-4f);
            if (decel > 6f && Mathf.Sign(spring.velocity) != Mathf.Sign(lastVelocity) && Mathf.Abs(lastVelocity) > 0.25f)
            {
                foreach (var item in contents) item.Jiggle(Mathf.Clamp01(Mathf.Abs(lastVelocity)) * 1.2f);
                if (!open) AudioDirector.Play("drawer_thunk", 0.5f, Random.Range(0.9f, 1.1f));
            }
            lastVelocity = spring.velocity;
        }

        public Vector3 SlotPosition(int index, int count)
        {
            // spread items along the drawer's depth (away from the front)
            float depth = 0.26f;
            float t = count <= 1 ? 0.5f : index / (float)(count - 1);
            Vector3 back = -slideDir.normalized;
            Vector3 p = floor != null ? floor.localPosition : Vector3.zero;
            return p + back * Mathf.Lerp(-depth * 0.32f, depth * 0.32f, t);
        }
    }
}
