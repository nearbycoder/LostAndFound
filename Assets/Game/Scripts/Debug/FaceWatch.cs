using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafFaceWatch (with the AutoPilot): the text audit's check of the speech bubble over a face, made at the end of every
    /// frame instead of only at screenshots, so a bubble passing over a face for a moment (sliding back into place after a
    /// turn, say) is caught too. Each spell is logged as "[FaceWatch] … over the face of …" with how many frames it lasted and
    /// the most of the face it covered; Summary() gives the totals.
    /// </summary>
    public class FaceWatch : MonoBehaviour
    {
        public static string Context = "";
        static int frames, coveredFrames, spells, pictures;
        int spellFrames;
        float spellWorst;
        string spellWho, spellContext;

        public static void Ensure()
        {
            if (Object.FindAnyObjectByType<FaceWatch>() == null) new GameObject("FaceWatch").AddComponent<FaceWatch>();
        }

        void Awake() => DontDestroyOnLoad(gameObject);

        System.Collections.IEnumerator Start()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;   // after everything has moved and the frame is drawn, as a screenshot sees it
                frames++;
                float worst = 0f;
                string who = null;
                TextAudit.CoveredFaces((w, share) => { if (share > worst) { worst = share; who = w; } });
                if (who != null)
                {
                    coveredFrames++;
                    if (spellFrames == 0)
                    {
                        spellWho = who; spellContext = Context; spellWorst = 0f;
                        var dlg = UIRoot.I != null ? UIRoot.I.dialogue : null;
                        Debug.Log($"[FaceWatch] a spell begins after {Context}: over {who} ({worst:P0}), view {CameraRig.I?.view}, bubble {dlg?.State}");
                        if (pictures < 6 && Game.Arg("-lafAutopilot") is string dir)
                        {
                            var tex = ScreenCapture.CaptureScreenshotAsTexture();
                            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"facewatch_{pictures++}_{Context}.jpg"), tex.EncodeToJPG(85));
                            Destroy(tex);
                        }
                    }
                    spellFrames++;
                    if (worst > spellWorst) { spellWorst = worst; spellWho = who; }
                }
                else if (spellFrames > 0) EndSpell();
            }
        }

        void EndSpell()
        {
            spells++;
            string view = CameraRig.I != null ? CameraRig.I.view.ToString() : "?";
            Debug.Log($"[FaceWatch] after {spellContext}: the speech bubble over the face of {spellWho} for {spellFrames} frame(s), at most {spellWorst:P0} of it (view now {view})");
            spellFrames = 0;
        }

        public static void Summary() =>
            Debug.Log($"[FaceWatch] {coveredFrames} of {frames} frames with the speech bubble over a face, in {spells} spell(s); " +
                      $"{DialogueBox.SaidCount} lines said; the bubble vanished to move {DialogueBox.HiddenForSwing} time(s) as the view swung " +
                      $"and {DialogueBox.HiddenForJump} for a jump in its place");
    }
}
