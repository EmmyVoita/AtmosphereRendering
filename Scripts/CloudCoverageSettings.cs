using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/CloudCoverageSettings")]
public class CloudCoverageSettings : ScriptableObject
{
    [Header("Cloud Coverage Texture Settings: ")]
    public Texture2D cloudCoverageTexture;
    public Vector2 coverageOffset;
    public Vector2 coverageTiling;


    [Header("Cloud Coverage Texture Step: ")]
    public bool useTextureStep = false;
    [Range(0, 1)]
    public float coverageTextureStep = 0.1f;
    [Range(-1, 1)]
    public float coverageTextureOffset = 0.0f;



    [Header("Cloud Coverage: ")]
    [Range(0, 1)]
    public float bottomHeight = .25f;
    [Range(0, 1)]
    public float topHeight = .75f;


    public float altitude_gradient_power_1 = 1;
    public float altitude_gradient_power_2 = 1;


    [Range(0, 10)]
    public float low_altitude_multiplier_influence = 0.1f;

    public Texture height_gradient;

    public Texture density_gradient;
    public float density_gradient_scalar = 1.0f;


    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        compute.SetBool("useCoverageTextureStep", useTextureStep);


        // Set Float:
        compute.SetFloat("bottomHeight", bottomHeight);
        compute.SetFloat("topHeight", topHeight);
        compute.SetFloat("coverageTextureDensityOffset", coverageTextureOffset);

        compute.SetFloat("coverageTextureStep", coverageTextureStep);
        compute.SetFloat("altitude_gradient_power_1", altitude_gradient_power_1);
        compute.SetFloat("altitude_gradient_power_2", altitude_gradient_power_2);
        compute.SetFloat("low_altitude_multiplier_influence", low_altitude_multiplier_influence);
        compute.SetFloat("density_gradient_scalar", density_gradient_scalar);

        // Set Vector:
        compute.SetVector("coverageTiling", coverageTiling);
        compute.SetVector("coverageOffset", coverageOffset);
        // Set Texture:
        compute.SetTexture(kernelID, "CloudCoverage", cloudCoverageTexture);
        compute.SetTexture(kernelID, "HeightGradient", height_gradient);
        compute.SetTexture(kernelID, "DensityGradient", density_gradient);
    }
}

