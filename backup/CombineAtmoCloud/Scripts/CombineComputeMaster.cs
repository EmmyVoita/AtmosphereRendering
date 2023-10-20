using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CombineComputeMaster : MonoBehaviour 
{

    private RenderTexture _target;

    private RenderTexture worldPositionTexture;

    public Light directionalLightObject;

    // ------------------------------------------------------------------------------------------ //
    private Camera _camera;

    private Vector2 screenParams;

    private RenderTexture mainTextureBuffer;

  

    const string headerDecorationStart = " [ ";
    const string headerDecorationEnd = " ] ";

    [Header (headerDecorationStart + "Main" + headerDecorationEnd)]

    public ComputeShader compute_cloud;

    private int compute_cloud_kernel_id;
    // ------------------------------------------------------------------------------------------ //

    public Transform container;
    public Vector3 cloudTestParams;

    [Header (headerDecorationStart + "PerformanceSettings" + headerDecorationEnd)]
    public PerformanceSettings performanceSettings;

    [Header (headerDecorationStart + "RayMarchSettings" + headerDecorationEnd)]
    public RayMarchSettings rayMarchSettings;

    [Header(headerDecorationStart + "CloudCoverageSettings" + headerDecorationEnd)]
    public CloudCoverageSettings cloudCoverageSettings;

    [Header(headerDecorationStart + "LightingSettings" + headerDecorationEnd)]
    public LightingSettings lightingSettings;

    [Header(headerDecorationStart + "BaseShapeSettings" + headerDecorationEnd)]
    public ShapeSettings shapeSettings;

    [Header(headerDecorationStart + "Animation" + headerDecorationEnd)]
    public AnimationSettings animationSettings; 


   

    

    [Space(10)]
    [Header (headerDecorationStart + "Sky Ambient Color" + headerDecorationStart)]
    [Space(10)]
    public Color colA;
    public Color colB;

    [Range(0,1)]
    public float extinction_factor = 1.0f;

    [Space(10)]
    [Header (headerDecorationStart + "Temporal Reprojection Settings" + headerDecorationStart)]
    [Space(10)]


    [Range(1,10)]
    public int neighborhood_tile_size = 3;

    // Internal
    [HideInInspector]
    public Material material;



    private bool paramInitialized;

    private Vector4 paramProjectionExtents;
    private Matrix4x4 paramCurrV;
    private Matrix4x4 paramCurrVP;
    private Matrix4x4 paramPrevVP;
    private Matrix4x4 paramPrevVP_NoFlip;

    private Matrix4x4 inverse_projection;

    private Matrix4x4 inverse_view;



    float previous_time = 0.0f;
    float current_time = 0.0f;

    private NoiseGenerator noise;

    private const uint MAX_FRAME_COUNT = uint.MaxValue;
    private bool print_data = false;

    // ------------------------------------------------------------------------------------------ //


    // ------------------------------------------------------------------------------------------ //
    void Reset()
    {
        _camera = GetComponent<Camera>();
        noise = FindObjectOfType<NoiseGenerator>();
    }

    void Clear()
    {
        paramInitialized = false;
        previous_time = 0.0f;
        print_data = false;
    }

    private void Awake()
    {
        Reset();
        Clear();
        
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) 
        {
            print_data = true;
        }
    }

   
    private void Start()
    {
        Application.targetFrameRate = 60;
    }
  
    private void SetShaderParameters()
    {
     

        //Set PerformanceSettings:
        performanceSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id);

        // Set RayMarchSettings:
        rayMarchSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id);

        // Set LightingSettings:
        lightingSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id);

        // Set CloudCoverageSettings:
        cloudCoverageSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id);

        // Set BaseShapeSettings:'
        shapeSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id, ref noise);


        // Set AnimationSettings:
        animationSettings.SetShaderProperties(ref compute_cloud, ref compute_cloud_kernel_id);

       

        compute_cloud.SetInt("neighborhood_tile_size", neighborhood_tile_size);
        compute_cloud.SetFloat("previous_time", previous_time);
        compute_cloud.SetFloat("current_time", current_time);


        // Pass near and far plane;
        compute_cloud.SetFloat("nearPlane", _camera.nearClipPlane);
        compute_cloud.SetFloat("farPlane", _camera.farClipPlane);

        //Pass matricies for reprojection
        compute_cloud.SetMatrix("_CurrV", paramCurrV);
        compute_cloud.SetMatrix("_CurrVP", paramCurrVP);
        compute_cloud.SetMatrix("_PrevVP", paramPrevVP);
        compute_cloud.SetMatrix("_PrevVP_NoFlip", paramPrevVP_NoFlip);


      

        // Pass the depth texture data from the main camera 
        compute_cloud.SetTextureFromGlobal(compute_cloud_kernel_id, "_DepthTexture", "_CameraDepthTexture");

        // Pass the cloud container dimensions
        Vector3 size = container.localScale;
        int width = Mathf.CeilToInt (size.x);
        int height = Mathf.CeilToInt (size.y);
        int depth = Mathf.CeilToInt (size.z);
        compute_cloud.SetVector ("mapSize", new Vector4 (width, height, depth, 0));
        compute_cloud.SetVector ("boundsMin", container.position - container.localScale / 2);
        compute_cloud.SetVector ("boundsMax", container.position + container.localScale / 2);
        
       

        
        

        compute_cloud.SetVector ("params", cloudTestParams);
        compute_cloud.SetVector ("IsotropicLightTop", colA);
        compute_cloud.SetVector ("IsotropicLightBottom", colB);
        compute_cloud.SetVector("_DirLightDirection", directionalLightObject.transform.forward);
        compute_cloud.SetFloat  ("extinction_factor", extinction_factor);

      
    }

    private void PassMainTextureToComputeShader(RenderTexture source)
    {
        if (mainTextureBuffer == null || mainTextureBuffer.width != source.width || mainTextureBuffer.height != source.height)
        {
            // Release previous buffer if dimensions have changed
            if (mainTextureBuffer != null)
                mainTextureBuffer.Release();

            // Create a new buffer with the same dimensions as the source texture
            mainTextureBuffer = new RenderTexture(source.width, source.height, 0);
            mainTextureBuffer.enableRandomWrite = true;
            mainTextureBuffer.Create();
        }

        Graphics.Blit(source, mainTextureBuffer);
        compute_cloud.SetTexture(compute_cloud_kernel_id, "_MainTex", mainTextureBuffer);
    }

    private void PassCameraVariablesToComputeShader()
    {
        
        compute_cloud.SetVector("_WorldSpaceCameraPos", _camera.transform.position);
        compute_cloud.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        compute_cloud.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Calculate screen parameters
        float screen_width = Screen.width;
        float screen_height = Screen.height;
        screenParams = new Vector2(screen_width, screen_height);

        // Pass screen parameters to compute shader
        compute_cloud.SetVector("_ScreenParams", screenParams);
    }

    private void PassLightVariablesToComputeShader()
    {
        compute_cloud.SetVector("_WorldSpaceLightPos0", RenderSettings.sun.transform.position);
        compute_cloud.SetVector("_LightColor0;", RenderSettings.sun.color);
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Get Noise Textures:
        noise.UpdateNoise();


        compute_cloud_kernel_id = compute_cloud.FindKernel("AtmosphereRayMarch");

        BeginFrame();

        SetShaderParameters();
        PassMainTextureToComputeShader(source);
        PassCameraVariablesToComputeShader();
        PassLightVariablesToComputeShader();

        SetDebugParams();

        Render(destination);

        EndFrame();
    }

    private void BeginFrame()
    {
        // Make sure we have a current render target
        InitRenderTextures();

        

        // Set the view and projection matricies for the current and previous frame
        Matrix4x4 currentV = _camera.worldToCameraMatrix;
        //Matrix4x4 currentP = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, true);
        Matrix4x4 currentP = _camera.projectionMatrix;
        Matrix4x4 currentP_NoFlip = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
        Matrix4x4 previousV = paramInitialized ? paramCurrV : currentV;
        inverse_projection = _camera.projectionMatrix.inverse;
        inverse_view = _camera.worldToCameraMatrix.inverse;
        
        paramInitialized = true;

        paramCurrV = currentV;
        paramCurrVP = currentP * paramCurrV;
        paramPrevVP = currentP * previousV;
        paramPrevVP_NoFlip = currentP_NoFlip * previousV;


        current_time = Time.time;
    }

    public void EndFrame()
    {
       
        uint frameCounter = performanceSettings.GetFrameCounter();
        frameCounter++;

        if(frameCounter == MAX_FRAME_COUNT)
        {
            frameCounter = 0;
        }

        performanceSettings.SetFrameCounter(frameCounter);

        previous_time = current_time;

        performanceSettings.ReleaseBuffer();
    }



    private void Render(RenderTexture destination)
    {

        // ------------------------------------------------------------------------------------------ //
        //  Cloud Pass (contains motion vector calculations)
        // ------------------------------------------------------------------------------------------ //

    
        // Set textures
        compute_cloud.SetTexture(compute_cloud_kernel_id, "worldPositionTexture", worldPositionTexture);
        compute_cloud.SetTexture(compute_cloud_kernel_id, "Result", _target);
        
        // Set the thread group dimensions
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // Set the target and dispatch the compute shader
        compute_cloud.Dispatch(compute_cloud_kernel_id, threadGroupsX, threadGroupsY, 1);


        // ------------------------------------------------------------------------------------------ //
        //  Display
        // ------------------------------------------------------------------------------------------ //

        // Blit the result texture to the screen 
        Graphics.Blit(_target, destination);
    }

    private Texture2D ConvertToTexture2D(RenderTexture renderTexture)
    {
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);

        // Read the RenderTexture data into the Texture2D
        RenderTexture.active = renderTexture;
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        return texture2D;
    }

    private void OnDisable()
    {
        if (mainTextureBuffer != null)
        {
            mainTextureBuffer.Release();
           // DestroyImmediate(mainTextureBuffer);
            mainTextureBuffer = null;
        }
    }

    private void OnDestroy()
    {
        // Release and destroy the ComputeBuffer
        /*if(history_frame_buffer != null)
            history_frame_buffer.Release();
        history_frame_buffer.Dispose();*/
    }


    private void InitRenderTextures()
    {
       // Check if render texture and motion vector texture need to be created or resized
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release and recreate render texture
            if (_target != null)
                _target.Release();

            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }

        if (worldPositionTexture == null || worldPositionTexture.width != Screen.width || worldPositionTexture.height != Screen.height)
        {
            // Release and recreate motion vector texture
            if (worldPositionTexture != null)
                worldPositionTexture.Release();

            worldPositionTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            worldPositionTexture.enableRandomWrite = true;
            worldPositionTexture.Create();
        }
    }

    void SetDebugParams()
    {

        var noise = FindObjectOfType<NoiseGenerator>();
        //var weatherMapGen = FindObjectOfType<WeatherMap>();

        int debugModeIndex = 0;
        if (noise.viewerEnabled)
        {
            debugModeIndex = (noise.activeTextureType == NoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
        }
        //if (weatherMapGen.viewerEnabled)
        //{
            //debugModeIndex = 3;
        //}

        compute_cloud.SetInt("debugViewMode", debugModeIndex);
        compute_cloud.SetFloat("debugNoiseSliceDepth", noise.viewerSliceDepth);
        compute_cloud.SetFloat("debugTileAmount", noise.viewerTileAmount);
        compute_cloud.SetFloat("viewerSize", noise.viewerSize);
        compute_cloud.SetVector("debugChannelWeight", noise.ChannelMask);
        compute_cloud.SetInt("debugGreyscale", (noise.viewerGreyscale) ? 1 : 0);
        compute_cloud.SetInt("debugShowAllChannels", (noise.viewerShowAllChannels) ? 1 : 0);
    }
}