using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// Agnes's nudges at the desk: H (or the d-pad's left) during a claim shows the next nudge for wherever the player
    /// has got to (see <see cref="Nudges"/>), and the last one in each stage lights up where to look. After a while
    /// without progress, the hint bar offers one.
    /// </summary>
    public partial class Director
    {
        /// <summary>Seconds on a claim without progress before the hint bar offers a nudge.</summary>
        public const float OfferAfter = 90f;
        /// <summary>Lets the nudge tour see the offer, which hands-free runs otherwise never get.</summary>
        public static bool OfferInTests;

        bool wantedSeen;
        readonly HashSet<string> askedThisCase = new();
        NudgeStage? nudgeStage;
        string nudgeDetail;
        int nudgeLevel;
        float lastProgress;
        bool offering;
        string caseHint;
        readonly List<Renderer[]> nudgeGlows = new();
        DetailDef pointed;

        /// <summary>The nudge on screen, while it still applies.</summary>
        public Nudge ActiveNudge { get; private set; }
        /// <summary>Nudges asked for during the current claim.</summary>
        public int NudgesTaken { get; private set; }
        /// <summary>The hint bar is offering a nudge.</summary>
        public bool OfferingNudge => offering;
        /// <summary>What the last "show me" lit up: "drawer", "item", "part", "glint" or null (the nudge tour checks it).</summary>
        public string ShowingWhere { get; private set; }

        void BeginNudges(CaseDef c)
        {
            wantedSeen = false;
            askedThisCase.Clear();
            nudgeStage = null;
            nudgeDetail = null;
            nudgeLevel = 0;
            NudgesTaken = 0;
            caseHint = string.IsNullOrEmpty(c.hint) ? null : c.hint;
            lastProgress = Time.time;
            offering = false;
            ClearNudge();
        }

        /// <summary>Something happened on the claim: the offer goes, and a nudge that no longer applies is put away.</summary>
        void OnClaimProgress()
        {
            lastProgress = Time.time;
            if (offering)
            {
                offering = false;
                if (phase == Phase.Investigate) UIRoot.I.hint.Set(caseHint);
            }
            if (ActiveNudge == null || Current == null) return;
            var now = CurrentNudges();
            if (now == null || now.Count == 0 || now[0].stage != ActiveNudge.stage || now[0].detailId != ActiveNudge.detailId) ClearNudge();
        }

        NudgeProgress ProgressNow()
        {
            var p = new NudgeProgress { itemSeen = wantedSeen, pad = GamepadInput.Active };
            var item = Db.Object(Current?.wants);
            if (item != null)
            {
                foreach (var d in item.details) if (IsDiscovered(item, d)) p.found.Add(d.id);
                if (Desk.I.OnTray != null && Desk.I.OnTray.def.id == item.id) p.itemSeen = true;
            }
            p.asked.UnionWith(askedThisCase);
            return p;
        }

        /// <summary>The nudges for where the player is on the current claim (null between claims).</summary>
        public List<Nudge> CurrentNudges() => Current == null || phase != Phase.Investigate ? null : Nudges.For(Db, Day, Current, State, ProgressNow());

        /// <summary>Show the next nudge (what H does). Returns it, or null if there's no claim open.</summary>
        public Nudge GiveNudge()
        {
            var list = CurrentNudges();
            if (list == null || list.Count == 0) return null;
            var first = list[0];
            if (nudgeStage != first.stage || nudgeDetail != first.detailId) { nudgeStage = first.stage; nudgeDetail = first.detailId; nudgeLevel = 0; }
            int i = Mathf.Min(nudgeLevel, list.Count - 1);
            nudgeLevel++;
            var n = list[i];
            ActiveNudge = n;
            NudgesTaken++;
            UIRoot.I.nudge.Show(n.text, i + 1, list.Count);
            Debug.Log($"[Nudge] case {Current.id} {n.stage}{(n.detailId != null ? " " + n.detailId : "")} {i + 1}/{list.Count}: {n.text}");
            lastProgress = Time.time;
            if (offering) { offering = false; UIRoot.I.hint.Set(caseHint); }
            return n;
        }

        void ClearNudge()
        {
            ActiveNudge = null;
            UIRoot.I?.nudge.Hide();
            if (InspectController.I != null && InspectController.I.Pointing == pointed) InspectController.I.Pointing = null;
            pointed = null;
            ClearNudgeGlows();
            ShowingWhere = null;
        }

        void Update()
        {
            if (phase != Phase.Investigate || Current == null)
            {
                if (ActiveNudge != null || nudgeGlows.Count > 0) ClearNudge();
                if (InputX.KeyDown(Key.H) && Running && !UIRoot.ModalOpen && phase == Phase.AwaitBell)
                    UIRoot.I.hint.Flash("Agnes's nudges are for when there's a claim at the window.");
                return;
            }
            if (InputX.KeyDown(Key.H) && !UIRoot.ModalOpen && !asking) GiveNudge();

            bool tutorial = Day == 1 && !Save.tutorialDone;
            if (!offering && !tutorial && !asking && Settings.OfferNudges && (!AutoAdvance || OfferInTests) && Time.time - lastProgress > OfferAfter)
            {
                offering = true;
                UIRoot.I.hint.Set(GamepadInput.Prompt("Stuck? Press H for a nudge from Agnes.", "Stuck? Press left on the d-pad for a nudge from Agnes."));
                Debug.Log($"[Nudge] case {Current.id}: offered after {OfferAfter:0}s without progress");
            }
            if (ActiveNudge != null && !UIRoot.I.nudge.Shown && ActiveNudge.point == NudgePoint.None) ActiveNudge = null;
            ShowMe();
        }

        /// <summary>The last nudge of a stage points: the drawer, then the object, or the spot on it.</summary>
        void ShowMe()
        {
            ClearNudgeGlows();
            var insp = InspectController.I;
            var n = ActiveNudge;
            string where = null;
            DetailDef pointing = null;
            if (n != null && n.point != NudgePoint.None && Desk.I.items.Values.FirstOrDefault(i => i != null && i.def.id == Current.wants) is ItemView item)
            {
                var d = n.detailId != null ? item.def.Detail(n.detailId) : null;
                if (insp.Held != item)
                {
                    // first find it: the drawer it's shut in, else the thing itself
                    if (n.point == NudgePoint.Storage && wantedSeen) { }
                    else if (item.drawer != null && !item.drawer.IsOpen && item.place == ItemPlace.Storage) { NudgeGlow(item.drawer.HighlightRenderers); where = "drawer"; }
                    else if (insp.Held == null) { NudgeGlow(item.HighlightRenderers); where = "item"; }
                }
                else if (d != null && !IsDiscovered(item.def, d))
                {
                    var part = item.parts.FirstOrDefault(p => p.def.reveals == d.id);
                    if (part != null) { NudgeGlow(string.IsNullOrEmpty(part.def.node) ? item.HighlightRenderers : part.GetComponentsInChildren<Renderer>()); where = "part"; }
                    else { pointing = d; where = "glint"; }
                }
            }
            // only take back a glint this put up (the nudge tour points at details itself)
            if (insp != null && (pointing != null || insp.Pointing == pointed)) insp.Pointing = pointing;
            pointed = pointing;
            ShowingWhere = where;
        }

        void NudgeGlow(Renderer[] rs)
        {
            if (rs == null) return;
            nudgeGlows.Add(rs);
            if (InteractionSystem.I.Hovered != null && InteractionSystem.I.Hovered.HighlightRenderers == rs) return;
            Highlighter.Set(rs, new Color(0.5f, 0.38f, 0.12f) * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f)));
        }

        void ClearNudgeGlows()
        {
            foreach (var g in nudgeGlows)
                if (InteractionSystem.I == null || InteractionSystem.I.Hovered == null || InteractionSystem.I.Hovered.HighlightRenderers != g)
                    Highlighter.Clear(g);
            nudgeGlows.Clear();
        }
    }
}
