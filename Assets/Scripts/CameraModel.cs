// CameraModel.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class CameraModel : MonoBehaviour
{
    public struct Triangle
    {
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;

        public Vector3 this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return v0;
                }
                else if (index == 1)
                {
                    return v1;
                }
                else if (index == 2)
                {
                    return v2;
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }

            set
            {
                if (index == 0)
                {
                    v0 = value;
                }
                else if (index == 1)
                {
                    v1 = value;
                }
                else if (index == 2)
                {
                    v2 = value;
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }
    }

    private GameObject displayPlane;
    public Shader displayPlaneShader;
    private RenderTexture renderTexture;
    private RenderTexture ZBufferTexture;
    private new Renderer renderer;
    private new Camera camera;

    int kernelRender, kernelClear;

    public ComputeShader rasterizerShader;

    private ComputeBuffer triangleBuffer;

    public int textureWidth = 1920;
    public int textureHeight = 1080;

    [Range(0.0f, 1.0f)]
    public float opacity = 1.0f;

    private Triangle[] triangles;

    private void UpdateDisplayPlane()
    {
        float aspect = GetComponent<Camera>().aspect;
        float fov = GetComponent<Camera>().fieldOfView;
        float near = GetComponent<Camera>().nearClipPlane * 1.001f;
        float nearHeight = near * Mathf.Tan((fov * 0.5f) * Mathf.Deg2Rad) * 2.0f;
        float nearWidth = nearHeight * aspect;
        Matrix4x4 matrix = GetComponent<Camera>().transform.localToWorldMatrix
                           * Matrix4x4.Translate(new Vector3(0.0f, 0.0f, near));

        displayPlane.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        displayPlane.transform.localScale = new Vector3(nearWidth, nearHeight, 1.0f);
    }

    private GameObject[] GetAllGameObjects()
    {
        return FindObjectsOfType(typeof(GameObject)) as GameObject[];
    }

    private Triangle[] GetAllTriangles()
    {
        GameObject[] gameObjects = GetAllGameObjects();
        List<Triangle> triangleList = new List<Triangle>();
        foreach (GameObject gameObject in gameObjects)
        {
            if (gameObject == displayPlane)
            {
                continue;
            }

            if (!gameObject.TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
            {
                continue;
            }

            Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null)
            {
                continue;
            }

            Transform transform = gameObject.transform;
            Vector3[] vertices = mesh.vertices;
            int[] faces = mesh.triangles;

            for (int i = 0; i < faces.Length; i += 3)
            {
                triangleList.Add(new Triangle
                {
                    v0 = transform.TransformPoint(vertices[faces[i]]),
                    v1 = transform.TransformPoint(vertices[faces[i + 1]]),
                    v2 = transform.TransformPoint(vertices[faces[i + 2]]),
                });
            }
        }
        return triangleList.ToArray();
    }

    private RenderTexture CreateRenderTexture(RenderTextureFormat format, FilterMode filterMode)
    {
        RenderTexture texture;
        texture = new RenderTexture(textureWidth, textureHeight, 0);
        texture.enableRandomWrite = true;
        texture.format = format;
        texture.filterMode = filterMode;
        texture.Create();

        return texture;
    }

    private void Start()
    {
        // Get Camera
        camera = GetComponent<Camera>();

        // Get All Triangles
        triangles = GetAllTriangles();

        // Create Render Textures
        renderTexture = CreateRenderTexture(RenderTextureFormat.ARGBFloat, FilterMode.Point);
        ZBufferTexture = CreateRenderTexture(RenderTextureFormat.RFloat, FilterMode.Point);

        // Get Kernels
        kernelRender = rasterizerShader.FindKernel("Render");
        kernelClear = rasterizerShader.FindKernel("Clear");

        // Create Compute Buffers
        triangleBuffer = new ComputeBuffer(triangles.Length, UnsafeUtility.SizeOf<Triangle>());

        // Setup Basic Variables
        rasterizerShader.SetInt("textureWidth", textureWidth);
        rasterizerShader.SetInt("textureHeight", textureHeight);

        // Setup Triangle Buffer
        triangleBuffer.SetData(triangles);
        rasterizerShader.SetBuffer(kernelRender, "triangleBuffer", triangleBuffer);

        // Create Display Plane
        displayPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);

        // Bind Render Texture to Compute Shader
        rasterizerShader.SetTexture(kernelRender, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelRender, "ZBuffer", ZBufferTexture);
        rasterizerShader.SetTexture(kernelClear, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelClear, "ZBuffer", ZBufferTexture);

        // Bind Render Texture to Display Plane
        renderer = displayPlane.GetComponent<MeshRenderer>();
        renderer.material = new Material(displayPlaneShader);
        renderer.enabled = true;
        renderer.material.SetTexture("_MainTex", renderTexture);
        renderer.material.SetFloat("_Opacity", opacity);
    }
 
    private void Update()
    {
        // Update Display Plane
        UpdateDisplayPlane();

        // Update Opacity
        renderer.material.SetFloat("_Opacity", opacity);

        // Update Basic Variables
        rasterizerShader.SetMatrix("worldToCamera", camera.transform.worldToLocalMatrix);
        rasterizerShader.SetMatrix("projectionMatrix", camera.projectionMatrix);

        // Clear Frame
        rasterizerShader.Dispatch(kernelClear, textureWidth / 32, textureHeight / 18, 1);

        // Execute Shader
        rasterizerShader.Dispatch(kernelRender, (int)Mathf.Ceil(triangles.Length / 512.0f), 1, 1);
    }

    private void OnDestroy()
    {
        if (triangleBuffer != null)
        {
            triangleBuffer.Dispose();
        }
    }
}
