using System.Collections.Generic;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Loads Blender FBX models from Resources/Models and swaps in library materials.</summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> Prefabs = new();

        public static GameObject Prefab(string path)
        {
            if (Prefabs.TryGetValue(path, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + path);
            Prefabs[path] = p;
            return p;
        }

        public static bool Exists(string path) => Prefab(path) != null;

        public static GameObject Spawn(string path, Transform parent = null)
        {
            var prefab = Prefab(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ModelLibrary] missing model {path}");
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = path + " (missing)";
                g.transform.localScale = Vector3.one * 0.1f;
                Object.Destroy(g.GetComponent<Collider>());
                if (parent != null) g.transform.SetParent(parent, false);
                return g;
            }
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            MaterialLibrary.Apply(go);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = Find(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        public static IEnumerable<Transform> Walk(Transform root)
        {
            yield return root;
            for (int i = 0; i < root.childCount; i++)
                foreach (var t in Walk(root.GetChild(i)))
                    yield return t;
        }

        /// <summary>World-space bounds of all renderers under root.</summary>
        public static Bounds Bounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.05f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Add mesh colliders to every mesh under root (for precise raycasts).</summary>
        public static void AddMeshColliders(GameObject root, bool convex = false)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = convex;
            }
        }
    }
}
