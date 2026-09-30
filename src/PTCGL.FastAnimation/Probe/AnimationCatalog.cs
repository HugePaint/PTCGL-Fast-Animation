using System.Collections.Generic;
using System.Reflection;
using System.Text;
using PTCGL.FastAnimation.Config;
using UnityEngine;

namespace PTCGL.FastAnimation.Probe
{
    /// <summary>
    /// Session catalog of presentation entry points and Animator clips. First sighting is logged immediately.
    /// Counts accumulate until the dump hotkey.
    /// </summary>
    internal static class AnimationCatalog
    {
        private const int MaxKeys = 500;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Entries = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> Clips = new Dictionary<string, int>();
        private static readonly Dictionary<int, int> LastClipHash = new Dictionary<int, int>();
        private static bool _capped;

        public static void Reset()
        {
            lock (Gate)
            {
                Entries.Clear();
                Clips.Clear();
                LastClipHash.Clear();
                _capped = false;
            }
        }

        public static void EntryPostfix(MethodBase __originalMethod, object __instance)
        {
            PluginConfig? cfg = Plugin.Cfg;
            if (cfg == null || !cfg.EnableProbe.Value || !cfg.LogEntryPoints.Value)
                return;

            var label = (__originalMethod.DeclaringType?.Name ?? "?") + "." + __originalMethod.Name;
            var scene = "-";
            var path = __instance?.GetType().Name ?? "-";
            if (__instance is Component component)
            {
                var sceneRef = component.gameObject.scene;
                scene = sceneRef.IsValid() ? sceneRef.name : "-";
                path = HierarchyPath(component.transform);
            }

            Note(Entries, "entry", label + " scene=" + scene + " path=" + path);
        }

        public static void SampleAnimators()
        {
            PluginConfig? cfg = Plugin.Cfg;
            if (cfg == null || !cfg.LogAnimatorClips.Value)
                return;

            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || !animator.isActiveAndEnabled)
                    continue;

                var controller = animator.runtimeAnimatorController;
                if (controller == null)
                    continue;

                var clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                if (clipInfos == null || clipInfos.Length == 0 || clipInfos[0].clip == null)
                    continue;

                var clip = clipInfos[0].clip;
                var token = animator.GetInstanceID() ^ (clip.GetInstanceID() * 397);
                lock (Gate)
                {
                    if (LastClipHash.TryGetValue(animator.GetInstanceID(), out var previous) && previous == token)
                        continue;
                    LastClipHash[animator.GetInstanceID()] = token;
                }

                var sceneRef = animator.gameObject.scene;
                var scene = sceneRef.IsValid() ? sceneRef.name : "-";
                var speed = animator.speed.ToString("0.###");
                var key = "scene=" + scene
                    + " ctrl=" + controller.name
                    + " clip=" + clip.name
                    + " speed=" + speed
                    + " path=" + HierarchyPath(animator.transform);
                Note(Clips, "clip", key);
            }
        }

        public static void Dump(string reason)
        {
            var builder = new StringBuilder();
            lock (Gate)
            {
                builder.Append("[Anim] catalog reason=").Append(reason)
                    .Append(" entries=").Append(Entries.Count)
                    .Append(" clips=").Append(Clips.Count);
                Plugin.Log.LogInfo(builder.ToString());
                DumpMap("entry", Entries);
                DumpMap("clip", Clips);
            }
        }

        private static void DumpMap(string kind, Dictionary<string, int> map)
        {
            foreach (var pair in map)
                Plugin.Log.LogInfo("[Anim] " + kind + " x" + pair.Value + " " + pair.Key);
        }

        private static void Note(Dictionary<string, int> map, string kind, string key)
        {
            var first = false;
            lock (Gate)
            {
                if (map.TryGetValue(key, out var count))
                {
                    map[key] = count + 1;
                }
                else if (Entries.Count + Clips.Count >= MaxKeys)
                {
                    if (!_capped)
                    {
                        _capped = true;
                        Plugin.Log.LogWarning("[Anim] catalog cap " + MaxKeys + " reached; further unique keys are dropped.");
                    }
                }
                else
                {
                    map[key] = 1;
                    first = true;
                }
            }

            if (first)
                Plugin.Log.LogInfo("[Anim] " + kind + " " + key);
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            var depth = 0;
            while (transform != null && depth < 8)
            {
                names.Push(transform.name);
                transform = transform.parent;
                depth++;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
