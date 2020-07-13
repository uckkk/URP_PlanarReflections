using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

// Declare type of Custom Editor
[CustomEditor(typeof(RecursiveReflectionControl))] //1
public class PlanarReflectionEditor : Editor
{
    float thumbnailWidth = 70;
    float thumbnailHeight = 70;
    public bool boolToogleButton_Ground;
    public bool boolToogleButton_Ceiling;
    public bool boolToogleButton_Left;
    public bool boolToogleButton_Right;
    public bool boolToogleButton_Forward;
    public bool boolToogleButton_Back;
    [SerializeField]
    private RecursiveReflectionControl rrc;
    private SerializedObject _getTarget;
    private List<PlanarReflectionSettings> _prs = new List<PlanarReflectionSettings>();
    private void OnEnable()
    { 
        rrc = ( RecursiveReflectionControl)target;
            _getTarget = new SerializedObject(rrc);
    }
    private void Settingsbuild(PlanarReflectionSettings planarLayer)
    {
            planarLayer.recursiveReflection = rrc.recursiveReflectionGroups;
            planarLayer.recursiveGroup = rrc.recursiveGroup;
            planarLayer.frameSkip = rrc.frameSkip;
            planarLayer.addBlackColour = rrc.addBlackColour;
            planarLayer.enableHdr = rrc.hdr;
            planarLayer.clipPlaneOffset = rrc.reflectionOffset;
            planarLayer.enableMsaa = rrc.msaa;
            planarLayer.occlusion = rrc.occlusion;
            planarLayer.shadows = rrc.shadows;
            planarLayer.resolutionMultiplier = rrc.resolutionMultiplier;
            planarLayer.reflectLayers = rrc.reflectLayers;
            _prs.Add(planarLayer);
    }
    public override void OnInspectorGUI() //2
    {
        base.OnInspectorGUI();
        _getTarget.Update();
        boolToogleButton_Ground = rrc.boolToogleButton_Ground;
        boolToogleButton_Ceiling = rrc.boolToogleButton_Ceiling;
        boolToogleButton_Left = rrc.boolToogleButton_Left;
        boolToogleButton_Right = rrc.boolToogleButton_Right;
        boolToogleButton_Back = rrc.boolToogleButton_Back;
        boolToogleButton_Forward = rrc.boolToogleButton_Forward;
        _prs = rrc.prs;
        GUILayout.Space(20f);
        GUILayout.Label("STEP #4 - Complete setup by choosing planar direction");
        GUILayout.BeginHorizontal();
        var planarLayer = new PlanarReflectionSettings();
        if (boolToogleButton_Ground == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubebottom"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Ground = true;
                planarLayer.direction = new float3(0, 1, 0);
                planarLayer.shaderPropertyName = "_PlanarGround";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Ground = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubebottom"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
        {
            boolToogleButton_Ground = rrc.boolToogleButton_Ground;
            int removalint = 999;
                for (int i =0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarGround")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {
                    boolToogleButton_Ground = false;
                    _prs.RemoveRange(removalint, 1);
                    rrc.boolToogleButton_Ground = false;
                    _getTarget.ApplyModifiedProperties();
                }
        }}
        if (boolToogleButton_Ceiling == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubetop"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Ceiling = true;
                planarLayer.direction = new float3(0, -1, 0);
                planarLayer.shaderPropertyName = "_PlanarCeiling";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Ceiling = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubetop"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                int removalint = 999;
                for (int i = 0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarCeiling")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {
                    _prs.RemoveRange(removalint, 1);
                    boolToogleButton_Ceiling = false;
                    rrc.boolToogleButton_Ceiling = false;
                    _getTarget.ApplyModifiedProperties();
                }
            }
        }
        if (boolToogleButton_Right == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cuberight"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Right = true;
                planarLayer.direction = new float3(1, 0, 0);
                planarLayer.shaderPropertyName = "_PlanarRight";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Right = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cuberight"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                
                int removalint = 999;
                for (int i = 0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarRight")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {boolToogleButton_Right = false;
                    _prs.RemoveRange(removalint, 1);
 rrc.boolToogleButton_Right = false;
 _getTarget.ApplyModifiedProperties();
                }
            }
        }
        if (boolToogleButton_Left == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeleft"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Left = true;
                planarLayer.direction = new float3(-1, 0, 0);
                planarLayer.shaderPropertyName = "_PlanarLeft";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Left = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeleft"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                int removalint = 999;
                for (int i = 0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarLeft")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {
                    _prs.RemoveRange(removalint, 1);
                    rrc.boolToogleButton_Left =false;
                    boolToogleButton_Left = false;
                    _getTarget.ApplyModifiedProperties();
                }
            }
        }
        if (boolToogleButton_Forward == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeforward"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Forward = true;
                planarLayer.direction = new float3(0, 0, 1);
                planarLayer.shaderPropertyName = "_PlanarForward";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Forward = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeforward"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                int removalint = 999;
                for (int i = 0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarForward")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {
                    _prs.RemoveRange(removalint, 1);
                    rrc.boolToogleButton_Forward = false;
                    boolToogleButton_Forward = false;
                    _getTarget.ApplyModifiedProperties();
                }
            }
        }
        if (boolToogleButton_Back == false)
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeback"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                boolToogleButton_Back = true;
                planarLayer.direction = new float3(0, 0, -1);
                planarLayer.shaderPropertyName = "_PlanarBack";
                Settingsbuild(planarLayer);
                rrc.boolToogleButton_Back = true;
                _getTarget.ApplyModifiedProperties();
            }
        }
        else
        {
            if (GUILayout.Button(Resources.Load<Texture>("Thumbnails/cubeback"),
                GUILayout.Width(thumbnailWidth), GUILayout.Height(thumbnailHeight)))
            {
                int removalint = 999;
                for (int i = 0; i < _prs.Count; i++)
                {
                    if (_prs[i].shaderPropertyName == "_PlanarBack")
                    {
                        removalint = i;
                        break;
                    }
                }
                if (removalint != 999)
                {
                    _prs.RemoveRange(removalint, 1);
                    boolToogleButton_Back = false;
                    rrc.boolToogleButton_Back = false;
                    _getTarget.ApplyModifiedProperties();
                }
            }
        }
        rrc.planarReflectionLayers = _prs.ToArray();
        _getTarget.ApplyModifiedProperties();
        EditorUtility.SetDirty(_getTarget.targetObject);
        GUILayout.EndHorizontal(); //4
    }
}
