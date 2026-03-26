using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimFPVDrone
{
    public static class DroneModel
    {
        private struct ModelDef
        {
            public string PrefabName;
            public Vector3 PositionOffset;
            public Vector3 RotationOffset;
        }

        // Layer used to hide the drone model from the local FPV camera while
        // keeping it visible to other players' cameras in multiplayer.
        private const int DroneModelLayer = 31;

        private static readonly Dictionary<DroneModelType, ModelDef> Models = new Dictionary<DroneModelType, ModelDef>
        {
            { DroneModelType.Karve, new ModelDef {
                PrefabName = "Karve",
                PositionOffset = Vector3.zero,
                RotationOffset = Vector3.zero
            }},
            { DroneModelType.Deathsquito, new ModelDef {
                PrefabName = "Deathsquito",
                PositionOffset = Vector3.zero,
                RotationOffset = Vector3.zero
            }},
            { DroneModelType.Deer, new ModelDef {
                PrefabName = "Deer",
                PositionOffset = Vector3.zero,
                RotationOffset = Vector3.zero
            }},
            { DroneModelType.Dragon, new ModelDef {
                PrefabName = "Dragon",
                PositionOffset = Vector3.zero,
                RotationOffset = Vector3.zero
            }},
        };

        // Search terms per model type for prefab discovery when name doesn't match
        private static readonly Dictionary<DroneModelType, string[]> SearchTerms = new Dictionary<DroneModelType, string[]>
        {
            { DroneModelType.Deathsquito, new[] { "squito", "mosquito", "death" } },
            { DroneModelType.Deer, new[] { "deer", "eikthyr" } },
            { DroneModelType.Dragon, new[] { "dragon", "moder", "drake" } },
        };

        private static readonly HashSet<Type> KeepTypes = new HashSet<Type>
        {
            typeof(Transform),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
            typeof(LODGroup),
        };

        public static GameObject Attach(GameObject droneObject, DroneModelType modelType)
        {
            if (modelType == DroneModelType.None)
                return null;

            // Player is a special case — clone from the local player
            if (modelType == DroneModelType.Player)
                return AttachPlayerModel(droneObject);

            if (!Models.TryGetValue(modelType, out ModelDef def))
            {
                Plugin.Log.LogWarning($"[FPVDrone] Unknown drone model type: {modelType}");
                return null;
            }

            if (ZNetScene.instance == null)
            {
                Plugin.Log.LogWarning("[FPVDrone] ZNetScene not available, cannot load drone model.");
                return null;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(def.PrefabName);
            if (prefab == null)
            {
                Plugin.Log.LogWarning($"[FPVDrone] Prefab '{def.PrefabName}' not found. Searching for similar names...");
                if (SearchTerms.TryGetValue(modelType, out string[] terms))
                {
                    foreach (var go in ZNetScene.instance.m_prefabs)
                    {
                        string n = go.name.ToLowerInvariant();
                        foreach (var term in terms)
                        {
                            if (n.Contains(term))
                            {
                                Plugin.Log.LogInfo($"[FPVDrone]   candidate: '{go.name}'");
                                break;
                            }
                        }
                    }
                }
                return null;
            }

            // Deactivate the prefab BEFORE instantiating so that the clone starts
            // inactive. This prevents Awake/Start/OnEnable from firing on any
            // MonoBehaviour — no AI, no ZNetView registration, no combat, nothing.
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);

            GameObject model = UnityEngine.Object.Instantiate(prefab);

            // Restore the original prefab immediately
            prefab.SetActive(wasActive);

            // Strip all non-visual components while the clone is still inactive
            StripNonVisualComponents(model);

            // Now safe to activate — only visual components remain
            model.SetActive(true);

            model.name = "DroneVisualModel";
            model.transform.SetParent(droneObject.transform, false);
            model.transform.localPosition = def.PositionOffset;
            model.transform.localRotation = Quaternion.Euler(def.RotationOffset);
            model.transform.localScale = Vector3.one;
            SetLayer(model, DroneModelLayer);

            var rendererCount = model.GetComponentsInChildren<Renderer>(true).Length;
            Plugin.Log.LogInfo($"[FPVDrone] Attached drone model: {modelType} (prefab: {def.PrefabName}, {rendererCount} renderers)");
            return model;
        }

        private static GameObject AttachPlayerModel(GameObject droneObject)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Plugin.Log.LogWarning("[FPVDrone] No local player — cannot create Player drone model.");
                return null;
            }

            // Find the player's visual root (contains the SkinnedMeshRenderers)
            GameObject visual = player.GetComponentInChildren<SkinnedMeshRenderer>(true)?.gameObject;
            if (visual == null)
            {
                Plugin.Log.LogWarning("[FPVDrone] No SkinnedMeshRenderer found on player.");
                return null;
            }

            // Walk up to find the top-level visual container under the player
            Transform visualRoot = visual.transform;
            while (visualRoot.parent != null && visualRoot.parent != player.transform)
                visualRoot = visualRoot.parent;

            // Capture the world-space scale before reparenting — the player hierarchy
            // may have non-unit scale on parent transforms that would be lost.
            Vector3 worldScale = visualRoot.lossyScale;

            // Deactivate before cloning to prevent any Awake/Start on the clone
            bool wasActive = visualRoot.gameObject.activeSelf;
            visualRoot.gameObject.SetActive(false);

            GameObject model = UnityEngine.Object.Instantiate(visualRoot.gameObject);

            visualRoot.gameObject.SetActive(wasActive);

            StripNonVisualComponents(model);
            model.SetActive(true);

            model.name = "DroneVisualModel";
            model.transform.SetParent(droneObject.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = worldScale;
            SetLayer(model, DroneModelLayer);

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Plugin.Log.LogInfo($"[FPVDrone] Attached Player drone model ({renderers.Length} renderers).");
            return model;
        }

        public static void Detach(ref GameObject modelObject)
        {
            if (modelObject != null)
            {
                UnityEngine.Object.DestroyImmediate(modelObject);
                modelObject = null;
            }
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        /// <summary>
        /// Toggles whether the drone model is visible to the local FPV camera.
        /// Call with hide=true in FPV mode to hide the model from the pilot's view
        /// while keeping it rendered for other players.
        /// </summary>
        public static void SetFPVCameraVisibility(Camera camera, bool hide)
        {
            if (camera == null) return;
            int layerBit = 1 << DroneModelLayer;
            if (hide)
                camera.cullingMask &= ~layerBit;
            else
                camera.cullingMask |= layerBit;
        }

        private static void StripNonVisualComponents(GameObject root)
        {
            // First pass: destroy ZNetView FIRST (registers with network, has deps)
            foreach (var znet in root.GetComponentsInChildren<ZNetView>(true))
                UnityEngine.Object.DestroyImmediate(znet);

            // Second pass: destroy Rigidbody (before colliders to avoid warnings)
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(rb);

            // Third pass: destroy ALL MonoBehaviours (AI, combat, movement, everything)
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                UnityEngine.Object.DestroyImmediate(mb);

            // Fourth pass: destroy anything else that isn't purely visual
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (KeepTypes.Contains(comp.GetType())) continue;
                UnityEngine.Object.DestroyImmediate(comp);
            }

            // Force-activate all child GameObjects and renderers
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
        }
    }
}
