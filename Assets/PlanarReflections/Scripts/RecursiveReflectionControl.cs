using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
public class RecursiveReflectionControl : MonoBehaviour
{
    [Header("STEP #1 - Planar Reflection Component Settings")]
    public float reflectionOffset;
    public bool shadows =true;
    public bool occlusion =true;
    public bool msaa =true;
    public bool hdr =true;
    public LayerMask reflectLayers = -1;
    public PlanarReflectionSettings.ResolutionMultipliers resolutionMultiplier = PlanarReflectionSettings.ResolutionMultipliers.Full;
    [Space] [Header("STEP #2 - Additional Setup Options")]
    public bool addBlackColour;
    [Range(1, 100)] public int frameSkip = 1;
    [Range(1, 10)]
        public int msaaRecursiveCutoff = 1;
        [Space]
    [Header("EXPERIMENTAL -- STEP #3 - Recursive Planar Reflection Component Setup")]
   public bool recursiveReflectionGroups;
   public int recursiveGroup = 1;
    [Range(1, 5)]
    public int levelsOfRecursion = 1;
    [Range(1, 10)]
        public int levelsOfShadowRecursion = 1;
    private IList<PlanarReflectionScript> _planarReflectionScripts = new List<PlanarReflectionScript>();
    private PlanarReflectionScript[,] _planarReflectionScripts_RenderCopy;
    [SerializeField,Header("Active Reflection Layers")]
    public PlanarReflectionSettings[] planarReflectionLayers;
//////////editoronly//////////////////
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Ground;
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Ceiling;
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Left;
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Right;
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Forward;
    [SerializeField, HideInInspector]
    public bool boolToogleButton_Back;
    [SerializeField, HideInInspector]
    public List<PlanarReflectionSettings> prs = new List<PlanarReflectionSettings>();
    /// ///////////////////////////
    void Start()
    {
        foreach (PlanarReflectionSettings p in planarReflectionLayers)
        {
            PlanarReflectionScript script = gameObject.AddComponent<PlanarReflectionScript>();
            var pls = script.planarLayerSettings;
            pls.direction = p.direction;
            pls.shaderPropertyName = p.shaderPropertyName;
            pls.frameSkip = p.frameSkip == 0 ? frameSkip : p.frameSkip;
            pls.shadows = p.shadows;
            pls.reflectLayers = p.reflectLayers;
            pls.resolutionMultiplier = p.resolutionMultiplier;
            pls.clipPlaneOffset = p.clipPlaneOffset;
            pls.recursiveReflection = p.recursiveReflection;
            pls.recursiveGroup = p.recursiveGroup;
            pls.occlusion = p.occlusion;
            pls.addBlackColour = p.addBlackColour;
            pls.enableHdr = p.enableHdr;
            pls.enableMsaa = p.enableMsaa;
        }

        if (!recursiveReflectionGroups) return;
        InitializeProperties();
        _cameraList = new Camera[_planarReflectionScripts.Count + 1];
        RenderPipelineManager.beginCameraRendering += ExecutePlanarReflections;
    }
   
    int GetNextCamIndex(int camIndex)
    {
        camIndex += 1;
        if (camIndex >= _planarReflectionScripts.Count)
            return 0;
        return camIndex;
    }

    private Camera[] _cameraList;
    private void ExecutePlanarReflections(ScriptableRenderContext arg1, Camera arg2)
    {
        if (this != null &&  _planarReflectionScripts.Count < 3 || levelsOfRecursion == 1)
        {
            for (int eachCam = 0; eachCam < _planarReflectionScripts.Count; eachCam++)
            {
                _cameraList = new Camera[levelsOfRecursion];
                var nextCamIndex = eachCam;
                _cameraList[0] = null;
                for (int eachDepth = 1; eachDepth < levelsOfRecursion; eachDepth++)
                {
                    if (_planarReflectionScripts[nextCamIndex] != null)
                        _cameraList[eachDepth] = _planarReflectionScripts[nextCamIndex].ExecuteRenderSequence(arg1, _cameraList[eachDepth - 1],  true, false);
                    nextCamIndex = GetNextCamIndex(nextCamIndex);
                }
                nextCamIndex = eachCam;
                if (levelsOfRecursion % 2 == 0)
                    nextCamIndex = GetNextCamIndex(nextCamIndex);
                for (int eachDepth = levelsOfRecursion - 1; eachDepth >= 0; eachDepth--)
                {
                    if (eachDepth == levelsOfRecursion - 1)
                    {
                        _planarReflectionScripts_RenderCopy[nextCamIndex, eachDepth].planarLayerSettings.reflectLayers = -1;
                    }
                    if (_planarReflectionScripts_RenderCopy[nextCamIndex, eachDepth] != null)
                        _planarReflectionScripts_RenderCopy[nextCamIndex, eachDepth].ExecuteRenderSequence(arg1,_cameraList[eachDepth],  nextCamIndex == eachCam);
                    nextCamIndex = GetNextCamIndex(nextCamIndex);
                }
            }
            for (int eachCam = 0; eachCam < _planarReflectionScripts.Count; eachCam++)
            {
                _planarReflectionScripts_RenderCopy[eachCam, 0].UpdateShader();
            }
        }
        else
        {
            for (int eachCam = 0; eachCam < _planarReflectionScripts.Count; eachCam++)
            {
                _cameraList[0] = null;
                _cameraList[1] =
                    _planarReflectionScripts[eachCam].ExecuteRenderSequence(arg1);
                var nextCam = eachCam;
                for (int toDrawCam = 0; toDrawCam < _planarReflectionScripts.Count; toDrawCam++)
                {
                    if (eachCam == toDrawCam)
                    {
                        continue;
                    }
                    nextCam = GetNextCamIndex(nextCam);
                    _planarReflectionScripts_RenderCopy[nextCam, 1].ExecuteRenderSequence(arg1,_cameraList[1]);
                }
                _planarReflectionScripts_RenderCopy[eachCam, 0].ExecuteRenderSequence(arg1);
            }
            for (int eachCam = 0; eachCam < _planarReflectionScripts.Count; eachCam++)
            {
                _planarReflectionScripts_RenderCopy[eachCam, 0].UpdateShader();
            }
        }
    } 
    private void InitializeProperties()
    {
        _planarReflectionScripts = GetComponents<PlanarReflectionScript>().Where(prsitem => prsitem.planarLayerSettings.recursiveReflection && prsitem.planarLayerSettings.recursiveGroup == recursiveGroup).ToList();
        _planarReflectionScripts_RenderCopy = new PlanarReflectionScript[_planarReflectionScripts.Count, levelsOfRecursion];
        for (int camIndex = 0; camIndex < _planarReflectionScripts.Count; camIndex++)
        {
            for (int depth = 0; depth < levelsOfRecursion; depth++)
            {
                var copy = gameObject.AddComponent<PlanarReflectionScript>();
                copy.planarLayerSettings = _planarReflectionScripts[camIndex].planarLayerSettings;
                if (_planarReflectionScripts[camIndex].planarLayerSettings.shadows == false && levelsOfShadowRecursion > depth)
                {
                    copy.planarLayerSettings.shadows = false;
                }
                else
                {
                    copy.planarLayerSettings.shadows = true;
                }
                if (_planarReflectionScripts[camIndex].planarLayerSettings.enableMsaa && msaaRecursiveCutoff > depth)
                {
                    copy.planarLayerSettings.enableMsaa = true;
                }
                else
                {
                    copy.planarLayerSettings.enableMsaa = false;
                }
                _planarReflectionScripts_RenderCopy[camIndex, depth] = copy;
            }
        }
    }
}
