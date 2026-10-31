using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RoadSafety.Core;

namespace RoadSafety.World
{
    /// <summary>
    /// The art direction of the reference picture, applied when the level starts (the rules and
    /// the layout of the street do not change):
    ///   - golden hour: low warm sun at the end of the street, pink-peach sky, soft fog, bloom;
    ///   - warm cream walls, deep blue doors, iron window grilles, blue wooden balconies upstairs;
    ///   - a paved stone road and stone pavements instead of plain asphalt.
    /// Built in code like the rest of the level, so nobody has to edit Main.unity.
    /// </summary>
    public static class StreetLook
    {
        private static readonly Color Cream = new Color(0.97f, 0.9f, 0.79f);
        private static readonly Color Blue = new Color(0.1f, 0.3f, 0.72f);
        private static readonly Color DoorBlue = new Color(0.13f, 0.45f, 0.72f);

        public static void Apply()
        {
            GoldenHour();
            PostProcessing();
            Materials();
            WindowsAndBalconies();
            HeroFacades();
            TrafficCars();
            SeaView();
        }

        // ------------------------------------------------------------------ light & sky

        private static void GoldenHour()
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                Light s0 = sun;
                Quaternion rot = sun.transform.rotation; Color col = sun.color; float inten = sun.intensity; LightShadows sh = sun.shadows; float shs = sun.shadowStrength;
                EditorPreviewLog.Record(() => { if (s0 != null) { s0.transform.rotation = rot; s0.color = col; s0.intensity = inten; s0.shadows = sh; s0.shadowStrength = shs; } });
                // low sun ahead, at the end of the street: long shadows towards the child
                sun.transform.rotation = Quaternion.Euler(16f, 195f, 0f);
                sun.color = new Color(1f, 0.74f, 0.52f);
                sun.intensity = 1.5f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.75f;
                RenderSettings.sun = sun;
            }

            {
                Light oldSun = RenderSettings.sun; AmbientMode am = RenderSettings.ambientMode; Color a1 = RenderSettings.ambientSkyColor, a2 = RenderSettings.ambientEquatorColor, a3 = RenderSettings.ambientGroundColor;
                Material sky0 = RenderSettings.skybox; bool fog = RenderSettings.fog; FogMode fm = RenderSettings.fogMode; Color fc = RenderSettings.fogColor; float f0 = RenderSettings.fogStartDistance, f1 = RenderSettings.fogEndDistance;
                EditorPreviewLog.Record(() =>
                {
                    RenderSettings.sun = oldSun; RenderSettings.ambientMode = am; RenderSettings.ambientSkyColor = a1; RenderSettings.ambientEquatorColor = a2; RenderSettings.ambientGroundColor = a3;
                    RenderSettings.skybox = sky0; RenderSettings.fog = fog; RenderSettings.fogMode = fm; RenderSettings.fogColor = fc; RenderSettings.fogStartDistance = f0; RenderSettings.fogEndDistance = f1;
                });
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.98f, 0.75f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.9f, 0.7f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.38f, 0.38f);

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Sky_GoldenHour" };
                sky.SetColor("_SkyTint", new Color(0.55f, 0.52f, 0.7f));
                sky.SetColor("_GroundColor", new Color(0.85f, 0.65f, 0.55f));
                sky.SetFloat("_AtmosphereThickness", 1.25f);
                sky.SetFloat("_Exposure", 1.15f);
                sky.SetFloat("_SunSize", 0.06f);
                RenderSettings.skybox = sky;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.98f, 0.78f, 0.68f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 900f;
            DynamicGI.UpdateEnvironment();
        }

        private static void PostProcessing()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(12f);
            color.colorFilter.Override(new Color(1f, 0.95f, 0.9f));
            var balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(14f);
            balance.tint.Override(6f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.7f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

            var go = new GameObject("GoldenHourVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            Camera cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    bool pp = data.renderPostProcessing; var aa = data.antialiasing;
                    EditorPreviewLog.Record(() => { if (data != null) { data.renderPostProcessing = pp; data.antialiasing = aa; } });
                    data.renderPostProcessing = true;
                    data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
                }
            }
        }

        // ------------------------------------------------------------------ materials

        private static void Materials()
        {
            Texture2D cobbles = Cobblestones(512, 9, new Color(0.72f, 0.6f, 0.52f), new Color(0.42f, 0.34f, 0.31f));
            Texture2D slabs = Cobblestones(256, 4, new Color(0.86f, 0.76f, 0.66f), new Color(0.6f, 0.5f, 0.44f));
            var replaced = new Dictionary<Material, Material>();
            var block = new MaterialPropertyBlock();

            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material m = r.sharedMaterial;
                if (m == null) continue;
                string n = m.name;
                if (n.StartsWith("M_Asphalt"))
                {
                    EditorPreviewLog.SetMaterial(r, Swap(replaced, m, Color.white, cobbles));
                    Tile(r, block, 2.2f);
                }
                else if (n.StartsWith("M_Sidewalk") || n.StartsWith("M_Ground"))
                {
                    EditorPreviewLog.SetMaterial(r, Swap(replaced, m, Color.white, slabs));
                    Tile(r, block, 1.6f);
                }
                else if (n.StartsWith("M_Wall") && !n.StartsWith("M_WallBlueish"))
                    EditorPreviewLog.SetMaterial(r, Swap(replaced, m, Cream, null));
                else if (n.StartsWith("M_Door"))
                    EditorPreviewLog.SetMaterial(r, Swap(replaced, m, DoorBlue, null));
                else if (n.StartsWith("M_Window"))
                    EditorPreviewLog.SetMaterial(r, Swap(replaced, m, new Color(0.16f, 0.36f, 0.62f), null));
            }
        }

        private static Material Swap(Dictionary<Material, Material> cache, Material original, Color color, Texture2D texture)
        {
            if (cache.TryGetValue(original, out Material done)) return done;
            var m = new Material(original) { name = original.name + "_Golden" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (texture != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            cache[original] = m;
            return m;
        }

        /// <summary>Tiles the texture by the object's real size (one tile = metres).</summary>
        private static void Tile(Renderer r, MaterialPropertyBlock block, float metres)
        {
            Vector3 size = r.bounds.size;
            r.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(1f, size.x / metres), Mathf.Max(1f, size.z / metres), 0f, 0f));
            r.SetPropertyBlock(block);
        }

        /// <summary>Rounded paving stones (Voronoi cells), painted in code.</summary>
        private static Texture2D Cobblestones(int size, int cells, Color stone, Color joint)
        {
            var rng = new System.Random(7);
            var points = new Vector2[cells * cells];
            var tints = new float[cells * cells];
            for (int y = 0; y < cells; y++)
                for (int x = 0; x < cells; x++)
                {
                    points[y * cells + x] = new Vector2(x + 0.2f + (float)rng.NextDouble() * 0.6f, y + 0.2f + (float)rng.NextDouble() * 0.6f);
                    tints[y * cells + x] = 0.86f + (float)rng.NextDouble() * 0.22f;
                }
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float u = (px + 0.5f) / size * cells, v = (py + 0.5f) / size * cells;
                    float d1 = 9f, d2 = 9f;
                    int best = 0;
                    int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = (cx + ox + cells) % cells, gy = (cy + oy + cells) % cells;
                            Vector2 p = points[gy * cells + gx] + new Vector2(cx + ox - gx, cy + oy - gy);
                            float d = Vector2.Distance(new Vector2(u, v), p);
                            if (d < d1) { d2 = d1; d1 = d; best = gy * cells + gx; }
                            else if (d < d2) d2 = d;
                        }
                    float edge = Mathf.Clamp01((d2 - d1) * 9f);
                    Color c = Color.Lerp(joint, stone * tints[best], Mathf.SmoothStep(0f, 1f, edge));
                    c.a = 1f;
                    pixels[py * size + px] = c;
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        // ------------------------------------------------------------------ windows & balconies

        /// <summary>Iron grilles on the ground-floor windows, blue wooden balconies on the upper floor.</summary>
        private static void WindowsAndBalconies()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            var iron = new Material(lit) { name = "M_Grille" };
            iron.SetColor("_BaseColor", Blue);
            iron.SetFloat("_Smoothness", 0.3f);
            var wood = new Material(lit) { name = "M_Balcony" };
            wood.SetColor("_BaseColor", new Color(0.09f, 0.27f, 0.68f));
            wood.SetFloat("_Smoothness", 0.2f);
            GameObject cubeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeTemp.SetActive(false);
            Mesh cube = cubeTemp.GetComponent<MeshFilter>().sharedMesh;
            EditorPreviewLog.Kill(cubeTemp);

            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.name != "Window") continue;
                Transform house = r.transform.parent;
                if (house == null || house.Find("WindowFrame") == null) continue; // only the houses of the street
                Vector3 local = r.transform.localPosition;
                var root = new GameObject("Ironwork").transform;
                root.SetParent(house, false);
                root.localPosition = local;

                if (local.y < 3f)
                {
                    // ground floor: iron grille in front of the glass
                    for (int i = 0; i < 5; i++)
                        Bar(cube, iron, root, new Vector3(-0.44f + i * 0.22f, 0f, 0.09f), new Vector3(0.025f, 1.2f, 0.025f));
                    for (int j = 0; j < 3; j++)
                        Bar(cube, iron, root, new Vector3(0f, -0.5f + j * 0.5f, 0.1f), new Vector3(1.0f, 0.025f, 0.025f));
                }
                else
                {
                    // upper floor: a blue wooden balcony with railings and a little roof
                    Bar(cube, wood, root, new Vector3(0f, -0.72f, 0.45f), new Vector3(1.7f, 0.08f, 0.8f));
                    Bar(cube, wood, root, new Vector3(0f, -0.2f, 0.83f), new Vector3(1.7f, 0.06f, 0.06f));
                    for (int i = 0; i < 9; i++)
                        Bar(cube, wood, root, new Vector3(-0.8f + i * 0.2f, -0.46f, 0.83f), new Vector3(0.04f, 0.5f, 0.04f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Bar(cube, wood, root, new Vector3(s * 0.84f, -0.46f, 0.45f), new Vector3(0.04f, 0.5f, 0.8f));
                        Bar(cube, wood, root, new Vector3(s * 0.84f, 0.2f, 0.8f), new Vector3(0.06f, 1.4f, 0.06f));
                    }
                    Bar(cube, wood, root, new Vector3(0f, 0.92f, 0.45f), new Vector3(1.9f, 0.07f, 0.95f));
                }
            }
        }

        private static void Bar(Mesh cube, Material material, Transform parent, Vector3 position, Vector3 scale)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = false;
        }
            // ------------------------------------------------------------------ traffic in the same style

        /// <summary>
        /// The other cars get soft low-poly bodies (Kenney Car Kit, CC0): yellow taxis, a van for the
        /// louage, small family cars. Only the look changes: the cars drive exactly as before.
        /// </summary>
        private static void TrafficCars()
        {
            foreach (RoadSafety.AI.NpcCar car in Object.FindObjectsByType<RoadSafety.AI.NpcCar>(FindObjectsSortMode.None))
            {
                string n = car.name;
                string model = n.Contains("Taxi") ? "taxi" : n.Contains("Louage") ? "van" : n.Contains("Orange") ? "hatchback-sports" : n.Contains("Green") ? "sedan" : "suv";
                var prefab = Resources.Load<GameObject>("Models/Cars/" + model);
                Transform old = car.transform.Find("Model");
                if (prefab == null || old == null) continue;
                var holder = new GameObject("Model_Styled").transform;
                holder.SetParent(car.transform, false);
                GameObject go = Object.Instantiate(prefab, holder, false);
                Bounds b = LocalBounds(holder, go);
                if (b.size.z < 1e-3f) { EditorPreviewLog.Kill(holder.gameObject); continue; }
                float length = n.Contains("Louage") ? 4.9f : 4.1f;
                go.transform.localScale *= length / b.size.z;
                b = LocalBounds(holder, go);
                go.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
                foreach (Renderer r in old.GetComponentsInChildren<Renderer>(true))
                    EditorPreviewLog.SetEnabled(r, false);
            }
        }

        // ------------------------------------------------------------------ the sea at the end of the street

        /// <summary>
        /// Past the old city gate the street opens onto the Mediterranean: a sandy beach, turquoise
        /// water that glitters in the low sun, white-and-blue houses climbing the cliffs on both sides
        /// of the bay, and little boats. Scenery only: nothing here touches the game's rules.
        /// </summary>
        private static void SeaView()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            Material M(string name, Color c, float smooth)
            {
                var m = new Material(lit) { name = name };
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                return m;
            }
            Material sea = M("M_Sea", new Color(0.12f, 0.52f, 0.62f), 0.92f);
            Material seaFar = M("M_SeaFar", new Color(0.2f, 0.42f, 0.6f), 0.85f);
            Material sand = M("M_Sand", new Color(0.93f, 0.82f, 0.62f), 0.1f);
            Material cliff = M("M_Cliff", new Color(0.78f, 0.62f, 0.46f), 0.05f);
            Material green = M("M_Scrub", new Color(0.42f, 0.55f, 0.3f), 0.05f);
            Material white = M("M_WhiteHouse", new Color(0.97f, 0.95f, 0.9f), 0.1f);
            Material blue = M("M_BlueDoor", new Color(0.12f, 0.38f, 0.75f), 0.3f);
            Material hull = M("M_Hull", new Color(0.95f, 0.95f, 0.95f), 0.4f);
            Material hullBlue = M("M_HullBlue", new Color(0.15f, 0.35f, 0.7f), 0.4f);
            Material sail = M("M_Sail", new Color(1f, 0.97f, 0.9f), 0.1f);

            var root = new GameObject("SeaView").transform;
            const float shore = 268f;
            Box(root, "Beach", new Vector3(0f, -0.02f, shore + 12f), new Vector3(700f, 0.2f, 30f), sand);
            Box(root, "Sea", new Vector3(0f, -0.15f, shore + 420f), new Vector3(2400f, 0.1f, 800f), sea);
            Box(root, "SeaFar", new Vector3(0f, -0.16f, shore + 1400f), new Vector3(4000f, 0.1f, 1200f), seaFar);
            root.gameObject.AddComponent<SeaShimmer>().Configure(sea);

            // cliffs on both sides of the bay, covered with little white houses with blue doors
            var rng = new System.Random(11);
            float R() => (float)rng.NextDouble();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(side * 170f, -10f, shore + 260f);
                Vector3 size = new Vector3(260f, 90f, 300f);
                Blob(root, "Cliff", c, size, cliff);
                Blob(root, "Scrub", c + new Vector3(0f, 6f, 0f), size * 0.96f, green);
                for (int i = 0; i < 140; i++)
                {
                    // a point on the upper part of the hill
                    float ax = (R() * 2f - 1f) * 0.85f, az = (R() * 2f - 1f) * 0.85f;
                    if (ax * ax + az * az > 0.72f) continue;
                    float y = Mathf.Sqrt(Mathf.Max(0f, 1f - ax * ax - az * az)) * size.y * 0.5f + c.y + 4f;
                    Vector3 at = new Vector3(c.x + ax * size.x * 0.5f, y, c.z + az * size.z * 0.5f);
                    if (side * at.x < 45f) continue; // keep the view down the street open
                    float w = 5f + R() * 6f, h = 4f + R() * 5f;
                    Quaternion face = Quaternion.LookRotation(new Vector3(-side, 0f, -1f));
                    Transform house = Box(root, "HillHouse", at + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, w * 0.8f), white);
                    house.rotation = face;
                    Transform door = Box(root, "HillDoor", Vector3.zero, new Vector3(w * 0.22f, h * 0.45f, 0.3f), blue);
                    door.SetParent(house, false);
                    door.localPosition = new Vector3(0f, -0.25f, 0.5f);
                    door.localScale = new Vector3(0.22f, 0.45f, 0.05f);
                    if (R() < 0.15f)
                        Blob(root, "HillDome", at + new Vector3(0f, h, 0f), new Vector3(w * 0.6f, w * 0.45f, w * 0.6f), R() < 0.5f ? white : blue);
                }
            }

            // little boats on the water
            for (int i = 0; i < 9; i++)
            {
                Vector3 at = new Vector3((R() * 2f - 1f) * 120f, -0.1f, shore + 50f + R() * 260f);
                var boat = new GameObject("Boat").transform;
                boat.SetParent(root, false);
                boat.position = at;
                boat.rotation = Quaternion.Euler(0f, R() * 360f, 0f);
                Blob(boat, "Hull", at + new Vector3(0f, 0.3f, 0f), new Vector3(1.8f, 0.9f, 5f), R() < 0.5f ? hull : hullBlue).SetParent(boat, true);
                if (R() < 0.6f)
                {
                    Transform mast = Box(root, "Mast", at + new Vector3(0f, 3f, 0f), new Vector3(0.12f, 5f, 0.12f), hull);
                    mast.SetParent(boat, true);
                    Transform s = Box(root, "Sail", at + new Vector3(0f, 3.2f, 0.9f), new Vector3(0.05f, 4f, 1.8f), sail);
                    s.SetParent(boat, true);
                }
                boat.gameObject.AddComponent<Bobbing>();
            }
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material m) => Shape(PrimitiveType.Cube, parent, name, position, size, m);
        private static Transform Blob(Transform parent, string name, Vector3 position, Vector3 size, Material m) => Shape(PrimitiveType.Sphere, parent, name, position, size, m);

        private static Transform Shape(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 size, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            EditorPreviewLog.Kill(go.GetComponent<Collider>()); // scenery: nothing to bump into
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ real Sidi Bou Said pieces

        /// <summary>
        /// On the houses closest to where the child starts, the generated door and upper windows are
        /// replaced by real models of Sidi Bou Said (Sketchfab, CC-BY: see CREDITS.md). Only a few,
        /// because these models are detailed and the game must stay smooth on a tablet.
        /// </summary>
        private static void HeroFacades()
        {
            var door = Resources.Load<GameObject>("Models/rs_door");
            var window = Resources.Load<GameObject>("Models/rs_window");
            var mashrabiya = Resources.Load<GameObject>("Models/rs_mashrabiya");
            if (door == null && window == null && mashrabiya == null) return;

            const float FromZ = -60f, ToZ = 70f;
            int doors = 0, windows = 0;
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Transform house = r.transform.parent;
                if (house == null || house.Find("WindowFrame") == null) continue;
                float z = r.transform.position.z;
                if (z < FromZ || z > ToZ || Mathf.Abs(r.transform.position.x) > 16f) continue;

                if (r.name == "Door" && door != null && doors < 8)
                {
                    Vector3 size = r.transform.localScale;
                    if (Place(door, house, r.transform.localPosition + new Vector3(0f, 0.15f, 0.06f), size.y + 0.6f))
                    {
                        Hide(house, r.transform.localPosition, 1.6f);
                        doors++;
                    }
                }
                else if (r.name == "Window" && windows < 14 && r.transform.localPosition.y > 3f)
                {
                    GameObject pick = (windows % 2 == 0 ? window : mashrabiya) ?? window ?? mashrabiya;
                    if (Place(pick, house, r.transform.localPosition + new Vector3(0f, 0f, 0.1f), 2.0f))
                    {
                        Transform iron = FindIronwork(house, r.transform.localPosition);
                        if (iron != null) iron.gameObject.SetActive(false);
                        windows++;
                    }
                }
            }
        }

        /// <summary>Instantiates a model on a house front, scaled to a height, its thin side against the wall.</summary>
        private static bool Place(GameObject prefab, Transform house, Vector3 localPosition, float height)
        {
            if (prefab == null) return false;
            var holder = new GameObject(prefab.name + "_Placed").transform;
            holder.SetParent(house, false);
            holder.localPosition = localPosition;
            GameObject go = Object.Instantiate(prefab, holder, false);

            Bounds b = LocalBounds(holder, go);
            if (b.size.sqrMagnitude < 1e-8f) { EditorPreviewLog.Kill(holder.gameObject); return false; }
            // the thinnest horizontal side is the depth: turn it to face the street (+Z of the house)
            if (b.size.x < b.size.z)
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            b = LocalBounds(holder, go);
            float scale = height / Mathf.Max(1e-4f, b.size.y);
            go.transform.localScale *= scale;
            b = LocalBounds(holder, go);
            go.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.min.z);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
            return true;
        }

        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            bool has = false;
            var b = new Bounds();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Bounds wb = r.bounds;
                Vector3 c = wb.center, e = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = space.InverseTransformPoint(corner);
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Hides the generated door parts around a point of the house (door, arch, frames).</summary>
        private static void Hide(Transform house, Vector3 at, float radius)
        {
            foreach (Transform child in house)
            {
                string n = child.name;
                if ((n.StartsWith("Door") || n.StartsWith("DoorArch")) && Vector2.Distance(new Vector2(child.localPosition.x, 0f), new Vector2(at.x, 0f)) < radius)
                {
                    var r = child.GetComponent<Renderer>();
                    if (r != null) EditorPreviewLog.SetEnabled(r, false);
                }
            }
        }

        private static Transform FindIronwork(Transform house, Vector3 at)
        {
            foreach (Transform child in house)
                if (child.name == "Ironwork" && (child.localPosition - at).sqrMagnitude < 0.01f)
                    return child;
            return null;
        }
    }


    /// <summary>The sea glitters: its colour and shine breathe slowly in the low sun.</summary>
    public class SeaShimmer : MonoBehaviour
    {
        private Material sea;
        private Color baseColor;
        public void Configure(Material m) { sea = m; baseColor = m.GetColor("_BaseColor"); }
        private void Update()
        {
            if (sea == null) return;
            float t = Time.time;
            sea.SetFloat("_Smoothness", 0.88f + Mathf.Sin(t * 1.3f) * 0.05f);
            sea.SetColor("_BaseColor", baseColor * (1f + Mathf.Sin(t * 0.7f) * 0.04f));
        }
    }

    /// <summary>A boat rocks gently on the water.</summary>
    public class Bobbing : MonoBehaviour
    {
        private Vector3 start;
        private float phase;
        private void Start() { start = transform.position; phase = Random.value * 10f; }
        private void Update()
        {
            float t = Time.time + phase;
            transform.position = start + new Vector3(0f, Mathf.Sin(t * 1.1f) * 0.15f, 0f);
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 3f, transform.eulerAngles.y, Mathf.Sin(t * 1.3f) * 4f);
        }
    }
}
