using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CombineComputeMaster : MonoBehaviour 
{
    const string headerDecorationStart = " [ ";
    const string headerDecorationEnd = " ] ";
    private const uint MAX_FRAME_COUNT = uint.MaxValue;
    
    // Render Textures:
    public RenderTexture worldPositionTexture;
    private RenderTexture mainTextureBuffer;
    private RenderTexture _target;

    // Directional Light Ref:
    public Light directionalLightObject;
    public Camera directionalLightCamera;
    private Matrix4x4 light_current_VP;

    // Generator References
    private NoiseGenerator noise;
    public DeepShadowMapGen deepShadowMapGen;


    // Comnpute Shader
    public ComputeShader combineCompute;
    private int combineKernelID;

    // Cloud Container
    public Transform container;

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

   

    // Internal
    [HideInInspector]
    public Material material;

    private Camera _camera;
    private Vector2 screenParams;


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


    // ------------------------------------------------------------------------------------------ //
    void Reset()
    {
        // Get reference to the main camera
        _camera = this.gameObject.GetComponent<Camera>();

        // Get reference to the noiseGenerator
        noise = FindObjectOfType<NoiseGenerator>();

    }

    void Clear()
    {
        paramInitialized = false;
        previous_time = 0.0f;

    }

    private void Awake()
    {

        Reset();

        // Get reference to the deepShadowMapGen and initialize it.
        deepShadowMapGen = FindObjectOfType<DeepShadowMapGen>();
        if (deepShadowMapGen == null) Debug.LogError("Error: DeepShadowMapGen not found. Line: Awake() \n Time:" + Time.time + " FrameCount: " + performanceSettings.GetFrameCounter());

      

        Clear();
    }





    private void Start()
    {
        Application.targetFrameRate = 60;
    }
  
    private void SetShaderParameters()
    {
        // Do some synchronous work that doesn't require noise or shadow map data
        // --------------------------------------------------------------------- //

        //Set PerformanceSettings:
        performanceSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

        // Set RayMarchSettings:
        rayMarchSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

        // Set LightingSettings:
        lightingSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

        // Set CloudCoverageSettings:
        cloudCoverageSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);

        // Set AnimationSettings:
        animationSettings.SetShaderProperties(ref combineCompute, ref combineKernelID);


        // Wait for tasks to complete 
        // --------------------------------------------------------------------- //

        //await Task.WhenAll(noiseGenerationTask);


        // Do some synchronous work that after getting noise and shadow map data
        // --------------------------------------------------------------------- //

        deepShadowMapGen.SetShaderProperties(ref combineCompute, ref combineKernelID);

        // Set BaseShapeSettings:
        shapeSettings.SetShaderProperties(ref combineCompute, ref combineKernelID, ref noise);





        combineCompute.SetFloat("previous_time", previous_time);
        combineCompute.SetFloat("current_time", current_time);


        // Pass near and far plane;
        combineCompute.SetFloat("nearPlane", _camera.nearClipPlane);
        combineCompute.SetFloat("farPlane", _camera.farClipPlane);

        //Pass matricies for reprojection
        combineCompute.SetMatrix("_CurrV", paramCurrV);
        combineCompute.SetMatrix("_CurrVP", paramCurrVP);
        combineCompute.SetMatrix("_PrevVP", paramPrevVP);
        combineCompute.SetMatrix("_PrevVP_NoFlip", paramPrevVP_NoFlip);


       

        // Pass the depth texture data from the main camera 
        combineCompute.SetTextureFromGlobal(combineKernelID, "_DepthTexture", "_CameraDepthTexture");

        // Pass the cloud container dimensions
        Vector3 size = container.localScale;
        int width = Mathf.CeilToInt (size.x);
        int height = Mathf.CeilToInt (size.y);
        int depth = Mathf.CeilToInt (size.z);
        combineCompute.SetVector ("mapSize", new Vector4 (width, height, depth, 0));
        combineCompute.SetVector ("boundsMin", container.position - container.localScale / 2);
        combineCompute.SetVector ("boundsMax", container.position + container.localScale / 2);
        

        combineCompute.SetVector("_DirLightDirection", directionalLightObject.transform.forward);

        combineCompute.SetMatrix("Light_VP", light_current_VP);
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
        combineCompute.SetTexture(combineKernelID, "_MainTex", mainTextureBuffer);
    }

    private void PassCameraVariablesToComputeShader()
    {
        
        combineCompute.SetVector("_WorldSpaceCameraPos", _camera.transform.position);
        combineCompute.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        combineCompute.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Calculate screen parameters
        float screen_width = Screen.width;
        float screen_height = Screen.height;
        screenParams = new Vector2(screen_width, screen_height);

        // Pass screen parameters to compute shader
        combineCompute.SetVector("_ScreenParams", screenParams);
    }

    private void PassLightVariablesToComputeShader()
    {
        combineCompute.SetVector("_WorldSpaceLightPos0", RenderSettings.sun.transform.position);
        combineCompute.SetVector("_LightColor0;", RenderSettings.sun.color);
    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        

        combineKernelID = combineCompute.FindKernel("AtmosphereRayMarch");

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
        // Update Noise Gnerator using Asyncornous Task: 
        //noiseGenerationTask = noise.UpdateNoiseAsync();
        noise.UpdateNoise();

        // Update DeepShadowMap Generator using Syncronous Update. Can't do async, have to do calculations on main thread:
        //deepShadowMapGen.Initialize();
        deepShadowMapGen.UpdateMap();





        // Make sure we have a current render target
        InitRenderTextures();

        Matrix4x4 light_current_V = directionalLightCamera.worldToCameraMatrix;
        Matrix4x4 light_current_P = directionalLightCamera.projectionMatrix;

        light_current_VP = light_current_P * light_current_V;

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
        combineCompute.SetTexture(combineKernelID, "worldPositionTexture", worldPositionTexture);
        combineCompute.SetTexture(combineKernelID, "Result", _target);
        
        // Set the thread group dimensions
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // Set the target and dispatch the compute shader
        combineCompute.Dispatch(combineKernelID, threadGroupsX, threadGroupsY, 1);


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

    void OnDestroy()
    {
        
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

    }

    void SetDebugParams()
    {

        var noise = FindObjectOfType<NoiseGenerator>();
        var deepShadowMapGen = FindObjectOfType<DeepShadowMapGen>();

        int debugModeIndex = 0;
        if (noise.viewerEnabled)
        {
            debugModeIndex = (noise.activeTextureType == NoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
        }
        if (deepShadowMapGen.viewerEnabled)
        {
            debugModeIndex = 3;
        }

        combineCompute.SetInt("debugViewMode", debugModeIndex);
        combineCompute.SetFloat("debugNoiseSliceDepth", noise.viewerSliceDepth);
        combineCompute.SetFloat("debugTileAmount", noise.viewerTileAmount);
        combineCompute.SetFloat("viewerSize", noise.viewerSize);
        combineCompute.SetVector("debugChannelWeight", noise.ChannelMask);
        combineCompute.SetInt("debugGreyscale", (noise.viewerGreyscale) ? 1 : 0);
        combineCompute.SetInt("debugShowAllChannels", (noise.viewerShowAllChannels) ? 1 : 0);
    }
}