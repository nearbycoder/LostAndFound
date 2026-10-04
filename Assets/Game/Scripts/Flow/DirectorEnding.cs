using System.Collections;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    public partial class Director
    {
        /// <summary>Replay a day from the state it started with.</summary>
        public void ReplayDay(int day)
        {
            var snap = Save.SnapshotFor(day);
            if (snap != null) State = snap;
            Save.state = State;
            StartDay(day);
        }

        public EndingDef ChooseEnding()
        {
            foreach (var e in Db.root.endings)
                if (State.Check(e.condition)) return e;
            return Db.root.endings.LastOrDefault();
        }

        IEnumerator Ending()
        {
            phase = Phase.Evening;
            var e = ChooseEnding();
            Save.ending = e != null ? e.id : "";
            Save.Write();
            yield return EndingView.Show(this, e);
        }
    }
}
