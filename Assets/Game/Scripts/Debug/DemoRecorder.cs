using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace LostAndFound
{
    /// <summary>
    /// -lafDemo &lt;dir&gt;: plays the first case of the week through simulated mouse and keyboard
    /// devices (so every hover, click and drag takes the same path a player's would) and records it.
    /// Game time is locked to 30 fps; each frame is piped to ffmpeg as raw RGBA and the mixed audio
    /// is captured with AudioRenderer, so the video is smooth however slowly the machine renders.
    /// Writes &lt;dir&gt;/video.mp4 and &lt;dir&gt;/audio.f32 (32-bit float, interleaved).
    /// -lafPlay plays whole days the same way instead (see DemoRecorder.Play.cs).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public partial class DemoRecorder : MonoBehaviour
    {
        const int Fps = 30;
        string dir;
        Process ffmpeg;
        Stream pipe;
        RenderTexture grab;
        FileStream audioOut;
        int channels;
        int frames, pending;
        /// <summary>Frames sent to the encoder so far: the frame index of whatever is on screen now.</summary>
        int captured;
        bool recording;

        Mouse mouse;
        Keyboard keyboard;
        Vector2 pos, lastSent;
        bool leftHeld;
        Key keyHeld = Key.None;

        RawImage cursorImage;
        CursorKind cursorKind = (CursorKind)(-1);

        /// <summary>-lafRecordOnly: film whatever plays (the AutoPilot) instead of the scripted first case.
        /// -lafRecordFrom &lt;caseId&gt; waits for that case; filming stops once the photographs have changed.</summary>
        bool recordOnly;
        string recordFrom;
        bool rolling;

        void Awake()
        {
            dir = Game.Arg("-lafDemo");
            Directory.CreateDirectory(dir);
            Time.captureFramerate = Fps;
            Application.runInBackground = true;
            recordOnly = Game.Arg("-lafRecordOnly") != null;
            recordFrom = Game.Arg("-lafRecordFrom");
            if (recordOnly) { Director.Cinematic = true; return; }
            playDays = Game.Arg("-lafPlay") != null;
            if (playDays) Director.Cinematic = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("DemoMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("DemoKeyboard");
            pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.42f);
            BuildCursor();
        }

        void Start()
        {
            channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            audioOut = File.Create(Path.Combine(dir, "audio.f32"));
            AudioRenderer.Start();
            StartFfmpeg();
            recording = true;
            StartCoroutine(Capture());
            if (recordOnly) { StartCoroutine(Watch()); return; }
            rolling = true;
            if (playDays) { StartPlay(); return; }
            StartCoroutine(Advancer());
            StartCoroutine(Script());
        }

        // ------------------------------------------------------------------ recording

        void StartFfmpeg()
        {
            int w = Screen.width, h = Screen.height;
            grab = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            var psi = new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {w}x{h} -r {Fps} -i - " +
                $"-vf \"scale=1920:1080:flags=lanczos,format=yuv420p\" -c:v libx264 -preset medium -crf 17 " +
                $"\"{Path.Combine(dir, "video.mp4")}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            ffmpeg = Process.Start(psi);
            pipe = ffmpeg.StandardInput.BaseStream;
            Debug.Log($"[Demo] recording {w}x{h} at {Fps} fps to {dir} ({AudioSettings.outputSampleRate} Hz x{channels})");
        }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (recording)
            {
                yield return eof;
                int n0 = AudioRenderer.GetSampleCountForCaptureFrame();
                if (!rolling)
                {
                    // not filming yet: keep the audio renderer drained so the take starts in sync
                    if (n0 > 0) { using var skip = new NativeArray<float>(n0 * channels, Allocator.Temp); AudioRenderer.Render(skip); }
                    continue;
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(grab);
                pending++;
                captured++;
                AsyncGPUReadback.Request(grab, 0, TextureFormat.RGBA32, req =>
                {
                    pending--;
                    if (req.hasError || pipe == null) return;
                    var data = req.GetData<byte>();
                    pipe.Write(data.ToArray(), 0, data.Length);
                    frames++;
                });
                int n = n0;
                if (n > 0)
                {
                    using var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                    AudioRenderer.Render(buf);
                    var bytes = new byte[buf.Length * 4];
                    System.Buffer.BlockCopy(buf.ToArray(), 0, bytes, 0, bytes.Length);
                    audioOut.Write(bytes, 0, bytes.Length);
                }
            }
        }

        /// <summary>Record-only: roll from the chosen case until the photographs have changed (or the week ends).</summary>
        IEnumerator Watch()
        {
            while (Director.I == null || Director.I.Current == null || (!string.IsNullOrEmpty(recordFrom) && Director.I.Current.id != recordFrom))
                yield return null;
            rolling = true;
            Debug.Log($"[Demo] rolling from case {Director.I.Current.id}");
            while (!Director.I.ChangingPhotos && !Director.I.Save.finished) yield return null;
            while (Director.I.ChangingPhotos) yield return null;
            yield return new WaitForSeconds(3f);
            yield return UIRoot.I.fader.FadeTo(1f, 1.5f);
            yield return Finish();
        }

        IEnumerator Finish()
        {
            recording = false;
            yield return null;
            AsyncGPUReadback.WaitAllRequests();
            AudioRenderer.Stop();
            audioOut.Close();
            pipe.Close();
            pipe = null;
            ffmpeg.WaitForExit();
            Debug.Log($"[Demo] done: {frames} frames ({frames / (float)Fps:0.0}s), ffmpeg exit {ffmpeg.ExitCode}");
            markers?.Close();
            Application.Quit();
        }

        // ------------------------------------------------------------------ the cursor (hardware cursors aren't in screen captures)

        void BuildCursor()
        {
            var go = new GameObject("DemoCursor");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var img = new GameObject("Cursor").AddComponent<RawImage>();
            img.transform.SetParent(go.transform, false);
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
            cursorImage = img;
        }

        void LateUpdate()
        {
            if (recordOnly) return;
            var k = CursorController.Shown;
            var (tex, hot) = CursorController.Image(k);
            if (k != cursorKind && tex != null)
            {
                cursorKind = k;
                cursorImage.texture = tex;
                cursorImage.rectTransform.sizeDelta = new Vector2(tex.width, tex.height);
                cursorImage.rectTransform.pivot = new Vector2(hot.x / tex.width, 1f - hot.y / tex.height);
            }
            cursorImage.rectTransform.anchoredPosition = mouse.position.ReadValue();
        }

        // ------------------------------------------------------------------ simulated input

        /// <summary>The mouse state the coroutines set up this frame is sent once, and read next frame.</summary>
        void Update()
        {
            if (recordOnly) return;
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            var st = new MouseState { position = pos, delta = pos - lastSent }.WithButton(MouseButton.Left, leftHeld);
            InputSystem.QueueStateEvent(mouse, st);
            lastSent = pos;
        }

        static object Frame() => null;

        static float Smooth(float k) => k * k * (3f - 2f * k);

        /// <summary>Glide to a screen point (re-evaluated every frame, so it can follow a moving camera).</summary>
        IEnumerator Glide(System.Func<Vector2> target, float duration)
        {
            Vector2 from = pos;
            Vector2 bend = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 40f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Smooth(Mathf.Clamp01(t / duration));
                pos = Vector2.Lerp(from, target(), k) + bend * Mathf.Sin(k * Mathf.PI);
                yield return Frame();
            }
            pos = target();
            yield return Frame();
        }

        IEnumerator GlideTo(Vector3 world, float duration) => Glide(() => ToScreen(world), duration);

        /// <summary>Glide onto a spot where the game's own picking would choose this object.</summary>
        IEnumerator GlideTo(Interactable target, float duration)
        {
            yield return Glide(() => AimPoint(target), duration);
            for (int i = 0; i < 20 && InteractionSystem.I.Hovered != target; i++) { pos = AimPoint(target); yield return Frame(); }
            if (InteractionSystem.I.Hovered != target)
                Debug.LogWarning($"[Demo] not hovering {target.name}, hovering {InteractionSystem.I.Hovered?.name}; {InteractionSystem.I.debugState}; modal={UIRoot.ModalOpen} over={UiUnderPointer()}");
        }

        /// <summary>The visible point on an interactable nearest its bounds centre that a ray from the camera lands on.</summary>
        static Vector2 AimPoint(Interactable target)
        {
            var rs = target.HighlightRenderers;
            if (rs == null || rs.Length == 0) return ToScreen(target.transform.position);
            var b = rs[0].bounds;
            foreach (var r in rs) if (r != null) b.Encapsulate(r.bounds);
            Vector2 centre = ToScreen(b.center), best = centre;
            float bestD = float.MaxValue;
            for (int ix = 0; ix <= 6; ix++)
                for (int iy = 0; iy <= 6; iy++)
                    for (int iz = 0; iz <= 2; iz++)
                    {
                        var w = b.min + Vector3.Scale(b.size, new Vector3(0.1f + 0.8f * ix / 6f, 0.1f + 0.8f * iy / 6f, 0.1f + 0.8f * iz / 2f));
                        Vector2 s = ToScreen(w);
                        float d = (s - centre).sqrMagnitude;
                        if (d < bestD && PicksAt(s, target)) { bestD = d; best = s; }
                    }
            return best;
        }

        static string UiUnderPointer()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "no event system";
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            return string.Join(",", hits.Select(h => h.gameObject.name));
        }

        static bool PicksAt(Vector2 screen, Interactable target)
        {
            var ray = Camera.main.ScreenPointToRay(screen);
            foreach (var h in Physics.RaycastAll(ray, 6f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
            {
                if (h.collider.GetComponent<RayBlocker>() != null) return false;
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it == null || !it.Interactive) continue;
                if (it == target) return true;
                if (it is Drawer d && d.IsOpen && target is ItemView iv && iv.drawer == d) continue;
                return false;
            }
            return false;
        }

        static Vector2 ToScreen(Vector3 world)
        {
            var p = Camera.main.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        IEnumerator Hold(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return Frame();
        }

        IEnumerator Click()
        {
            leftHeld = true;
            yield return Hold(0.1f);
            leftHeld = false;
            yield return Hold(0.1f);
        }

        IEnumerator Press(Key k)
        {
            while (keyHeld != Key.None) yield return null;
            keyHeld = k;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            yield return Hold(0.1f);
            keyHeld = Key.None;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Hold(0.1f);
        }

        /// <summary>Drag by an offset (turning a held object).</summary>
        IEnumerator Drag(Vector2 by, float duration)
        {
            leftHeld = true;
            yield return Hold(0.06f);
            Vector2 from = pos;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                pos = from + by * Smooth(Mathf.Clamp01(t / duration));
                yield return Frame();
            }
            pos = from + by;
            yield return Frame();
            leftHeld = false;
            yield return Hold(0.7f);
        }

        IEnumerator Until(System.Func<bool> cond, float timeout = 30f)
        {
            for (float t = 0f; t < timeout && !cond(); t += Time.deltaTime) yield return Frame();
        }

        // ------------------------------------------------------------------ reading pace

        /// <summary>Dialogue and Agnes's notes are advanced with Enter once there's been time to read them.</summary>
        IEnumerator Advancer()
        {
            var d = UIRoot.I.dialogue;
            var note = UIRoot.I.note;
            float typing = 0f, waited = 0f, noteT = 0f;
            while (true)
            {
                if (d.Typing) { typing += Time.deltaTime; waited = 0f; }
                else if (d.Waiting)
                {
                    waited += Time.deltaTime;
                    if (waited > 0.9f + typing * 0.55f && keyHeld == Key.None) { typing = 0f; waited = 0f; yield return Press(Key.Enter); continue; }
                }
                if (note.Open)
                {
                    noteT += Time.deltaTime;
                    if (noteT > 4.2f && keyHeld == Key.None) { noteT = 0f; yield return Press(Key.Enter); continue; }
                }
                else noteT = 0f;
                yield return null;
            }
        }

        bool Talking => UIRoot.I.dialogue.Typing || UIRoot.I.dialogue.Waiting || UIRoot.I.note.Open;

        // ------------------------------------------------------------------ the demo

        IEnumerator Script()
        {
            yield return Frame();
            var director = Director.I;
            var desk = Desk.I;

            // the title: a moment on it, then click "Begin"
            yield return Until(() => TitleScreen.Showing, 10f);
            if (TitleScreen.Showing)
            {
                yield return Hold(2.5f);
                var begin = TitleScreen.FirstButton;
                if (begin != null)
                {
                    // aim at the word itself: a little left of the button's centre (the label is left-aligned)
                    yield return Glide(() => RectTransformUtility.WorldToScreenPoint(null, begin.TransformPoint(begin.rect.center + new Vector2(-begin.rect.width * 0.4f, 0f))), 1.0f);
                    yield return Hold(0.6f);
                    yield return Click();
                }
                else TitleScreen.Begin(Game.I);
            }

            // the morning: day card, Gus, Agnes's note, the first rule
            yield return Until(() => director.CanRing, 90f);
            yield return Hold(0.8f);

            // ring for Walter
            yield return GlideTo(desk.props.bell, 1.1f);
            yield return Hold(0.35f);
            yield return Click();
            yield return Hold(0.3f);
            if (director.CanRing) { Debug.LogWarning("[Demo] bell click missed; ringing directly"); desk.props.bell.Ring(); }
            yield return Until(() => director.CanUseStamps, 60f);
            if (director.Current == null) { Debug.LogError("[Demo] no case started"); yield return Finish(); yield break; }
            yield return Hold(0.6f);

            // read the slip
            yield return GlideTo(ClaimSlip.I.transform.position + new Vector3(0f, 0f, 0.03f), 1.0f);
            yield return Hold(2.6f);
            yield return Glide(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.97f), 0.6f);
            yield return Hold(0.7f);

            // turn to the drawers and open A
            var wallet = desk.items[director.Current.wants];
            var drawer = wallet.drawer;
            yield return Glide(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0.5f);
            yield return Press(Key.A);
            yield return Hold(1.0f);
            yield return GlideTo(drawer, 0.9f);
            yield return Hold(0.3f);
            yield return Click();
            yield return Hold(1.2f);

            // read the tag, pick the wallet up
            yield return GlideTo(wallet, 0.8f);
            yield return Hold(2.2f);
            yield return Click();
            yield return Until(() => InspectController.I.Held == wallet && !InspectController.I.Busy, 5f);
            yield return Hold(0.6f);

            // turn it over and back
            yield return Glide(() => new Vector2(Screen.width * 0.42f, Screen.height * 0.5f), 0.4f);
            yield return Drag(new Vector2(260f, 0f), 0.7f);
            yield return Drag(new Vector2(-260f, 0f), 0.7f);
            yield return Hold(0.6f);

            // open the flap
            var flap = wallet.parts.FirstOrDefault();
            if (flap != null)
            {
                yield return FindPart(wallet, flap);
                yield return Hold(1.4f);
            }

            // look closely: each case detail
            foreach (var d in wallet.def.CaseDetails)
            {
                if (director.IsDiscovered(wallet.def, d)) continue;
                var hs = wallet.Hotspot(d);
                if (hs == null) continue;
                yield return FindDetail(wallet, d, hs);
            }
            yield return Hold(0.8f);

            // onto the tray
            yield return Press(Key.T);
            yield return Until(() => desk.OnTray == wallet && InspectController.I.Held == null, 5f);
            yield return Hold(0.8f);

            // ask about the photograph
            yield return GlideTo(ClaimSlip.I.transform.position + new Vector3(0f, 0f, 0.03f), 0.9f);
            yield return Until(() => ClaimSlip.I.Focused, 3f);
            yield return Hold(1.2f);
            var photo = wallet.def.CaseDetails.FirstOrDefault(x => x.id == "photo");
            if (photo != null && ClaimSlip.I.LinkPosition(photo.id, out _))
            {
                yield return Glide(() => ClaimSlip.I.LinkPosition(photo.id, out var w) ? ToScreen(w) : pos, 0.8f);
                yield return Hold(0.5f);
                yield return Click();
                yield return Hold(0.5f);
                yield return Until(() => director.CanUseStamps && !Talking, 20f);
                yield return Hold(0.6f);
            }

            // stamp it RETURN
            var stamp = desk.props.stamps.First(s => s.kind == Verdict.Return);
            yield return GlideTo(stamp, 0.9f);
            yield return Hold(0.4f);
            yield return Click();
            yield return Hold(0.9f);
            yield return GlideTo(ClaimSlip.I.transform.position + new Vector3(0.02f, 0f, 0.05f), 0.8f);
            yield return Hold(0.5f);
            yield return Click();

            // Walter's thanks, the receipt, the hand-over, the next claimant
            yield return Until(() => director.CanRing, 40f);
            yield return Hold(0.8f);
            yield return GlideTo(desk.props.bell, 1.0f);
            yield return Hold(0.3f);
            yield return Click();
            yield return Until(() => director.CanUseStamps, 60f);
            yield return Hold(2.5f);
            yield return UIRoot.I.fader.FadeTo(1f, 1.2f);
            yield return Hold(0.5f);
            yield return Finish();
        }

        /// <summary>Turn the held object until a part faces us, then click it.</summary>
        IEnumerator FindPart(ItemView item, ItemPart part)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                var r = part.GetComponentInChildren<Renderer>();
                Vector3 c = r != null ? r.bounds.center : part.transform.position;
                if (RayHits(item, ToScreen(c), part.transform))
                {
                    yield return GlideTo(c, 0.7f);
                    yield return Hold(0.5f);
                    yield return Click();
                    yield break;
                }
                yield return TurnSome(tries);
            }
        }

        IEnumerator FindDetail(ItemView item, DetailDef d, Transform hs)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                if (Director.I.IsDiscovered(item.def, d)) yield break;
                if (Facing(item, hs))
                {
                    // sweep in so the glint shows, then click on it
                    yield return Glide(() => ToScreen(hs.position) + new Vector2(70f, -40f), 0.6f);
                    yield return Hold(0.3f);
                    yield return Glide(() => ToScreen(hs.position), 0.5f);
                    yield return Hold(0.45f);
                    yield return Click();
                    yield return Hold(1.8f);
                    if (Director.I.IsDiscovered(item.def, d)) yield break;
                }
                yield return TurnSome(tries);
            }
            Debug.LogWarning($"[Demo] couldn't find {d.id}");
        }

        IEnumerator TurnSome(int i)
        {
            Vector2[] turns = { new(160f, 0f), new(0f, 120f), new(160f, 0f), new(0f, -200f), new(-220f, 60f), new(0f, 160f), new(200f, 0f), new(0f, -120f) };
            yield return Glide(() => new Vector2(Screen.width * 0.44f, Screen.height * 0.45f), 0.35f);
            yield return Drag(turns[i % turns.Length], 0.5f);
        }

        bool Facing(ItemView item, Transform hs)
        {
            var cam = Camera.main;
            Vector3 to = hs.position - cam.transform.position;
            float dist = to.magnitude;
            if (Vector3.Dot(hs.up, -to / dist) < 0.3f) return false;
            Vector2 s = ToScreen(hs.position);
            if (s.x < 60 || s.y < 60 || s.x > Screen.width - 60 || s.y > Screen.height - 60) return false;
            var hits = Physics.RaycastAll(cam.transform.position, to / dist, dist - 0.004f, ~0, QueryTriggerInteraction.Ignore);
            return !hits.Any(h => h.collider.transform.IsChildOf(item.transform));
        }

        static bool RayHits(ItemView item, Vector2 screen, Transform part)
        {
            var ray = Camera.main.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 2f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.transform.IsChildOf(item.transform)).OrderBy(h => h.distance).ToArray();
            return hits.Length > 0 && hits[0].collider.transform.IsChildOf(part);
        }

        void OnDestroy()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        }
    }
}
