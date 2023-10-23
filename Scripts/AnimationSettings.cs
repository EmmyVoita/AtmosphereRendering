using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/AnimationSettings")]
public class AnimationSettings : ScriptableObject
{

    public float timeScale = 1;
    public float baseSpeed = 1;
    public float detailSpeed = 2;

    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        // Set Float:
        compute.SetFloat("timeScale", (Application.isPlaying) ? timeScale : 0);
        compute.SetFloat("baseSpeed", baseSpeed);
        compute.SetFloat("detailSpeed", detailSpeed);
    }
}