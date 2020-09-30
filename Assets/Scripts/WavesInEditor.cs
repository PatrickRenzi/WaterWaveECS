using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Octave
{
    public Vector2 Direction;
    public float Steepness;
    public float Wavelength;
}

public class WavesInEditor : MonoBehaviour
{

    public Octave[] Octaves;
    public int WavesPerUnit = 10;
    public Mesh Mesh;
    public MeshFilter MeshFilter;
    public float UVScale = 1f;

    private int X_Dimension = 0;
    private int Z_Dimension = 0;
    private float X_Ratio;
    private float Z_Ratio;
    private float ratio;
    private Matrix4x4 localToWorld;
    private Vector3[] anchorVerts;

    // Start is called before the first frame update
    void Start()
    {

        Mesh = MeshFilter.mesh;
        ////Mesh Setup

        Mesh.Clear();
        Mesh.MarkDynamic();
        Mesh.name = gameObject.name;
        Mesh.vertices = GenerateVertsByUnit();
        
        Mesh.triangles = GenerateTries();
        Mesh.uv = GenerateUVs();
        Mesh.RecalculateNormals();
        Mesh.RecalculateBounds();
        Mesh.RecalculateTangents();


    }
    
    private Vector3[] GenerateVertsByUnit()
    {
        localToWorld = transform.localToWorldMatrix;


        X_Dimension = (Mathf.RoundToInt(Mesh.bounds.extents.x * 2 * this.transform.localScale.x)) * WavesPerUnit;
        Z_Dimension = (Mathf.RoundToInt(Mesh.bounds.extents.x * 2 * this.transform.localScale.z)) * WavesPerUnit;
        X_Ratio = 1/this.transform.localScale.x;
        Z_Ratio = 1 / this.transform.localScale.z;


        var verts = new Vector3[(X_Dimension + 1) * (Z_Dimension + 1)];
        anchorVerts = new Vector3[(X_Dimension + 1) * (Z_Dimension + 1)];
      
        float x_position = Mesh.bounds.center.x - (Mesh.bounds.extents.x * this.transform.localScale.x);
        float z_position = Mesh.bounds.center.z - (Mesh.bounds.extents.z * this.transform.localScale.z);

        //this.transform.localScale = new Vector3(1f, 1f, 1f);

        //equaly distributed verts
        for (int x = 0; x <= X_Dimension; x++)
        {
            for (int z = 0; z <= Z_Dimension; z++)
            {
                verts[index(x, z)] = new Vector3(x_position + (x * (1f / WavesPerUnit)), 0, z_position + (z * (1f / WavesPerUnit)));
                anchorVerts[index(x, z)] = new Vector3(x_position + (x * (1f / WavesPerUnit)), 0, z_position + (z * (1f / WavesPerUnit)));
            }
        }

        return verts;
    }

    private int[] GenerateTries()
    {
        var tries = new int[Mesh.vertices.Length * 6];

        //two triangles are one tile
        for (int x = 0; x < X_Dimension; x++)
        {
            for (int z = 0; z < Z_Dimension; z++)
            {
                tries[index(x, z) * 6 + 0] = index(x, z);
                tries[index(x, z) * 6 + 1] = index(x + 1, z + 1);
                tries[index(x, z) * 6 + 2] = index(x + 1, z);
                tries[index(x, z) * 6 + 3] = index(x, z);
                tries[index(x, z) * 6 + 4] = index(x, z + 1);
                tries[index(x, z) * 6 + 5] = index(x + 1, z + 1);
            }
        }

        return tries;
    }

    private Vector2[] GenerateUVs()
    {
        var uvs = new Vector2[Mesh.vertices.Length];

        for(int x = 0; x <= X_Dimension; x++)
        {
            for(int z = 0; z <= Z_Dimension; z++)
            {
                    var vec = new Vector2((x / UVScale) % 2, (z / UVScale) % 2);
                    uvs[index(x, z)] = new Vector2(vec.x <= 1 ? vec.x : 2 - vec.x, vec.y <= 1 ? vec.y : 2 - vec.y);
            }
        }

        return uvs;
    }



    private int index(int x, int z)
    {
        return x * (Z_Dimension + 1) + z;
    }

    private int index(float x, float z)
    {
        return (int)(x * (Z_Dimension + 1) + z);
    }

    // Update is called once per frame
    void Update()
    {
            var verts = Mesh.vertices;
        
            for (int x = 0; x <= X_Dimension; x++)
            {

                for (int z = 0; z <= Z_Dimension; z++)
                {

                    var Y_incr = 0f;
                    var X_incr = 0f;
                    var Z_incr = 0f;
                    var vert = verts[index(x, z)];
                    var anchorVert = anchorVerts[index(x, z)];
                    var position = localToWorld.MultiplyPoint3x4(vert);
                    var anchorPosition = localToWorld.MultiplyPoint3x4(anchorVert);


                    for (int o = 0; o < Octaves.Length; o++)
                    {
                        //var perl = Mathf.PerlinNoise((vert.x * Octaves[o].scale.x) / X_Dimension, (vert.z * Octaves[o].scale.y) / Z_Dimension) * Mathf.PI * 2f;

                        float k = 2 * Mathf.PI / Octaves[o].Wavelength;
                        float c = Mathf.Sqrt(9.8f / k);
                        Vector2 d = Octaves[o].Direction.normalized;
                        float f = k * (Vector2.Dot(d, new Vector2(anchorPosition.x, anchorPosition.z)) - c * Time.time);
                        float a = Octaves[o].Steepness / k;

                        X_incr += d.x * (a * Mathf.Cos(f * X_Ratio));
                        Y_incr += a * Mathf.Sin(f * Z_Ratio);
                        Z_incr += d.y * (a * Mathf.Cos(f * Z_Ratio));
                    }

                    verts[index(x, z)] = new Vector3(anchorVert.x + X_incr, Y_incr, anchorVert.z + Z_incr);
                }
            }

            Mesh.vertices = verts;
            Mesh.RecalculateNormals();
        
    }
}
