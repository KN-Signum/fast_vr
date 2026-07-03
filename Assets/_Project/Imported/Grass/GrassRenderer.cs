using UnityEngine;

[ExecuteAlways]
public class GrassRenderer : MonoBehaviour
{
    [Tooltip("Optional custom mesh. Leave empty to use the procedural blade controlled by Width/Height/Bend.")]
    public Mesh bladeMesh;
    public Material grassMaterial;
    public Terrain terrain;
    [Tooltip("If set, blades only spawn where this terrain layer's splat weight exceeds the threshold. Takes precedence over Placement Mask.")]
    public TerrainLayer grassLayer;
    [Tooltip("Fallback / override mask sampled in terrain UV space. Used only when Grass Layer is not assigned. Requires Read/Write enabled on the texture.")]
    public Texture2D placementMask;
    public int density = 50000;
    [Range(0f, 1f)] public float maskThreshold = 0.5f;
    public int randomSeed = 0;
    [Tooltip("Width of the blade base in meters. Mid-height is 80% of this, tip tapers to 0.")]
    [Range(0.005f, 0.1f)] public float bladeWidth = 0.03f;
    [Tooltip("Height of the blade in meters before per-instance random scaling (0.7–1.3x).")]
    [Range(0.05f, 2f)] public float bladeHeight = 0.5f;
    [Tooltip("How far the tip leans forward (model Z) in meters. Random per-blade Y rotation spreads the lean direction in world space.")]
    [Range(0f, 0.5f)] public float bladeBend = 0.08f;
    [Tooltip("Per-blade Y scale random range (x = min, y = max). Wider range = more height variation.")]
    public Vector2 heightVariation = new Vector2(0.5f, 1.6f);
    [Tooltip("Per-blade XZ scale random range (x = min, y = max). Keep narrow — wildly varying widths look noisy.")]
    public Vector2 widthVariation = new Vector2(0.85f, 1.15f);
    [Tooltip("How much the splatmap weight at each blade contributes to its height. 0 = ignore weight, 1 = blade at threshold weight is fully short, blade at weight 1 is full height.")]
    [Range(0f, 1f)] public float heightFromWeight = 0.6f;

    [Header("Patch Height Variation")]
    [Tooltip("Approximate diameter (meters) of one tall/short patch. Larger = broader rolling fields, smaller = tighter clumps.")]
    [Range(0.5f, 30f)] public float patchScale = 5f;
    [Tooltip("How much patch noise pulls a blade down from full height. 0 = uniform, 1 = patch low spots reach zero height.")]
    [Range(0f, 1f)] public float patchStrength = 0.5f;
    [Tooltip("Optional second octave for finer clumps inside the big patches. 0 = single octave.")]
    [Range(0f, 1f)] public float patchDetail = 0.35f;
    [Tooltip("World-space offset for the patch noise, lets you reseed the patch layout without changing per-blade randomness.")]
    public Vector2 patchOffset = Vector2.zero;

    [Header("Edge Falloff")]
    [Tooltip("How strongly blades near the mask edge get shortened. 0 = hard edge, 1 = full taper to zero at the threshold boundary.")]
    [Range(0f, 1f)] public float edgeFalloffStrength = 0.85f;
    [Tooltip("Sampling radius (meters) used to measure proximity to the mask edge. Larger = wider, softer transition.")]
    [Range(0.05f, 5f)] public float edgeFalloffRadius = 0.6f;
    [Tooltip("Number of mask taps around each blade (in addition to the center). More = smoother edge but slower build.")]
    [Range(2, 12)] public int edgeFalloffSamples = 6;

    private ComputeBuffer argsBuffer;
    private ComputeBuffer matrixBuffer;
    private Bounds bounds;
    private int instanceCount;
    [System.NonSerialized] private Mesh generatedMesh;
    [System.NonSerialized] private Mesh activeMesh;

    void OnEnable()
    {
        Build();
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    void Update()
    {
        if (grassMaterial == null || activeMesh == null || argsBuffer == null) return;
        Graphics.DrawMeshInstancedIndirect(activeMesh, 0, grassMaterial, bounds, argsBuffer);
    }

    void Build()
    {
        ReleaseBuffers();

        if (terrain == null || grassMaterial == null) return;
        if (grassLayer == null && placementMask == null) return;
        if (bladeMesh != null)
        {
            activeMesh = bladeMesh;
        }
        else
        {
            if (generatedMesh != null) DestroyImmediate(generatedMesh);
            generatedMesh = CreateGrassBlade(bladeWidth, bladeHeight, bladeBend);
            activeMesh = generatedMesh;
        }

        Random.InitState(randomSeed);

        var terrainData = terrain.terrainData;
        var terrainSize = terrainData.size;
        var terrainPos = terrain.transform.position;

        float[,,] alphamaps = null;
        int alphaW = 0, alphaH = 0, layerIndex = -1;
        if (grassLayer != null)
        {
            var layers = terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == grassLayer) { layerIndex = i; break; }
            }
            if (layerIndex < 0)
            {
                Debug.LogWarning($"[GrassRenderer] '{grassLayer.name}' is not a layer on terrain '{terrain.name}'. No grass spawned.", this);
                return;
            }
            alphaW = terrainData.alphamapWidth;
            alphaH = terrainData.alphamapHeight;
            alphamaps = terrainData.GetAlphamaps(0, 0, alphaW, alphaH);
        }

        var matrices = new Matrix4x4[density];
        int count = 0;

        // UV-space radius for edge sampling — convert meters to terrain UV per axis.
        float edgeUVRadiusU = edgeFalloffRadius / Mathf.Max(0.01f, terrainSize.x);
        float edgeUVRadiusV = edgeFalloffRadius / Mathf.Max(0.01f, terrainSize.z);
        // Inverse of patch scale in world units; guarded so the inspector slider can't divide by zero.
        float patchFreq = 1f / Mathf.Max(0.01f, patchScale);

        // Try up to 2x density attempts to hit target count after mask rejection.
        for (int i = 0; i < density * 2 && count < density; i++)
        {
            float u = Random.value;
            float v = Random.value;

            float weight = SampleWeight(alphamaps, placementMask, u, v, alphaW, alphaH, layerIndex);
            if (weight < maskThreshold) continue;

            Vector3 worldPos = terrainPos + new Vector3(u * terrainSize.x, 0f, v * terrainSize.z);
            worldPos.y = terrain.SampleHeight(worldPos) + terrainPos.y;

            float xzScale = Random.Range(widthVariation.x, widthVariation.y);
            float yScale = Random.Range(heightVariation.x, heightVariation.y);
            yScale *= Mathf.Lerp(1f, weight, heightFromWeight);

            // Patch noise: low-freq Perlin in [0,1], optionally mixed with a higher-freq octave.
            float patch = Mathf.PerlinNoise(
                (worldPos.x + patchOffset.x) * patchFreq,
                (worldPos.z + patchOffset.y) * patchFreq);
            if (patchDetail > 0f)
            {
                float detail = Mathf.PerlinNoise(
                    (worldPos.x + patchOffset.x) * patchFreq * 3.17f + 41.3f,
                    (worldPos.z + patchOffset.y) * patchFreq * 3.17f - 17.7f);
                patch = Mathf.Lerp(patch, detail, patchDetail);
            }
            yScale *= Mathf.Lerp(1f - patchStrength, 1f, patch);

            // Edge falloff: average mask weight in a small ring; remap above threshold; taper height.
            if (edgeFalloffStrength > 0f)
            {
                float blurred = SampleWeightBlurred(alphamaps, placementMask, u, v, alphaW, alphaH, layerIndex,
                    edgeUVRadiusU, edgeUVRadiusV, edgeFalloffSamples);
                float denom = Mathf.Max(1e-4f, 1f - maskThreshold);
                float edgeFactor = Mathf.Clamp01((blurred - maskThreshold) / denom);
                yScale *= Mathf.Lerp(1f, edgeFactor, edgeFalloffStrength);
            }

            float rot = Random.Range(0f, 360f);
            // Z tied to Y so the model-space bend (forward lean) scales with height, not width.
            matrices[count++] = Matrix4x4.TRS(worldPos, Quaternion.Euler(0f, rot, 0f), new Vector3(xzScale, yScale, yScale));
        }

        instanceCount = count;
        if (count == 0) return;

        matrixBuffer = new ComputeBuffer(count, sizeof(float) * 16);
        matrixBuffer.SetData(matrices, 0, 0, count);
        grassMaterial.SetBuffer("_Matrices", matrixBuffer);

        var args = new uint[5] { activeMesh.GetIndexCount(0), (uint)count, 0u, 0u, 0u };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        bounds = new Bounds(terrainPos + terrainSize * 0.5f, terrainSize);
    }

    void ReleaseBuffers()
    {
        argsBuffer?.Release();
        argsBuffer = null;
        matrixBuffer?.Release();
        matrixBuffer = null;
        if (generatedMesh != null)
        {
            DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
        activeMesh = null;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild() => Build();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        // Defer so the inspector finishes serializing before we rebuild.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Build();
        };
    }
#endif

    static float SampleWeight(float[,,] maps, Texture2D mask, float u, float v, int w, int h, int layer)
    {
        if (maps != null) return SampleAlphamapBilinear(maps, u, v, w, h, layer);
        return mask.GetPixelBilinear(u, v).r;
    }

    // Approximates a small box blur of the mask by averaging the center with a ring of taps.
    // Used to detect distance-to-edge: blades near the boundary read a lower average.
    static float SampleWeightBlurred(float[,,] maps, Texture2D mask, float u, float v, int w, int h, int layer,
        float radiusU, float radiusV, int ringSamples)
    {
        float sum = SampleWeight(maps, mask, u, v, w, h, layer);
        int n = Mathf.Max(2, ringSamples);
        float step = Mathf.PI * 2f / n;
        for (int k = 0; k < n; k++)
        {
            float a = step * k;
            float su = Mathf.Clamp01(u + Mathf.Cos(a) * radiusU);
            float sv = Mathf.Clamp01(v + Mathf.Sin(a) * radiusV);
            sum += SampleWeight(maps, mask, su, sv, w, h, layer);
        }
        return sum / (n + 1);
    }

    // Alphamap layout is [y, x, layer]; y=0 is the south (-Z) edge, matching terrain UV.v=0.
    static float SampleAlphamapBilinear(float[,,] maps, float u, float v, int w, int h, int layer)
    {
        float x = Mathf.Clamp01(u) * (w - 1);
        float y = Mathf.Clamp01(v) * (h - 1);
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Mathf.Min(x0 + 1, w - 1);
        int y1 = Mathf.Min(y0 + 1, h - 1);
        float fx = x - x0;
        float fy = y - y0;
        float a = maps[y0, x0, layer];
        float b = maps[y0, x1, layer];
        float c = maps[y1, x0, layer];
        float d = maps[y1, x1, layer];
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static Mesh CreateGrassBlade(float width, float height, float bend)
    {
        float halfBase = width * 0.5f;
        float halfMid = width * 0.4f;
        float midY = height * 0.5f;
        // Quadratic curve: root flat, tip leans full `bend`.
        float midZ = bend * 0.25f;
        float tipZ = bend;

        var mesh = new Mesh { name = "GrassBlade" };
        mesh.vertices = new[]
        {
            new Vector3(-halfBase, 0f,     0f),
            new Vector3( halfBase, 0f,     0f),
            new Vector3(-halfMid,  midY,   midZ),
            new Vector3( halfMid,  midY,   midZ),
            new Vector3( 0f,       height, tipZ),
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3 };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 1f),
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
