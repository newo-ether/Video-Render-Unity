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
    
    struct RayCastingCamera
    {
        public float3 pos;

        public float3 look;
        public float3 up;
        public float3 right;

        public float3 screenLowerLeftCorner;
        public float screenPlaneWidth;
        public float screenPlaneHeight;
    };

    private GameObject displayPlane;
    public Shader displayPlaneShader;
    private RenderTexture renderTexture;
    private RenderTexture ZBufferTexture;
    private new Renderer renderer;
    private new Camera camera;
    
    private RayCastingCamera rayCastingCamera;

    private int kernelClear;
    private int kernelGeometryProcessing;
    private int kernelRasterizer;
    private int kernelRayCasting;

    public bool mode = false;

    public ComputeShader rasterizerShader;
    public ComputeShader rayCastingShader;

    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triangleNDCBuffer;
    private ComputeBuffer triangleNDCDivWBuffer;
    private ComputeBuffer triangleBoundBuffer;
    private ComputeBuffer cameraBuffer;

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

    private void UpdateRayCastingCamera()
    {
        float aspect = camera.aspect;

        Vector3 pos = camera.transform.position;

        Vector3 look = camera.transform.forward;
        Vector3 up = camera.transform.up;
        Vector3 right = camera.transform.right;

        float fovVertical = camera.fieldOfView;

        Vector3 screenPlaneOrigin = pos + look * camera.nearClipPlane;
        float screenPlaneHeight = 2.0f * Mathf.Tan(fovVertical * 0.5f * Mathf.Deg2Rad) * camera.nearClipPlane;
        float screenPlaneWidth = screenPlaneHeight * aspect;
        Vector3 screenLowerLeftCorner = screenPlaneOrigin
                                        - 0.5f * screenPlaneWidth * right
                                        - 0.5f * screenPlaneHeight * up;
        
        rayCastingCamera.pos = pos;
        rayCastingCamera.look = look;
        rayCastingCamera.up = up;
        rayCastingCamera.right = right;
        rayCastingCamera.screenLowerLeftCorner = screenLowerLeftCorner;
        rayCastingCamera.screenPlaneWidth = screenPlaneWidth;
        rayCastingCamera.screenPlaneHeight = screenPlaneHeight;
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

    private void InitRasterizer()
    {
        // Get Kernels
        kernelClear = rasterizerShader.FindKernel("KernelClear");
        kernelGeometryProcessing = rasterizerShader.FindKernel("KernelGeometryProcessing");
        kernelRasterizer = rasterizerShader.FindKernel("KernelRender");

        // Create Compute Buffers
        triangleNDCBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<Triangle>());
        triangleNDCDivWBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<Triangle>());
        triangleBoundBuffer = new ComputeBuffer(triangles.Length * 2, UnsafeUtility.SizeOf<uint4>());
        
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

        rasterizerShader.SetBuffer(kernelRasterizer, "triangleBuffer", triangleBuffer);
        rasterizerShader.SetBuffer(kernelRasterizer, "triangleNDCBuffer", triangleNDCBuffer);
        rasterizerShader.SetBuffer(kernelRasterizer, "triangleNDCDivWBuffer", triangleNDCDivWBuffer);
        rasterizerShader.SetBuffer(kernelRasterizer, "triangleBoundBuffer", triangleBoundBuffer);
        
        // Bind Render Texture to Compute Shader
        rasterizerShader.SetTexture(kernelClear, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelClear, "ZBuffer", ZBufferTexture);
        rasterizerShader.SetTexture(kernelRasterizer, "frameBuffer", renderTexture);
        rasterizerShader.SetTexture(kernelRasterizer, "ZBuffer", ZBufferTexture);
    }
    
    private void InitRayCasting()
    {
        // Get Kernels
        kernelRayCasting = rayCastingShader.FindKernel("KernelRender");

        // Create Compute Buffers
        cameraBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<RayCastingCamera>());

        // Setup Basic Variables
        rayCastingShader.SetInt("renderWidth", textureWidth);
        rayCastingShader.SetInt("renderHeight", textureHeight);
        rayCastingShader.SetInt("triangleCount", triangles.Length);

        // Bind Compute Buffers
        rayCastingShader.SetBuffer(kernelRayCasting, "triangleBuffer", triangleBuffer);
        rayCastingShader.SetBuffer(kernelRayCasting, "cameraBuffer", cameraBuffer);
        
        // Bind Render Texture to Compute Shader
        rayCastingShader.SetTexture(kernelRayCasting, "renderResult", renderTexture);
    }

    private void Start()
    {
        // Get Camera
        camera = GetComponent<Camera>();

        // Get All Triangles
        triangles = GetAllTriangles();
        
        // Create Triangle Buffer
        triangleBuffer = new ComputeBuffer(triangles.Length, UnsafeUtility.SizeOf<Triangle>());
        
        // Setup Triangle Buffer
        triangleBuffer.SetData(triangles);

        // Create Render Textures
        renderTexture = CreateRenderTexture(RenderTextureFormat.ARGBFloat, FilterMode.Point);
        ZBufferTexture = CreateRenderTexture(RenderTextureFormat.RFloat, FilterMode.Point);
        
        InitRasterizer();
        InitRayCasting();

        // Create Display Plane
        displayPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);

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

        // Execute Shader
        if (mode)
        {
            ExecuteRasterizer();
        }
        else
        {
            ExecuteRayCasting();
        }
    }

    private void ExecuteRasterizer()
    {
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
        rasterizerShader.Dispatch(kernelRasterizer, textureWidth / 30, textureHeight / 30, triangles.Length * 2);
    }

    private void ExecuteRayCasting()
    {
        UpdateRayCastingCamera();
        cameraBuffer.SetData(new RayCastingCamera[] { rayCastingCamera });
        rayCastingShader.Dispatch(kernelRayCasting, textureWidth / 30, textureHeight / 30, 1);
    }

    private void OnDestroy()
    {
        triangleBuffer?.Dispose();
        triangleNDCBuffer?.Dispose();
        triangleNDCDivWBuffer?.Dispose();
        triangleBoundBuffer?.Dispose();
        cameraBuffer?.Dispose();
    }
}
