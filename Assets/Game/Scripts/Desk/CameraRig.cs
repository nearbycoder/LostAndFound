using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    public enum View { Counter, Cabinet, Shelf }

    /// <summary>
    /// The seated first-person camera. Three swivel views with eased turns, a gentle mouse "head look",
    /// breathing sway, impulse shake, and temporary focus targets (look down at the slip, lean into an
    /// open drawer, watch the Iron Drawer).
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public Camera cam;
        public Vector3 eye = new(0f, 1.26f, -0.08f);
        public View view = View.Counter;
        public bool allowTurn = true;
        public float baseFov = 58f;

        public static readonly Vector2 CounterAngles = new(0f, -15f);
        public static readonly Vector2 CabinetAngles = new(-100f, -25f);
        public static readonly Vector2 ShelfAngles = new(100f, -17f);

        float yaw, pitch, fov;
        float shake, shakeSeed;
        bool hasFocus;
        Vector3 focusPoint, focusEye;
        float focusFov;
        float focusBlend;
        float edgeTimer;
        int edgeDir;

        public event System.Action<View> ViewChanged;
        public Light handLight;      // a soft key light that follows your gaze when you hold or lean in
        float handLightK;

        void Awake()
        {
            I = this;
            var a = Angles(view);
            yaw = a.x;
            pitch = a.y;
            fov = baseFov;
            shakeSeed = Random.value * 100f;
        }

        public static Vector2 Angles(View v) => v switch
        {
            View.Cabinet => CabinetAngles,
            View.Shelf => ShelfAngles,
            _ => CounterAngles,
        };

        public void SetView(View v, bool instant = false)
        {
            if (v == view && !instant) return;
            view = v;
            if (instant)
            {
                var a = Angles(v);
                yaw = a.x;
                pitch = a.y;
            }
            ViewChanged?.Invoke(v);
            AudioDirector.Play("chair_swivel", 0.35f, Random.Range(0.92f, 1.08f));
        }

        public void Turn(int dir)
        {
            if (!allowTurn) return;
            int idx = view == View.Cabinet ? -1 : view == View.Shelf ? 1 : 0;
            idx = Mathf.Clamp(idx + dir, -1, 1);
            SetView(idx < 0 ? View.Cabinet : idx > 0 ? View.Shelf : View.Counter);
        }

        /// <summary>Look at a point from an optional eye offset; cleared with ClearFocus.</summary>
        public void Focus(Vector3 point, Vector3? fromEye = null, float fovOverride = 0f)
        {
            hasFocus = true;
            focusPoint = point;
            focusEye = fromEye ?? eye;
            focusFov = fovOverride > 0 ? fovOverride : baseFov;
        }

        public void ClearFocus() => hasFocus = false;

        /// <summary>The camera's height of view for a view framed at 16:9 with <paramref name="fov16x9"/>: the same at 16:9 and
        /// wider, and taller on a narrower window (4:3, or one snapped to half a screen) so the view keeps its 16:9 width
        /// and nothing at the sides of the desk (the lamp, the printer, the ledger book, the object in your hands) is cut off.
        /// Every field of view the game asks for (Focus's included) is framed at 16:9.</summary>
        public static float FovFor(float fov16x9, float aspect)
        {
            const float wide = 16f / 9f;
            if (!(aspect > 0f) || aspect >= wide) return fov16x9;
            aspect = Mathf.Max(aspect, 0.5f);
            return 2f * Mathf.Atan(Mathf.Tan(fov16x9 * 0.5f * Mathf.Deg2Rad) * wide / aspect) * Mathf.Rad2Deg;
        }
        public bool HasFocus => hasFocus;

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void Update()
        {
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            if (allowTurn && kb != null && !UIRoot.ModalOpen)
            {
                if (InputX.KeyDown(Key.A) || InputX.KeyDown(Key.LeftArrow)) Turn(-1);
                if (InputX.KeyDown(Key.D) || InputX.KeyDown(Key.RightArrow)) Turn(1);
            }
            UpdateEdgeTurn(dt);

            var target = Angles(view);
            // gentle head-look towards the mouse
            Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 n = new(m.x / Mathf.Max(1, Screen.width) - 0.5f, m.y / Mathf.Max(1, Screen.height) - 0.5f);
            if (UIRoot.ModalOpen || !MouseLive || PointerOut) n = Vector2.zero;   // a pointer that has left the window: look ahead
            float look = Settings.ReduceMotion ? 0.4f : 1f;
            target += new Vector2(n.x * 4.0f, n.y * 2.6f) * look;

            yaw = MathX.DampAngle(yaw, target.x, 7f, dt);
            pitch = MathX.Damp(pitch, target.y, 7f, dt);
            focusBlend = MathX.Damp(focusBlend, hasFocus ? 1f : 0f, 6f, dt);

            float t = Time.time;
            float breathe = Settings.ReduceMotion ? 0f : 1f;
            Vector3 sway = new Vector3(Mathf.Sin(t * 0.37f) * 0.002f, Mathf.Sin(t * 0.52f) * 0.004f, 0f) * breathe;

            Quaternion baseRot = Quaternion.Euler(-pitch, yaw, 0f);
            Vector3 pos = eye + sway;
            Quaternion rot = baseRot;
            float targetFov = baseFov;
            if (focusBlend > 0.001f)
            {
                Vector3 fe = focusEye + sway;
                Quaternion fr = Quaternion.LookRotation((focusPoint - fe).normalized, Vector3.up);
                // keep a little of the head-look while focused
                fr = Quaternion.Euler(-n.y * 1.5f * look, n.x * 2f * look, 0f) * fr;
                float k = Ease.InOutSine(focusBlend);
                pos = Vector3.Lerp(pos, fe, k);
                rot = Quaternion.Slerp(rot, fr, k);
                targetFov = Mathf.Lerp(baseFov, focusFov, k);
            }
            fov = MathX.Damp(fov, targetFov, 6f, dt);

            if (shake > 0.0001f)
            {
                float s = shake * (Settings.ScreenShake ? 1f : 0f);
                float x = (Mathf.PerlinNoise(shakeSeed, t * 28f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(shakeSeed + 7f, t * 28f) - 0.5f) * 2f;
                rot *= Quaternion.Euler(y * s * 2.2f, x * s * 2.2f, x * y * s);
                pos += rot * new Vector3(x, y, 0f) * s * 0.01f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 2.2f);
            }

            transform.SetPositionAndRotation(pos, rot);
            if (cam != null) cam.fieldOfView = FovFor(fov, cam.aspect);
            if (handLight != null)
            {
                bool want = (InspectController.I != null && InspectController.I.Held != null) || (hasFocus && view == View.Cabinet);
                handLightK = MathX.Damp(handLightK, want ? 1f : 0f, 5f, dt);
                handLight.intensity = handLightK * 2.2f;
                handLight.enabled = handLightK > 0.01f;
            }
        }

        Vector2 firstMouse = new(-1f, -1f);
        bool mouseMoved;

        /// <summary>The mouse counts once the window has focus and the pointer has actually moved
        /// (a headless or unfocused window reports a stuck (0,0) pointer).</summary>
        public bool MouseLive
        {
            get
            {
                if (Mouse.current == null || !Application.isFocused) return false;
                var p = Mouse.current.position.ReadValue();
                if (firstMouse.x < 0f) firstMouse = p;
                if ((p - firstMouse).sqrMagnitude > 4f) mouseMoved = true;
                return mouseMoved;
            }
        }

        /// <summary>Whether a pointer last read at <paramref name="p"/> (whole pixels, 0 to size - 1, as the player reads them)
        /// after a step of <paramref name="step"/> has most likely left a window of <paramref name="size"/>. The player is told
        /// nothing when the pointer leaves its window and keeps the last position it had inside, which for a pointer leaving
        /// by a side is at that side, where it turned the desk (Tools/unity.sh pointertest, round 11). A pointer that one more
        /// such step would take onto or past the window's first or last pixel has gone (the readings are rounded, so a step
        /// that seems to land on it was on its way out), unless it's on that pixel already after a big step (8 px or more):
        /// that's a pointer stopped by the side of the screen (a maximised window, or one snapped to a side), which can't
        /// leave. A person slowing to a stop near a side moves by a pixel or two at the end, and stays.</summary>
        public static bool PointerLeft(Vector2 p, Vector2 step, Vector2 size)
        {
            float maxX = size.x - 1f, maxY = size.y - 1f;
            if (step == Vector2.zero) return false;
            Vector2 next = p + step;
            if (next.x > 0f && next.x < maxX && next.y > 0f && next.y < maxY) return false;
            bool onLastPixel = p.x <= 0f || p.x >= maxX || p.y <= 0f || p.y >= maxY;
            return !(onLastPixel && step.magnitude >= 8f);
        }

        Vector2 lastPointer = new(float.NaN, float.NaN), pointerStep;

        /// <summary>The mouse pointer has most likely left the window (see PointerLeft). Never in fullscreen, where it can't,
        /// nor for the gamepad's cursor, which stays on the screen.</summary>
        public bool PointerOut => !Screen.fullScreen && !GamepadInput.Active && !float.IsNaN(lastPointer.x)
                                  && PointerLeft(lastPointer, pointerStep, new Vector2(Screen.width, Screen.height));

        void UpdateEdgeTurn(float dt)
        {
            if (Mouse.current != null)
            {
                var p = Mouse.current.position.ReadValue();
                if (p != lastPointer) { if (!float.IsNaN(lastPointer.x)) pointerStep = p - lastPointer; lastPointer = p; }
            }
            // not while another window has the focus, nor once the pointer has left the window: it still reads as at the edge.
            // Not at all with Settings > Turn at the screen's edge off
            if (!Settings.EdgeTurn || !allowTurn || Mouse.current == null || UIRoot.ModalOpen || hasFocus || !MouseLive || !Application.isFocused || PointerOut) { edgeTimer = 0f; edgeDir = 0; return; }
            float x = Mouse.current.position.ReadValue().x / Mathf.Max(1, Screen.width);
            int dir = x < 0.025f ? -1 : x > 0.975f ? 1 : 0;
            if (dir != edgeDir) { edgeDir = dir; edgeTimer = 0f; }
            if (dir == 0) return;
            edgeTimer += dt;
            if (edgeTimer > 0.35f)
            {
                Turn(dir);
                edgeTimer = -0.6f;
            }
        }

        public IEnumerator Kick(float degrees, float duration)
        {
            float start = pitch;
            yield return Tween.Run(duration, k => pitch = start - Mathf.Sin(k * Mathf.PI) * degrees, Ease.Linear);
        }
    }
}
