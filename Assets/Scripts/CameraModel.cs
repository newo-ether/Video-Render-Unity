// CameraModel.cs

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine.UI;
using TMPro;

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
    
    public struct RayCastingCamera
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
    private GameObject ZBufferPlane;
    private GameObject topLine;
    private GameObject bottomLine;
    private GameObject leftLine;
    private GameObject rightLine;
    private GameObject topLeftLine;
    private GameObject topRightLine;
    private GameObject bottomLeftLine;
    private GameObject bottomRightLine;

    private GameObject oneRay;
    private GameObject[] allRays;
    
    private GameObject[] oneTriangleRay;
    private GameObject[] allTriangleRays;

    private GameObject[] ZBufferTexts;

    [Range(0.0f, 0.1f)]
    public float wireframeWidth = 0.02f;
    [Range(0.0f, 0.1f)]
    public float rayWidth = 0.02f;
    [Range(0.0f, 0.5f)]
    public float borderWidth = 0.1f;

    public float wireframeOffset = 0.1f;
    public Color gridColor = new Color(0.0f, 1.0f, 0.0f, 1.0f);
    [Range(0.0f, 100.0f)]
    public float gridEmission = 20.0f;
    [Range(0.0f, 100.0f)]
    public float screenEmission = 5.0f;
    public float displayPlaneAppearScreen = 0.0f;
    public float displayPlaneAppearGrid = 0.0f;
    public bool disableZBuffer = false;
    public float ZBufferPlaneAppearScreen = 0.0f;
    public float ZBufferPlaneAppearGrid = 0.0f;
    public float ZBufferPlaneYMovement = 0.0f;
    public float ZBufferToneMapping = 5.0f;
    public Shader ZBufferTextShader;
    public TMP_FontAsset ZBufferTextFont;
    [Range(0.0f, 0.005f)]
    public float ZBufferTextSize = 0.005f;
    [Range(0.0f, 10.0f)]
    public float appearTransitionWidth = 1.0f;
    [Range(0.0f, 10.0f)]
    public float appearTransitionSmoothness = 1.0f;

    public int oneRayIndex = 0;
    [Range(0.0f, 1.0f)]
    public float oneRayAppear = 0.0f;
    
    [Range(0.0f, 1.0f)]
    public float allRaysAppear = 0.0f;

    public int oneTriangleRayIndex = 0;
    [Range(0.0f, 1.0f)]
    public float oneTriangleRayAppear = 0.0f;
    
    [Range(0.0f, 1.0f)]
    public float allTriangleRaysAppear = 0.0f;

    public int pixelClampMin = 0;
    public int pixelClampMax = 0;

    public int triangleClampMin = 0;
    public int triangleClampMax = 0;
    [Range(0.0f, 1.0f)]
    public float triangleAppear = 0.0f;

    public int triangleOne = 0;
    public int triangleTwo = 0;
    [Range(0.0f, 1.0f)]
    public float triangleOneAppear = 0.0f;
    [Range(0.0f, 1.0f)]
    public float triangleTwoAppear = 0.0f;

    private Texture2D ZBufferTexture2D;
    
    public Material displayPlaneMaterial;
    public Material ZBufferPlaneMaterial;
    public Material wireframeMaterial;
    public Material rayMaterial;
    private RenderTexture renderTexture;
    private RenderTexture ZBufferTexture;
    private Renderer displayPlaneMeshRenderer;
    private Renderer ZBufferPlaneMeshRenderer;
    private new Camera camera;
    
    private RayCastingCamera rayCastingCamera;

    private int kernelClear;
    private int kernelGeometryProcessing;
    private int kernelRasterizer;
    private int kernelRayCasting;

    public Mode mode;

    public enum Mode
    {
        RayCasting = 0,
        Rasterizer = 1
    }

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
    public float displayPlaneOpacity = 1.0f;
    [Range(0.0f, 1.0f)]
    public float ZBufferPlaneOpacity = 1.0f;

    private Triangle[] triangles;

    private void UpdateCamera()
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

        float offsetScreenPlaneWidth = screenPlaneWidth + 2.0f * wireframeOffset;
        float offsetScreenPlaneHeight = screenPlaneHeight + 2.0f * wireframeOffset / camera.aspect;

        Vector3 screenTopLeftCorner = screenPlaneOrigin
                                      - 0.5f * screenPlaneWidth * right
                                      + 0.5f * screenPlaneHeight * up;

        Vector3 screenLowerLeftCorner = screenPlaneOrigin
                                        - 0.5f * screenPlaneWidth * right
                                        - 0.5f * screenPlaneHeight * up;

        Vector3 screenTopRightCorner = screenPlaneOrigin
                                       + 0.5f * screenPlaneWidth * right
                                       + 0.5f * screenPlaneHeight * up;

        Vector3 screenLowerRightCorner = screenPlaneOrigin
                                         + 0.5f * screenPlaneWidth * right
                                         - 0.5f * screenPlaneHeight * up;

        Vector3 offsetScreenTopLeftCorner = screenPlaneOrigin
                                            - 0.5f * offsetScreenPlaneWidth * right
                                            + 0.5f * offsetScreenPlaneHeight * up;

        Vector3 offsetScreenLowerLeftCorner = screenPlaneOrigin
                                              - 0.5f * offsetScreenPlaneWidth * right
                                              - 0.5f * offsetScreenPlaneHeight * up;

        Vector3 offsetScreenTopRightCorner = screenPlaneOrigin
                                             + 0.5f * offsetScreenPlaneWidth * right
                                             + 0.5f * offsetScreenPlaneHeight * up;

        Vector3 offsetScreenLowerRightCorner = screenPlaneOrigin
                                               + 0.5f * offsetScreenPlaneWidth * right
                                               - 0.5f * offsetScreenPlaneHeight * up;

        // Update Display Plane
        Matrix4x4 matrix = camera.transform.localToWorldMatrix
                           * Matrix4x4.Translate(new Vector3(0.0f, 0.0f, camera.nearClipPlane * 1.001f));

        displayPlane.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        displayPlane.transform.localScale = new Vector3(screenPlaneWidth, screenPlaneHeight, 1.0f);
        
        // Update Z-Buffer Plane
        matrix = camera.transform.localToWorldMatrix
                 * Matrix4x4.Translate(new Vector3(0.0f, ZBufferPlaneYMovement, camera.nearClipPlane * 1.005f));

        ZBufferPlane.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        ZBufferPlane.transform.localScale = new Vector3(screenPlaneWidth, screenPlaneHeight, 1.0f);

        // Update Camera Wireframe
        matrix = camera.transform.localToWorldMatrix
                 * Matrix4x4.Translate(new Vector3(0.0f, offsetScreenPlaneHeight * 0.5f, camera.nearClipPlane))
                 * Matrix4x4.Rotate(Quaternion.AngleAxis(90.0f, Vector3.forward));

        topLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        topLine.transform.localScale = new Vector3(wireframeWidth, 0.5f * offsetScreenPlaneWidth, wireframeWidth);

        matrix = camera.transform.localToWorldMatrix
                 * Matrix4x4.Translate(new Vector3(0.0f, -offsetScreenPlaneHeight * 0.5f, camera.nearClipPlane))
                 * Matrix4x4.Rotate(Quaternion.AngleAxis(90.0f, Vector3.forward));

        bottomLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        bottomLine.transform.localScale = new Vector3(wireframeWidth, 0.5f * offsetScreenPlaneWidth, wireframeWidth);

        matrix = camera.transform.localToWorldMatrix
                 * Matrix4x4.Translate(new Vector3(-0.5f * offsetScreenPlaneWidth, 0.0f, camera.nearClipPlane));

        leftLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        leftLine.transform.localScale = new Vector3(wireframeWidth, 0.5f * offsetScreenPlaneHeight, wireframeWidth);

        matrix = camera.transform.localToWorldMatrix
                 * Matrix4x4.Translate(new Vector3(0.5f * offsetScreenPlaneWidth, 0.0f, camera.nearClipPlane));

        rightLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        rightLine.transform.localScale = new Vector3(wireframeWidth, 0.5f * offsetScreenPlaneHeight, wireframeWidth);

        matrix = Matrix4x4.Translate(0.5f * (offsetScreenTopLeftCorner + pos))
                 * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, pos - offsetScreenTopLeftCorner));

        topLeftLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        topLeftLine.transform.localScale = new Vector3(wireframeWidth,
            0.5f * Vector3.Magnitude(pos - offsetScreenTopLeftCorner), wireframeWidth);

        matrix = Matrix4x4.Translate(0.5f * (offsetScreenTopRightCorner + pos))
                 * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, pos - offsetScreenTopRightCorner));

        topRightLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        topRightLine.transform.localScale = new Vector3(wireframeWidth,
            0.5f * Vector3.Magnitude(pos - offsetScreenTopRightCorner), wireframeWidth);

        matrix = Matrix4x4.Translate(0.5f * (offsetScreenLowerLeftCorner + pos))
                 * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, pos - offsetScreenLowerLeftCorner));

        bottomLeftLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        bottomLeftLine.transform.localScale = new Vector3(wireframeWidth,
            0.5f * Vector3.Magnitude(pos - offsetScreenLowerLeftCorner), wireframeWidth);

        matrix = Matrix4x4.Translate(0.5f * (offsetScreenLowerRightCorner + pos))
                 * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, pos - offsetScreenLowerRightCorner));

        bottomRightLine.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
        bottomRightLine.transform.localScale = new Vector3(wireframeWidth,
            0.5f * Vector3.Magnitude(pos - offsetScreenLowerRightCorner), wireframeWidth);

        // Update Ray Casting Camera Data
        rayCastingCamera.pos = pos;
        rayCastingCamera.look = look;
        rayCastingCamera.up = up;
        rayCastingCamera.right = right;
        rayCastingCamera.screenLowerLeftCorner = screenLowerLeftCorner;
        rayCastingCamera.screenPlaneWidth = screenPlaneWidth;
        rayCastingCamera.screenPlaneHeight = screenPlaneHeight;

        // Update One Ray
        if (mode == Mode.RayCasting && oneRayIndex > 0 && oneRayIndex < textureWidth * textureHeight)
        {
            int x = oneRayIndex % textureWidth;
            int y = textureHeight - oneRayIndex / textureWidth - 1;
            Vector2 screenOffset = new Vector2((x + 0.5f) / textureWidth, (y + 0.5f) / textureHeight);
            Vector3 screenPoint = screenLowerLeftCorner
                                  + screenOffset.x * screenPlaneWidth * right
                                  + screenOffset.y * screenPlaneHeight * up;

            Vector3 origin = pos;
            Vector3 dir = Vector3.Normalize(screenPoint - origin);
            float depth = ZBufferTexture2D.GetPixel(x, y).r;
            Vector3 end = origin + oneRayAppear * depth * dir;

            matrix = Matrix4x4.Translate(0.5f * (origin + end))
                     * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, dir));

            oneRay.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
            oneRay.transform.localScale = new Vector3(rayWidth, 0.5f * oneRayAppear * depth, rayWidth);
        }
        else
        {
            oneRay.transform.position = Vector3.zero;
            oneRay.transform.localScale = Vector3.zero;
        }
        
        // Update All Rays
        if (mode == Mode.RayCasting)
        {
            for (int i = 0; i < textureWidth * textureHeight; i++)
            {
                int x = i % textureWidth;
                int y = textureHeight - i / textureWidth - 1;
                Vector2 screenOffset = new Vector2((x + 0.5f) / textureWidth, (y + 0.5f) / textureHeight);
                Vector3 screenPoint = screenLowerLeftCorner
                                      + screenOffset.x * screenPlaneWidth * right
                                      + screenOffset.y * screenPlaneHeight * up;

                Vector3 origin = pos;
                Vector3 dir = Vector3.Normalize(screenPoint - origin);
                float depth = ZBufferTexture2D.GetPixel(x, y).r;
                Vector3 end = origin + allRaysAppear * depth * dir;

                matrix = Matrix4x4.Translate(0.5f * (origin + end))
                         * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, dir));

                allRays[i].transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
                allRays[i].transform.localScale = new Vector3(rayWidth, 0.5f * allRaysAppear * depth, rayWidth);
            }
        }
        else
        {
            foreach (var ray in allRays)
            {
                ray.transform.position = Vector3.zero;
                ray.transform.localScale = Vector3.zero;
            }
        }
        
        // Update One Triangle Ray
        if (mode == Mode.Rasterizer && oneTriangleRayIndex > 0 && oneTriangleRayIndex < triangles.Length)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = triangles[oneTriangleRayIndex][i].xyz;
                Vector3 origin = pos;
                Vector3 dir = p - origin;
                Vector3 end = origin + oneTriangleRayAppear * dir;

                matrix = Matrix4x4.Translate(0.5f * (origin + end))
                         * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, dir));

                oneTriangleRay[i].transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
                oneTriangleRay[i].transform.localScale = new Vector3(
                    rayWidth,
                    0.5f * oneTriangleRayAppear * Vector3.Magnitude(dir),
                    rayWidth);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                oneTriangleRay[i].transform.position = Vector3.zero;
                oneTriangleRay[i].transform.localScale = Vector3.zero;
            }
        }
        
        // Update All Triangle Rays
        if (mode == Mode.Rasterizer)
        {
            for (int n = 0; n < triangles.Length; n++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = triangles[n][i].xyz;
                    Vector3 origin = pos;
                    Vector3 dir = p - origin;
                    Vector3 end = origin + allTriangleRaysAppear * dir;

                    matrix = Matrix4x4.Translate(0.5f * (origin + end))
                             * Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, dir));

                    allTriangleRays[n * 3 + i].transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
                    allTriangleRays[n * 3 + i].transform.localScale = new Vector3(
                        rayWidth,
                        0.5f * allTriangleRaysAppear * Vector3.Magnitude(dir),
                        rayWidth);
                }
            }
        }
        else
        {
            foreach (var ray in allTriangleRays)
            {
                ray.transform.position = Vector3.zero;
                ray.transform.localScale = Vector3.zero;
            }
        }
        
        // Update Z-Buffer Texts
        for (int i = 0; i < textureWidth * textureHeight; i++)
        {
            int x = i % textureWidth;
            int y = textureHeight - i / textureWidth - 1;
            Vector2 screenOffset =
                new Vector2((x + 0.5f) / textureWidth, (y + 0.5f) / textureHeight) * 2.0f - Vector2.one;
            matrix = camera.transform.localToWorldMatrix
                     * Matrix4x4.Translate(
                         new Vector3(0.5f * screenOffset.x * screenPlaneWidth,
                             0.5f * screenOffset.y * screenPlaneHeight + ZBufferPlaneYMovement,
                             camera.nearClipPlane * 1.0049f));

            float depth = ZBufferTexture2D.GetPixel(x, y).r;
            float color = Mathf.Pow(depth, ZBufferToneMapping);
            color = -0.2f * color + (color > 0.5f ? 0.2f : 1.0f);
            
            GameObject obj = ZBufferTexts[i];
            obj.GetComponent<MeshRenderer>().material.SetColor("_FaceColor", new Color(color, color, color, 1.0f));
            
            TextMeshPro tmp = obj.GetComponent<TextMeshPro>();
            tmp.transform.SetLocalPositionAndRotation(matrix.GetPosition(), matrix.rotation);
            tmp.transform.localScale = new Vector3(ZBufferTextSize, ZBufferTextSize, ZBufferTextSize);
            tmp.text = depth.ToString("F2");
        }
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
            // if (gameObject.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
            // {
            //     if (meshRenderer.material.shader.name == "Shader Graphs/Icosphere")
            //     {
            //         meshRenderer.material.SetFloat("_Index_Offset", triangleList.Count);
            //         Debug.Log(meshRenderer.material.name + " : " + triangleList.Count);
            //     }
            // }
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
        rasterizerShader.SetInt("triangleClampMin", Math.Clamp(triangleClampMin, 0, triangles.Length));
        rasterizerShader.SetInt("triangleClampMax", Math.Clamp(triangleClampMax, 0, triangles.Length));
        rasterizerShader.SetFloat("triangleAppear", Math.Clamp(triangleAppear, 0.0f, 1.0f));
        rasterizerShader.SetBool("disableZBuffer", disableZBuffer);

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
        rayCastingShader.SetInt("pixelClampMin", Math.Clamp(pixelClampMin, 0, textureWidth * textureHeight - 1));
        rayCastingShader.SetInt("pixelClampMax", Math.Clamp(pixelClampMax, 0, textureWidth * textureHeight - 1));

        // Bind Compute Buffers
        rayCastingShader.SetBuffer(kernelRayCasting, "triangleBuffer", triangleBuffer);
        rayCastingShader.SetBuffer(kernelRayCasting, "cameraBuffer", cameraBuffer);
        
        // Bind Render Texture to Compute Shader
        rayCastingShader.SetTexture(kernelRayCasting, "frameBuffer", renderTexture);
        rayCastingShader.SetTexture(kernelRayCasting, "depthBuffer", ZBufferTexture);
    }

    private void InitOneRay()
    {
        oneRay = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    }
    
    private void InitAllRays()
    {
        List<GameObject> objList = new List<GameObject>();
        for (int i = 0; i < textureWidth * textureHeight; i++)
        {
            objList.Add(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        }
        allRays = objList.ToArray();
    }
    
    private void InitOneTriangleRay()
    {
        oneTriangleRay = new GameObject[]
        {
            GameObject.CreatePrimitive(PrimitiveType.Cylinder),
            GameObject.CreatePrimitive(PrimitiveType.Cylinder),
            GameObject.CreatePrimitive(PrimitiveType.Cylinder)
        };
    }
    
    private void InitAllTriangleRays()
    {
        List<GameObject> objList = new List<GameObject>();
        for (int i = 0; i < triangles.Length * 3; i++)
        {
            objList.Add(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        }
        allTriangleRays = objList.ToArray();
    }

    private void InitZBufferTexts()
    {
        List<GameObject> objList = new List<GameObject>();
        for (int i = 0; i < textureWidth * textureHeight; i++)
        {
            GameObject obj = new GameObject("Text");
            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            obj.GetComponent<MeshRenderer>().material = new Material(ZBufferTextShader);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.font = ZBufferTextFont;
            tmp.fontStyle = FontStyles.Bold;
            objList.Add(obj);
        }
        ZBufferTexts = objList.ToArray();
    }

    private void GetZBuffer()
    {
        RenderTexture.active = ZBufferTexture;
        ZBufferTexture2D.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        ZBufferTexture2D.Apply();
    }

    private void Start()
    {
        // Get Camera
        camera = GetComponent<Camera>();
        
        // Get All Triangles
        triangles = GetAllTriangles();

        // Create Display Plane
        displayPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        
        // Create Z-Buffer Plane
        ZBufferPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        
        // Create Z-Buffer Texts
        InitZBufferTexts();
        
        // Create Wireframe
        topLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topLeftLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topRightLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomLeftLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomRightLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        
        // Assign Material to Wireframe
        topLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        bottomLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        leftLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        rightLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        topLeftLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        topRightLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        bottomLeftLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        bottomRightLine.GetComponent<MeshRenderer>().material = wireframeMaterial;
        
        // Init Rays
        InitOneRay();
        InitAllRays();
        InitOneTriangleRay();
        InitAllTriangleRays();
        
        // Assign Material to Rays
        oneRay.GetComponent<MeshRenderer>().material = rayMaterial;
        foreach (var obj in allRays)
        {
            obj.GetComponent<MeshRenderer>().material = rayMaterial;
        }
        foreach (var obj in oneTriangleRay)
        {
            obj.GetComponent<MeshRenderer>().material = rayMaterial;
        }
        foreach (var obj in allTriangleRays)
        {
            obj.GetComponent<MeshRenderer>().material = rayMaterial;
        }
        
        // Create Triangle Buffer
        triangleBuffer = new ComputeBuffer(triangles.Length, UnsafeUtility.SizeOf<Triangle>());
        
        // Setup Triangle Buffer
        triangleBuffer.SetData(triangles);

        // Create Render Textures
        renderTexture = CreateRenderTexture(RenderTextureFormat.ARGBFloat, FilterMode.Point);
        ZBufferTexture = CreateRenderTexture(RenderTextureFormat.ARGBFloat, FilterMode.Point);
        
        // Create Texture2D
        ZBufferTexture2D = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBAFloat, false);
        
        // Init Compute Shaders
        InitRasterizer();
        InitRayCasting();

        // Bind Render Texture to Display Plane
        displayPlaneMeshRenderer = displayPlane.GetComponent<MeshRenderer>();
        displayPlaneMeshRenderer.material = displayPlaneMaterial;
        displayPlaneMeshRenderer.enabled = true;
        displayPlaneMaterial.SetTexture("_MainTex", renderTexture);
        displayPlaneMaterial.SetFloat("_Aspect", camera.aspect);
        displayPlaneMaterial.SetFloat("_Opacity", displayPlaneOpacity);
        displayPlaneMaterial.SetFloat("_Emission", screenEmission);
        displayPlaneMaterial.SetFloat("_Border_Width", borderWidth);
        displayPlaneMaterial.SetFloat("_Texture_Width", textureWidth);
        displayPlaneMaterial.SetFloat("_Texture_Height", textureHeight);
        displayPlaneMaterial.SetColor("_Grid_Color", gridColor);
        displayPlaneMaterial.SetFloat("_Grid_Emission", gridEmission);
        displayPlaneMaterial.SetFloat("_Screen_Emission", screenEmission);
        displayPlaneMaterial.SetFloat("_Appear_Grid", displayPlaneAppearGrid);
        displayPlaneMaterial.SetFloat("_Appear_Screen", displayPlaneAppearScreen);
        displayPlaneMaterial.SetFloat("_Appear_Transition_Width", appearTransitionWidth);
        displayPlaneMaterial.SetFloat("_Appear_Transition_Smoothness", appearTransitionSmoothness);
        displayPlaneMaterial.SetFloat("_Tone_Mapping", 1.0f);
        
        // Bind Z-Buffer Texture to Z-Buffer Plane
        ZBufferPlaneMeshRenderer = ZBufferPlane.GetComponent<MeshRenderer>();
        ZBufferPlaneMeshRenderer.material = ZBufferPlaneMaterial;
        ZBufferPlaneMeshRenderer.enabled = true;
        ZBufferPlaneMaterial.SetTexture("_MainTex", ZBufferTexture);
        ZBufferPlaneMaterial.SetFloat("_Aspect", camera.aspect);
        ZBufferPlaneMaterial.SetFloat("_Opacity", ZBufferPlaneOpacity);
        ZBufferPlaneMaterial.SetFloat("_Emission", screenEmission);
        ZBufferPlaneMaterial.SetFloat("_Border_Width", borderWidth);
        ZBufferPlaneMaterial.SetFloat("_Texture_Width", textureWidth);
        ZBufferPlaneMaterial.SetFloat("_Texture_Height", textureHeight);
        ZBufferPlaneMaterial.SetColor("_Grid_Color", gridColor);
        ZBufferPlaneMaterial.SetFloat("_Grid_Emission", gridEmission);
        ZBufferPlaneMaterial.SetFloat("_Screen_Emission", screenEmission);
        ZBufferPlaneMaterial.SetFloat("_Appear_Grid", ZBufferPlaneAppearGrid);
        ZBufferPlaneMaterial.SetFloat("_Appear_Screen", ZBufferPlaneAppearScreen);
        ZBufferPlaneMaterial.SetFloat("_Appear_Transition_Width", appearTransitionWidth);
        ZBufferPlaneMaterial.SetFloat("_Appear_Transition_Smoothness", appearTransitionSmoothness);
        ZBufferPlaneMaterial.SetFloat("_Tone_Mapping", ZBufferToneMapping);
    }
 
    private void Update()
    {
        // Get Z-Buffer
        GetZBuffer();
        
        // Update Camera
        UpdateCamera();
        
        // Update Display Plane Material
        displayPlaneMaterial.SetFloat("_Aspect", camera.aspect);
        displayPlaneMaterial.SetFloat("_Opacity", displayPlaneOpacity);
        displayPlaneMaterial.SetFloat("_Emission", screenEmission);
        displayPlaneMaterial.SetFloat("_Border_Width", borderWidth);
        displayPlaneMaterial.SetColor("_Grid_Color", gridColor);
        displayPlaneMaterial.SetFloat("_Grid_Emission", gridEmission);
        displayPlaneMaterial.SetFloat("_Screen_Emission", screenEmission);
        displayPlaneMaterial.SetFloat("_Appear_Grid", displayPlaneAppearGrid);
        displayPlaneMaterial.SetFloat("_Appear_Screen", displayPlaneAppearScreen);
        displayPlaneMaterial.SetFloat("_Appear_Transition_Width", appearTransitionWidth);
        displayPlaneMaterial.SetFloat("_Appear_Transition_Smoothness", appearTransitionSmoothness);
        
        // Update Z-Buffer Plane Material
        ZBufferPlaneMaterial.SetFloat("_Aspect", camera.aspect);
        ZBufferPlaneMaterial.SetFloat("_Opacity", ZBufferPlaneOpacity);
        ZBufferPlaneMaterial.SetFloat("_Emission", screenEmission);
        ZBufferPlaneMaterial.SetFloat("_Border_Width", borderWidth);
        ZBufferPlaneMaterial.SetColor("_Grid_Color", gridColor);
        ZBufferPlaneMaterial.SetFloat("_Grid_Emission", gridEmission);
        ZBufferPlaneMaterial.SetFloat("_Screen_Emission", screenEmission);
        ZBufferPlaneMaterial.SetFloat("_Appear_Grid", ZBufferPlaneAppearGrid);
        ZBufferPlaneMaterial.SetFloat("_Appear_Screen", ZBufferPlaneAppearScreen);
        ZBufferPlaneMaterial.SetFloat("_Appear_Transition_Width", appearTransitionWidth);
        ZBufferPlaneMaterial.SetFloat("_Appear_Transition_Smoothness", appearTransitionSmoothness);
        ZBufferPlaneMaterial.SetFloat("_Tone_Mapping", ZBufferToneMapping);

        // Execute Shader
        if (mode == Mode.Rasterizer)
        {
            ExecuteRasterizer();
        }
        else if (mode == Mode.RayCasting)
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
        
        rasterizerShader.SetInt("triangleClampMin", Math.Clamp(triangleClampMin, 0, triangles.Length - 1));
        rasterizerShader.SetInt("triangleClampMax", Math.Clamp(triangleClampMax, 0, triangles.Length - 1));
        rasterizerShader.SetFloat("triangleAppear", Math.Clamp(triangleAppear, 0.0f, 1.0f));
        rasterizerShader.SetBool("disableZBuffer", disableZBuffer);
        rasterizerShader.SetInt("triangleOne", Math.Clamp(triangleOne, 0, triangles.Length - 1));
        rasterizerShader.SetInt("triangleTwo", Math.Clamp(triangleTwo, 0, triangles.Length - 1));
        rasterizerShader.SetFloat("triangleOneAppear", triangleOneAppear);
        rasterizerShader.SetFloat("triangleTwoAppear", triangleTwoAppear);
        
        // Clear Frame
        rasterizerShader.Dispatch(kernelClear, textureWidth / 30, textureHeight / 30, 1);

        // Execute Geometry Processing Shader
        rasterizerShader.Dispatch(kernelGeometryProcessing, (int) Mathf.Ceil(triangles.Length / 512.0f), 1, 1);
        
        // Execute Render Shader
        for (int i = 0; i < triangles.Length * 2; i++)
        {
            rasterizerShader.SetInt("triangleIndex", i);
            rasterizerShader.Dispatch(kernelRasterizer, textureWidth / 30, textureHeight / 30, 1);
        }
    }

    private void ExecuteRayCasting()
    {
        rayCastingShader.SetInt("pixelClampMin", Math.Clamp(pixelClampMin, 0, textureWidth * textureHeight - 1));
        rayCastingShader.SetInt("pixelClampMax", Math.Clamp(pixelClampMax, 0, textureWidth * textureHeight - 1));
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
