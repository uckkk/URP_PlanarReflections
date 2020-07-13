using UnityEngine;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Entities;
using Unity.Jobs;

//Class for storing user reflection settings
[Serializable]
public class PlanarReflectionSettings
{
    public bool recursiveReflection;
    public int recursiveGroup = 1;
    public string shaderPropertyName;
    public float3 direction;
    public float clipPlaneOffset = 0.07f;
    public LayerMask reflectLayers = -1;
    public ResolutionMultipliers resolutionMultiplier;
    public bool shadows;
    public int frameSkip = 1;
    public bool occlusion;
    public bool addBlackColour;
    public bool enableHdr;
    public bool enableMsaa;
    public enum ResolutionMultipliers
    {
        Full,
        Half,
        Third,
        Quarter
    }
    public float GetScaleValue()
    {
        switch (resolutionMultiplier)
        {
            case ResolutionMultipliers.Full:
                return 1f;
            case ResolutionMultipliers.Half:
                return 0.5f;
            case ResolutionMultipliers.Third:
                return 0.33f;
            case ResolutionMultipliers.Quarter:
                return 0.25f;
            default:
                return 0.5f;
        }
    }
}
public class PlanarReflectionScript : MonoBehaviour
{
    //Local camera the script is attached to.
    private Camera _targetCamera;
    //Entity Variables
    private Camera _entityAttachedCam;
    private static EntityArchetype _cameraArchetype;
    private EntityManager _entityManager;
    //_fpsCounter used for frame skip option
    private int _fpsCounter;
    //Controls whether or not to contribute HDR elements (eg emission) to the reflection.
    private bool _currentHDRsetting;
    //Used for realtime adjustment of reflection resolution.
    private int _currentRenderTextureint;
    //Primary reflection texture.
    private  RenderTexture _reflTexture;
    //Instance of custom class used for reflection settings in the inspector.
    public PlanarReflectionSettings  planarLayerSettings = new PlanarReflectionSettings();
    //Utility methods for killing reflections cleanly.
    private void OnDisable()
    {
        Cleanup();
    }
    private void OnDestroy()
    {
        Cleanup();
    }
    private void Cleanup()
    {
        //Unsubscribe from beginCameraRendering
        RenderPipelineManager.beginCameraRendering -= ExecutePlanarReflections;
        //Release temporary textures and null out existing texture value
        if (!_reflTexture) return;
        RenderTexture.ReleaseTemporary(_reflTexture);
        _reflTexture = null;
    }
    //end of utility methods
    private void Update()
    {
        _fpsCounter++;
    } 
    private void Start()
    {    //Get reference to the default entity manager
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        //Get reference to attached camera
        _targetCamera = GetComponent<Camera>();
        //Setting up archtype for entity creation
        _cameraArchetype = _entityManager.CreateArchetype(typeof(CamObjectStruct));
        //Checking if script should allow recursive control to coordinate reflections
        if (!planarLayerSettings.recursiveReflection)
            //If not recursive, subscribe to beginCameraRendering event
            RenderPipelineManager.beginCameraRendering += ExecutePlanarReflections;
    }
    //Main kickoff point determining whether or not to let the recursivecontrol take over managing reflection coordination.
    private void ExecutePlanarReflections(ScriptableRenderContext arg1, Camera arg2)
    {
        if (planarLayerSettings.recursiveReflection)
            return;
        //Pass URP ScriptableRenderContext from beginCameraRendering camera to main work method
        ExecuteRenderSequence(arg1);
    }
    //Main method for individual reflection scripts not using recursion.
    public Camera ExecuteRenderSequence(ScriptableRenderContext src, Camera sentCamera = null, bool inverted = true, bool enableRender = true)
    { 
        if (this != null)
        {
            var cameraToUse = sentCamera;
            if (cameraToUse == null && this.gameObject != null)
                cameraToUse = _targetCamera;
            //Avoid processing cameras used for reflection probes
            if (cameraToUse != null && cameraToUse.cameraType == CameraType.Reflection)
                return null;
            //Frame skip checks
            bool skipFrame = _fpsCounter % planarLayerSettings.frameSkip != 0;
            if (skipFrame)
            {
                return null;
            }
            //Resetting FPS skip counter to avoid storing large ints over long play sessions.
            if (_fpsCounter > 1000)
            {
                _fpsCounter = 0;
            }
            //Caching intial fog state, to reset after it is disabled for reflection (NOTE:this should be unnecessary now, will look at removing)
            var fogcache = RenderSettings.fog;
            RenderSettings.fog = false;
            //Create temporary render textures and camera entity
            CreateMirrorObjects(cameraToUse, out Camera reflectionCamera);
            //Sync current reflection settings to camera entity
            UpdateCameraModes(cameraToUse, reflectionCamera);
            //Apply culling mask layer options to camera entity
            reflectionCamera.cullingMask = planarLayerSettings.reflectLayers;
            //Determine the direction of the reflection from the user specified settings.
            float3 normal = planarLayerSettings.direction;
            //Job setup
            NativeArray<Matrix4x4> resultMatrix = new NativeArray<Matrix4x4>(1, Allocator.TempJob);
            CalculateReflectionMatrixJob calculateReflectionMatrix = new CalculateReflectionMatrixJob
            {
                reflectionMat = Matrix4x4.identity, plane = new float4(normal.x, normal.y, normal.z, -planarLayerSettings.clipPlaneOffset), resultMatrix = resultMatrix
            };
            //Start reflection matrix calculation job
            JobHandle handle = calculateReflectionMatrix.Schedule();
            //Setup camera clip plane calculation job with results of previous job
            NativeArray<float4> cameraSpacePlaneResult = new NativeArray<float4>(1, Allocator.TempJob);
            CameraSpacePlaneJob cameraSpacePlaneJob = new CameraSpacePlaneJob();
            //Assign current user values
            cameraSpacePlaneJob.normal = normal;
            cameraSpacePlaneJob.resultMatrix = resultMatrix;
            cameraSpacePlaneJob.sideSign = inverted ? 1.0f : -1.0f;
            cameraSpacePlaneJob.offsetPos = normal * planarLayerSettings.clipPlaneOffset;
            if (cameraToUse != null)
            {
                cameraSpacePlaneJob.worldToCameraMatrix = cameraToUse.worldToCameraMatrix;
                cameraSpacePlaneJob.cameraSpacePlaneResult = cameraSpacePlaneResult;
                //Calculate clip plane job start
                JobHandle cameraSpaceHandle = cameraSpacePlaneJob.Schedule(handle);
                Matrix4x4 projectionMatrix = cameraToUse.projectionMatrix;
                NativeArray<Matrix4x4> matrixtemp = new NativeArray<Matrix4x4>(1, Allocator.TempJob);
                //Setup job for calculating oblique projection matrix
                MakeProjectionMatrixObliqueJob makeProjectionMatrixObliqueJob = new MakeProjectionMatrixObliqueJob();
                makeProjectionMatrixObliqueJob.matrix = projectionMatrix;
                makeProjectionMatrixObliqueJob.matrixtemp = matrixtemp;
                makeProjectionMatrixObliqueJob.cameraSpacePlaneResult = cameraSpacePlaneResult;
                //Start oblique projection job
                JobHandle makeProjectionMatrixObliqueHandle = makeProjectionMatrixObliqueJob.Schedule(cameraSpaceHandle);
                makeProjectionMatrixObliqueHandle.Complete();
                //Assign position from current used camera to entity camera
                reflectionCamera.transform.position = cameraToUse.transform.position;
                //Calculate entity cameras world to camera matrix by multiplying the main cameras world to camera matrix
                //by the results of the cameraSpacePlane job
                reflectionCamera.worldToCameraMatrix = cameraToUse.worldToCameraMatrix * resultMatrix[0];
                cameraSpacePlaneJob.cameraSpacePlaneResult = cameraSpacePlaneResult;
                projectionMatrix = matrixtemp[0];
                matrixtemp.Dispose();
                //Assign oblique projection matric job result to entity camera projection matrix
                reflectionCamera.projectionMatrix = projectionMatrix;
                //Assign rotation from current used camera to entity camera
                reflectionCamera.transform.rotation = cameraToUse.transform.rotation;
            }
            //Cache culling settings(like the fog, I don't believe this should be needed anymore, will test though)
            var oldInvertCulling = GL.invertCulling;
            //Invert culling to show the backs of objects in reflections
            GL.invertCulling = inverted;
            //Assign already setup temporary rendertexture to entity camera
            reflectionCamera.targetTexture = _reflTexture;
            if (enableRender)
            {//Manually render frame with entity camera
                UniversalRenderPipeline.RenderSingleCamera(src, reflectionCamera);
            }
            //Restore cached culling 
            GL.invertCulling = oldInvertCulling;
            if (enableRender)
            {
                //Update shader property method
                UpdateShader();
            }
            //Restore fog
            RenderSettings.fog = fogcache;
            return reflectionCamera;
        }
        else
        {
            return null;
        }
    }
    public void UpdateShader()
    {
        //Push out render result of entity camera to shader property specified by user settings (eg, _PlanarGround)
        Shader.SetGlobalTexture(planarLayerSettings.shaderPropertyName, _reflTexture); 
    }
    private void UpdateCameraModes(Camera src, Camera dest)
    { //Options to set background color to black during reflection to avoid skyboxs that would normally not be visible at that angle or through obstructions
        if (dest == null)
            return;
        if (planarLayerSettings.addBlackColour)
        {
            dest.clearFlags = CameraClearFlags.Color;
            dest.backgroundColor = new Color(0, 0, 0, 1);
        }
        else
        {
            dest.clearFlags = src.clearFlags;
            dest.backgroundColor = src.backgroundColor;
        }
        if (dest.gameObject.TryGetComponent(out UniversalAdditionalCameraData camData))
        {
            //Sync remaining user/camera settings to entity camera
            camData.renderShadows = planarLayerSettings.shadows;
            dest.nearClipPlane = src.nearClipPlane;
            dest.farClipPlane = src.farClipPlane;
            dest.orthographic = src.orthographic;
            dest.fieldOfView = src.fieldOfView;
            dest.aspect = src.aspect;
            dest.orthographicSize = src.orthographicSize;
            dest.allowHDR = planarLayerSettings.enableHdr;
            dest.allowMSAA = planarLayerSettings.enableMsaa;
            dest.useOcclusionCulling = planarLayerSettings.occlusion;
        }
    }
    //Method for determining the scaled resolution of reflections
    private int2 ReflectionResolution(Camera cam, float scale)
    {
        float scaleValue = planarLayerSettings.GetScaleValue();
        var x = (int) (cam.pixelWidth * scale * scaleValue);
        var y = (int) (cam.pixelHeight * scale * scaleValue);
        return new int2(x, y);
    }
    private void CreateMirrorObjects(Camera currentCamera, out Camera reflectionCamera)
    {    //Get current reflection resolution calculation
        var textureSize = ReflectionResolution(currentCamera, UniversalRenderPipeline.asset.renderScale);
        //Check if any user settings have been modified, if so, update reflection render texture/camera settings
        if (!_reflTexture || planarLayerSettings.enableHdr != _currentHDRsetting || _currentRenderTextureint != textureSize[0] )
        {
            if (_reflTexture)
                RenderTexture.ReleaseTemporary(_reflTexture);
            if (planarLayerSettings.enableHdr)
                _reflTexture = RenderTexture.GetTemporary(textureSize[0], textureSize[1], 24, RenderTextureFormat.DefaultHDR);
            else
                _reflTexture =  RenderTexture.GetTemporary(textureSize[0], textureSize[1], 24, RenderTextureFormat.Default);
            if (QualitySettings.antiAliasing > 0)
                _reflTexture.antiAliasing = QualitySettings.antiAliasing;
            _currentRenderTextureint = textureSize[0];
            _currentHDRsetting = planarLayerSettings.enableHdr;
        }
        if (_entityAttachedCam != null)
        {
            //If a camera entity already exists, use that.
            reflectionCamera = _entityAttachedCam;
        }
        else
        {
            //If no camera entities exist, create one from the archtype.
            var query = _entityManager.CreateEntityQuery(typeof(CamObjectStruct)).ToEntityArray(Allocator.TempJob);
            if (query.Length == 0)
            {
                Entity camEntity = _entityManager.CreateEntity(_cameraArchetype);
                GameObject go = new GameObject();
                go.AddComponent<Camera>();
                var cameraData = go.AddComponent(typeof(UniversalAdditionalCameraData)) as UniversalAdditionalCameraData;
                if (cameraData != null)
                {
                    cameraData.requiresColorOption = CameraOverrideOption.Off;
                    cameraData.requiresDepthOption = CameraOverrideOption.Off;
                    cameraData.SetRenderer(0);
                }
                //Plug in required info to the archtype
                _entityManager.SetComponentData(camEntity,
                    new CamObjectStruct
                    {
                        Cam = go.GetComponent<Camera>(),
                        Uacd = go.GetComponent<UniversalAdditionalCameraData>()
                    });
                Camera tempcam = _entityManager.GetComponentData<CamObjectStruct>(camEntity).Cam;
                reflectionCamera = tempcam;
                reflectionCamera.enabled = false;
                _entityAttachedCam = tempcam;
            }
            else
            {
                //If a camera entity exist, but is not assigned to _entityAttachedCam for some reason, assign it.
                Camera tempcam = _entityManager.GetComponentData<CamObjectStruct>(query[0]).Cam;
                reflectionCamera = tempcam;
                _entityAttachedCam = tempcam;
            } 
            query.Dispose();
        }
    }
    //Achieve angled reflection compensation through an oblique projection matrix calculation based on initial camera space plane  
    //https://en.wikipedia.org/wiki/Projection_(linear_algebra)#Oblique_projections
    [BurstCompile(CompileSynchronously = false)]
    private struct MakeProjectionMatrixObliqueJob : IJob
    {
        public NativeArray<float4> cameraSpacePlaneResult;
        private float4 _clipPlane;
        public NativeArray<Matrix4x4> matrixtemp;
        public Matrix4x4 matrix;
        public void Execute()
        {
            //Assign result of clip plane job
            _clipPlane = cameraSpacePlaneResult[0];
            float4 q;
            //Calculate sign values
            q.x = (Mathf.Sign(_clipPlane.x) + matrix[8]) / matrix[0];
            q.y = (Mathf.Sign(_clipPlane.y) + matrix[9]) / matrix[5];
            q.z = -1.0F;
            q.w = (1.05F + matrix[10]) / matrix[14];
            float4 c = _clipPlane * (2.0F / math.dot(_clipPlane, q));
            matrix[2] = c.x;
            matrix[6] = c.y;
            matrix[10] = c.z + 1.0F;
            matrix[14] = c.w;
            matrixtemp[0] = matrix;
        }
    }
    [BurstCompile(CompileSynchronously = false)]
    private struct CameraSpacePlaneJob : IJob
    {
        public NativeArray<Matrix4x4> resultMatrix;
        public float3 offsetPos;
        public float3 normal;
        public Matrix4x4 worldToCameraMatrix;
        public float sideSign;
        public NativeArray<float4> cameraSpacePlaneResult; 
        public void Execute()
        {
            worldToCameraMatrix = worldToCameraMatrix * resultMatrix[0];
            //setting up camera position and normal direction calculation to create clipping plane, based on user specific offset and direction settings
            float3 cameraPosition = worldToCameraMatrix.MultiplyPoint(offsetPos);
            float3 cameraNormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
            //calculating clipping plane area.
            cameraSpacePlaneResult[0] = new float4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -math.dot(cameraPosition, cameraNormal));
        }
    }
    
    //Standard implementation of calculating a reflection matrix, in job form
    //https://www.mathplanet.com/education/geometry/transformations/transformation-using-matrices
    [BurstCompile(CompileSynchronously = false)]
    private struct CalculateReflectionMatrixJob : IJob
    {
        public float4 plane;
        public Matrix4x4 reflectionMat;
        public NativeArray<Matrix4x4> resultMatrix;
        public void Execute()
        {
            reflectionMat.m00 = 1F - 2F * plane[0] * plane[0];
            reflectionMat.m01 = -2F * plane[0] * plane[1];
            reflectionMat.m02 = -2F * plane[0] * plane[2];
            reflectionMat.m03 = -2F * plane[3] * plane[0];
            reflectionMat.m10 = -2F * plane[1] * plane[0];
            reflectionMat.m11 = 1F - 2F * plane[1] * plane[1];
            reflectionMat.m12 = -2F * plane[1] * plane[2];
            reflectionMat.m13 = -2F * plane[3] * plane[1];
            reflectionMat.m20 = -2F * plane[2] * plane[0];
            reflectionMat.m21 = -2F * plane[2] * plane[1];
            reflectionMat.m22 = 1F - 2F * plane[2] * plane[2];
            reflectionMat.m23 = -2F * plane[3] * plane[2];
            reflectionMat.m33 = 1F;
            resultMatrix[0] = reflectionMat;
        }
    }
}
//Struct used for camera entity archtype
[Serializable]
public class CamObjectStruct : IComponentData
{ 
    public UniversalAdditionalCameraData Uacd {get;  set; } 
    public  Camera Cam {get;  set; }
}