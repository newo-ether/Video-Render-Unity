// CameraModel.cs

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public class CameraModel : MonoBehaviour
{
    public struct Triangle
    {
        public float4 v0;
        public float4 v1;
        public float4 v2;

        public float4 this[int index]
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

    private int kernelClear;
    private int kernelGeometryProcessing;
    private int kernelRender;

    public ComputeShader rasterizerShader;

    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triangleNDCBuffer;
    private ComputeBuffer triangleNDCDivWBuffer;
    private ComputeBuffer triangleBoundBuffer;

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
                    v0 = new float4(transform.TransformPoint(vertices[faces[i]]), 1.0f),
                    v1 = new float4(transform.TransformPoint(vertices[faces[i + 1]]), 1.0f),
                    v2 = new float4(transform.TransformPoint(vertices[faces[i + 2]]), 1.0f),
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
        kernelClear = rasterizerShader.FindKernel("KernelClear");
        kernelGeometryProcessing = rasterizerShader.FindKernel("KernelGeometryProcessing");
        kernelRender = rasterizerShader.FindKernel("KernelRender");

        // Create Compute Buffers
        triangleBuffer = new ComputeBuffer(triangles.Length, UnsafeUtility.SizeOf<Triangle>());
        triangleNDCBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<Triangle>());
        triangleNDCDivWBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<Triangle>());
        triangleBoundBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<uint4>());

        // Setup Triangle Buffer
        triangleBuffer.SetData(triangles);
        
        // Setup Basic Variables
        rasterizerShader.SetInt("textureWidth", textureWidth);
        rasterizerShader.SetInt("textureHeight", textureHeight);
        rasterizerShader.SetInt("triangleCount", triangles.Length);
        rasterizerShader.SetInt("clippedTriangleCount", triangles.Length * 2);

        // Bind Compute Buffers
        rasterizerShader.SetBuffer(kernelGeometryProcessing, "triangleBuffer", triangleBuffer);
        rasterizerShader.SetBuffer(kernelGeometryProcessing, "triangleNDCBuffer", triangleNDCBuffer);
        rasterizerShader.SetBuffer(kernelGeometryProcessing, "triangleNDCDivWBuffer", triangleNDCDivWBuffer);
        rasterizerShader.SetBuffer(kernelGeometryProcessing, "triangleBoundBuffer", triangleBoundBuffer);

        rasterizerShader.SetBuffer(kernelRender, "triangleBuffer", triangleBuffer);
        rasterizerShader.SetBuffer(kernelRender, "triangleNDCBuffer", triangleNDCBuffer);
        rasterizerShader.SetBuffer(kernelRender, "triangleNDCDivWBuffer", triangleNDCDivWBuffer);
        rasterizerShader.SetBuffer(kernelRender, "triangleBoundBuffer", triangleBoundBuffer);

        // Create Display Plane
        displayPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);

        // Bind Render Texture to Compute Shader
        rasterizerShader.SetTexture(kernelClear, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelClear, "ZBuffer", ZBufferTexture);
        rasterizerShader.SetTexture(kernelRender, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelRender, "ZBuffer", ZBufferTexture);

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
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        float verticalFov = camera.fieldOfView * Mathf.Deg2Rad;
        float tanHalfVerticalFov = Mathf.Tan(verticalFov / 2.0f);
        float tanHalfHorizontalFov = tanHalfVerticalFov * camera.aspect;
        rasterizerShader.SetMatrix("worldToNDC", 
            new Matrix4x4(
                new Vector4(1.0f / (tanHalfHorizontalFov), 0.0f, 0.0f, 0.0f),
                new Vector4(0.0f, 1.0f / (tanHalfVerticalFov), 0.0f, 0.0f),
                new Vector4(0.0f, 0.0f, far / (far - near), 1.0f),
                new Vector4(0.0f, 0.0f, -far * near / (far - near), 0.0f))
            * camera.transform.worldToLocalMatrix);
        
        // Clear Frame
        rasterizerShader.Dispatch(kernelClear, textureWidth / 30, textureHeight / 30, 1);

        // Execute Geometry Processing Shader
        rasterizerShader.Dispatch(kernelGeometryProcessing, (int) Mathf.Ceil(triangles.Length / 512.0f), 1, 1);
        
        // Execute Render Shader
        rasterizerShader.Dispatch(kernelRender, textureWidth / 30, textureHeight / 30, triangles.Length * 2);
    }

    private void OnDestroy()
    {
        triangleBuffer?.Dispose();
        triangleNDCBuffer?.Dispose();
        triangleNDCDivWBuffer?.Dispose();
        triangleBoundBuffer?.Dispose();
    }
}
