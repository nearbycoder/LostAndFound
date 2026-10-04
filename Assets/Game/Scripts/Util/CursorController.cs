using System.Collections.Generic;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Hardware cursor that changes with context (hand, grab, magnifier, stamp...).
    /// Systems call Want() every frame; the last request wins, otherwise the default arrow.</summary>
    public class CursorController : MonoBehaviour
    {
        static CursorKind wanted = CursorKind.Default;
        static bool wantedThisFrame;
        CursorKind current = (CursorKind)(-1);
        readonly Dictionary<CursorKind, (Texture2D tex, Vector2 hot)> cursors = new();

        public static void Want(CursorKind kind)
        {
            wanted = kind;
            wantedThisFrame = true;
        }

        void Awake()
        {
            Load(CursorKind.Default, "cursor_arrow", new Vector2(6, 4));
            Load(CursorKind.Hand, "cursor_hand", new Vector2(22, 6));
            Load(CursorKind.Grab, "cursor_grab", new Vector2(24, 20));
            Load(CursorKind.Magnifier, "cursor_magnifier", new Vector2(22, 22));
            Load(CursorKind.Stamp, "cursor_hand", new Vector2(22, 6));
            Load(CursorKind.Look, "cursor_look", new Vector2(24, 24));
            Load(CursorKind.Bell, "cursor_hand", new Vector2(22, 6));
        }

        void Load(CursorKind k, string name, Vector2 hot)
        {
            var t = Resources.Load<Texture2D>("Textures/UI/" + name);
            cursors[k] = (t, hot);
        }

        void LateUpdate()
        {
            var k = wantedThisFrame ? wanted : CursorKind.Default;
            wantedThisFrame = false;
            if (k == current) return;
            current = k;
            if (cursors.TryGetValue(k, out var c) && c.tex != null)
                Cursor.SetCursor(c.tex, c.hot, CursorMode.Auto);
            else
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
