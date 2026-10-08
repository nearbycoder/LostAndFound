using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// -lafEdgeTest: holds a virtual mouse at each side of the screen in the counter view and checks when the desk turns.
    /// With Settings › Turn at the screen's edge on it turns (left to the drawers, right to the shelf); off, it doesn't; and
    /// while another window has the focus it doesn't either (Tools/unity.sh edgetest puts one over the game, as LAF_STEAL
    /// does). Logs "[EdgeTest] PASS" when every hold did what it should. Run it inside the headless KWin only.
    /// </summary>
    public class EdgeTest : MonoBehaviour
    {
        Mouse mouse;
        readonly List<string> failed = new();
        int checks;

        void Start()
        {
            Application.runInBackground = true;
            mouse = InputSystem.AddDevice<Mouse>("EdgeMouse");
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        }

        void Point(Vector2 p)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p });
        }

        static IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Centre()
        {
            Point(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return new WaitForSecondsRealtime(0.2f);
            Point(new Vector2(Screen.width * 0.5f + 40f, Screen.height * 0.5f));   // it has moved: the pointer counts from here
            yield return new WaitForSecondsRealtime(0.3f);
            if (CameraRig.I.view != View.Counter) { CameraRig.I.SetView(View.Counter); yield return new WaitForSecondsRealtime(0.6f); }
        }

        /// <summary>Hold the pointer at one side for 1.5 s (a turn needs 0.35 s there) and see whether the desk turned.</summary>
        IEnumerator Hold(int side, bool expectTurn, string what)
        {
            yield return Centre();
            var before = CameraRig.I.view;
            // slowing to a stop just inside the side, as a person does: a pointer whose last step would carry it out of the
            // window is taken to have left it (CameraRig.PointerLeft)
            foreach (float inset in new[] { 40f, 20f, 10f, 6f, 4f, 3f })
            {
                Point(new Vector2(side < 0 ? inset : Screen.width - inset, Screen.height * 0.5f));
                yield return null;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            var after = CameraRig.I.view;
            bool turned = after != before;
            checks++;
            string line = $"{what}: pointer at the {(side < 0 ? "left" : "right")} edge for 1.5 s, view {before} -> {after} (window focused {Application.isFocused}, edge turning {(Settings.EdgeTurn ? "on" : "off")})";
            if (turned == expectTurn) Debug.Log($"[EdgeTest] ok  {line}");
            else { Debug.Log($"[EdgeTest] FAIL {line}: expected {(expectTurn ? "a turn" : "no turn")}"); failed.Add(what); }
            yield return Centre();
        }

        IEnumerator Run()
        {
            var d = Director.I;
            // past the morning, to the bell: the counter view, nothing open, free to turn
            d.autoAdvance = true;
            yield return Until(() => d.CanRing && !UIRoot.ModalOpen && Application.isFocused, 90f);
            if (!d.CanRing) { Debug.Log("[EdgeTest] FAIL: never reached the bell"); Application.Quit(); yield break; }
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[EdgeTest] at the bell, {Screen.width}x{Screen.height}, view {CameraRig.I.view}");

            bool was = Settings.EdgeTurn;
            Settings.EdgeTurn = true;
            yield return Hold(-1, true, "on");
            yield return Hold(1, true, "on");
            Settings.EdgeTurn = false;
            yield return Hold(-1, false, "off");
            yield return Hold(1, false, "off");
            Settings.EdgeTurn = true;

            // another window takes the focus (Tools/unity.sh edgetest opens one): the pointer left at the side mustn't turn the desk
            Debug.Log("[EdgeTest] waiting for another window to take the focus");
            yield return Until(() => !Application.isFocused, 40f);
            if (Application.isFocused) { Debug.Log("[EdgeTest] FAIL: no other window took the focus (run it with Tools/unity.sh edgetest)"); failed.Add("focus"); }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Hold(-1, false, "out of focus");
                yield return Hold(1, false, "out of focus");
                yield return Until(() => Application.isFocused, 40f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Hold(-1, true, "focus back");
            }
            Settings.EdgeTurn = was;
            Debug.Log(failed.Count == 0 ? $"[EdgeTest] PASS ({checks} holds)" : $"[EdgeTest] FAIL: {string.Join(", ", failed)}");
            Application.Quit();
        }
    }
}
