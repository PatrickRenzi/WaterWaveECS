using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

public class WaveGenerator : MonoBehaviour
{
    public MeshFilter[] WaterMeshFilters;
    public Octave[] Octaves;
    public float VertsPerUnit = 10;
    
    private Mesh[] waterMeshes;
    private int2[] Dimensions;
    private float2[] Ratios;
    private Matrix4x4[] TransformMatrices;
    private int[] MeshIndexLimits;
    private float UVScale = 1;

    //Native arrays for building out Job
    NativeArray<Vector3> waterVertices;
    NativeArray<Vector3> waterNormals;
    NativeArray<Vector3> anchorVertices;
    NativeArray<int2> dimensions;
    NativeArray<float2> ratios;
    NativeArray<int> meshIndexLimits;
    NativeArray<Matrix4x4> transformMatrices;
    NativeArray<Octave> octaves;

    JobHandle meshModificationJobHandle;
    UpdateMeshJob meshModificationJob;
    

    // Start is called before the first frame update
    void Start()
    {

        var allVertices = new List<Vector3>();
        var allNormals = new List<Vector3>();
        waterMeshes = new Mesh[WaterMeshFilters.Length];
        Dimensions = new int2[WaterMeshFilters.Length];
        Ratios = new float2[WaterMeshFilters.Length];
        MeshIndexLimits = new int[WaterMeshFilters.Length];
        TransformMatrices = new Matrix4x4[WaterMeshFilters.Length];

        UVScale = 2 * VertsPerUnit * 5;

        for (int i = 0; i < WaterMeshFilters.Length; i++)
        {
            waterMeshes[i] = WaterMeshFilters[i].mesh;
            waterMeshes[i].Clear();
            waterMeshes[i].MarkDynamic();
            waterMeshes[i].vertices = GenerateVertsBake(i);
            waterMeshes[i].triangles = GenerateTries(i);
            waterMeshes[i].uv = GenerateUVs(i);
            waterMeshes[i].RecalculateNormals();
            waterMeshes[i].RecalculateBounds();
            waterMeshes[i].RecalculateTangents();
            MeshIndexLimits[i] = waterMeshes[i].vertices.Length;
            TransformMatrices[i] = WaterMeshFilters[i].transform.localToWorldMatrix;
            Ratios[i] = new float2(1f/WaterMeshFilters[i].transform.localScale.x, 1f / WaterMeshFilters[i].transform.localScale.z);
        }

        for (int i = 0; i < waterMeshes.Length; i++)
        {
            allVertices.AddRange(waterMeshes[i].vertices);
            allNormals.AddRange(waterMeshes[i].normals);
        }

        octaves = new NativeArray<Octave>(Octaves, Allocator.Persistent);
        waterVertices = new NativeArray<Vector3>(allVertices.ToArray(), Allocator.Persistent);
        anchorVertices = new NativeArray<Vector3>(allVertices.ToArray(), Allocator.Persistent);
        waterNormals = new NativeArray<Vector3>(allNormals.ToArray(), Allocator.Persistent);
        dimensions = new NativeArray<int2>(Dimensions, Allocator.Persistent);
        ratios = new NativeArray<float2>(Ratios, Allocator.Persistent);
        meshIndexLimits = new NativeArray<int>(MeshIndexLimits, Allocator.Persistent);
        transformMatrices = new NativeArray<Matrix4x4>(TransformMatrices, Allocator.Persistent);
    }

    // Update is called once per frame
    void Update()
    {
        meshModificationJob = new UpdateMeshJob()
        {
            vertices = waterVertices,
            normals = waterNormals,
            time = Time.time,
            octaves = octaves,
            dimensions = dimensions,
            anchorVerts = anchorVertices,
            meshIndexLimits = meshIndexLimits,
            transformMatrices = transformMatrices,
            ratios = ratios
        };

        meshModificationJobHandle =
            meshModificationJob.Schedule(waterVertices.Length, 64);
    }

    private void LateUpdate()
    {
        meshModificationJobHandle.Complete();

        int indexStart = 0;

        for (int i = 0; i < waterMeshes.Length; i++)
        {
            waterMeshes[i].SetVertices(meshModificationJob.vertices, indexStart, MeshIndexLimits[i]);

            waterMeshes[i].RecalculateNormals();

            indexStart += MeshIndexLimits[i];
        }
    }

    private void OnDestroy()
    {
        anchorVertices.Dispose();
        dimensions.Dispose();
        waterVertices.Dispose();
        waterNormals.Dispose();
        meshIndexLimits.Dispose();
        transformMatrices.Dispose();
        octaves.Dispose();
        ratios.Dispose();
    }

    private int index(int x, int z, int z_Dimension)
    {
        return x * (z_Dimension + 1) + z;
    }

    private Vector3[] GenerateVertsBake(int i)
    {

        int vertsInInt = (int)VertsPerUnit;
        var X_Dimension = (Mathf.RoundToInt(waterMeshes[i].bounds.extents.x * 2 * WaterMeshFilters[i].transform.localScale.x)) * vertsInInt;
        var Z_Dimension = (Mathf.RoundToInt(waterMeshes[i].bounds.extents.z * 2 * WaterMeshFilters[i].transform.localScale.z)) * vertsInInt;
        Dimensions[i] = new int2(X_Dimension, Z_Dimension);    

        var verts = new Vector3[(X_Dimension + 1) * (Z_Dimension + 1)];

        float x_position = waterMeshes[i].bounds.center.x - (waterMeshes[i].bounds.extents.x * WaterMeshFilters[i].transform.localScale.x);
        float z_position = waterMeshes[i].bounds.center.z - (waterMeshes[i].bounds.extents.z * WaterMeshFilters[i].transform.localScale.z);

        WaterMeshFilters[i].transform.localScale = new Vector3(1f, 1f, 1f);

        //equaly distributed verts
        for (int x = 0; x <= X_Dimension; x++)
        {
            for (int z = 0; z <= Z_Dimension; z++)
            {
                verts[index(x, z, Z_Dimension)] = new Vector3(x_position + (x * (1f / vertsInInt)), 0, z_position + (z * (1f / vertsInInt)));
            }
        }

        return verts;
    }

    private int[] GenerateTries(int i)
    {
        var tries = new int[waterMeshes[i].vertices.Length * 6];

        //two triangles are one tile
        for (int x = 0; x < Dimensions[i].x; x++)
        {
            for (int z = 0; z < Dimensions[i].y; z++)
            {
                tries[index(x, z, Dimensions[i].y) * 6 + 0] = index(x, z, Dimensions[i].y);
                tries[index(x, z, Dimensions[i].y) * 6 + 1] = index(x + 1, z + 1, Dimensions[i].y);
                tries[index(x, z, Dimensions[i].y) * 6 + 2] = index(x + 1, z, Dimensions[i].y);
                tries[index(x, z, Dimensions[i].y) * 6 + 3] = index(x, z, Dimensions[i].y);
                tries[index(x, z, Dimensions[i].y) * 6 + 4] = index(x, z + 1, Dimensions[i].y);
                tries[index(x, z, Dimensions[i].y) * 6 + 5] = index(x + 1, z + 1, Dimensions[i].y);
            }
        }

        return tries;
    }

    private Vector2[] GenerateUVs(int i)
    {
        var uvs = new Vector2[waterMeshes[i].vertices.Length];

        for (int x = 0; x <= Dimensions[i].x; x++)
        {
            for (int z = 0; z <= Dimensions[i].y; z++)
            {
                var vec = new Vector2((x / UVScale) % 2, (z / UVScale) % 2);
                uvs[index(x, z, Dimensions[i].y)] = new Vector2(vec.x <= 1 ? vec.x : 2 - vec.x, vec.y <= 1 ? vec.y : 2 - vec.y);
            }
        }

        return uvs;
    }

    [BurstCompile]
    private struct UpdateMeshJob : IJobParallelFor
    {
        public NativeArray<Vector3> vertices;

        [ReadOnly]
        public NativeArray<Vector3> normals;
        [ReadOnly]
        public NativeArray<int2> dimensions;
        [ReadOnly]
        public NativeArray<Vector3> anchorVerts;
        [ReadOnly]
        public NativeArray<int> meshIndexLimits;
        [ReadOnly]
        public NativeArray<Matrix4x4> transformMatrices;
        [ReadOnly]
        public NativeArray<Octave> octaves;
        [ReadOnly]
        public NativeArray<float2> ratios;

        public float time;

        private float Noise(float x, float y)
        {
            float2 pos = math.float2(x, y);

            return noise.snoise(pos);
        }
        
        public void Execute(int Index)
        {
            //equally distributed verts
            int transformIndexCounter = 0;
            Matrix4x4 localToWorld = new Matrix4x4();
            float2 ratio = new float2();
            
            for (int i = 0; i < meshIndexLimits.Length; i++)
            {
                transformIndexCounter += meshIndexLimits[i];
                if (Index < transformIndexCounter)
                {
                    ratio = ratios[i];
                    localToWorld = transformMatrices[i];
                    break;
                }
            }

            var Y_incr = 0f;
            var X_incr = 0f;
            var Z_incr = 0f;

            var anchorPosition = localToWorld.MultiplyPoint3x4(anchorVerts[Index]);

            for (int o = 0; o < octaves.Length; o++)
            {
                if (octaves[o].Wavelength != 0)
                {
                    float k = 2 * Mathf.PI / octaves[o].Wavelength;
                    float c = Mathf.Sqrt(9.8f / k);
                    Vector2 d = octaves[o].Direction.normalized;
                    float f = k * (Vector2.Dot(d, new Vector2(anchorPosition.x, anchorPosition.z)) - c * time);
                    float a = octaves[o].Steepness / k;

                    X_incr += d.x * (a * Mathf.Cos(f * ratio.x));
                    Y_incr += a * Mathf.Sin(f * ratio.y);
                    Z_incr += d.y * (a * Mathf.Cos(f * ratio.y));
                }
            }

            vertices[Index] = new Vector3(anchorVerts[Index].x + X_incr, Y_incr, anchorVerts[Index].z + Z_incr);

        }
    }
}
