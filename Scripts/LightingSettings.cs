using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/CloudSettings/LightingSettings")]
public class LightingSettings : ScriptableObject
{
    [Header("Lighting Settings: ")]

    public float power = 200f;

    [Tooltip("Multiplier affecting the scattering density. Higher values result in denser scattering effects.")]
    public float scatteringDensityMultiplier = 0.5f;

    [Tooltip("Multiplier affecting the absorption of light passing through the clouds.")]
    public float lightAbsorptionThroughClouds = 1;

    [Tooltip("Multiplier affecting the absorption of light towards the sun.")]
    public float lightAbsorptionTowardsSun = 1;

    [Header("Light Transmittance Blending: ")]
    [Tooltip("In this case, darknessThreshold is acting as the minimum threshold. If the original value is below this threshold, it will be increased to at least this value. Then, (1 - darknessThreshold) is acting as a blending factor that determines how much of the original value is retained.")]
    [Range(0, 1)]
    public float darknessThreshold = .2f;

    [Header("Phase Function Settings: ")]
    [Tooltip("Controls the forward scattering factor. Higher values result in stronger forward scattering.")]
    [Range(0, 1)]
    public float forwardScattering = 0.1f;

    [Tooltip("Controls the back scattering factor. Higher values result in stronger back scattering.")]
    [Range(0, 1)]
    public float backScattering = .3f;

    [Tooltip("Base brightness of the scattering. Higher values increase the overall brightness of the scattering.")]
    [Range(0, 1)]
    public float baseBrightness = 0.0f;

    [Tooltip("Multiplier affecting the phase function. Adjusting this can fine-tune the appearance of the scattering.")]
    [Range(0, 1)]
    public float phaseFunctionMultiplier = 1.0f;


    [Header("Scattering Properties: ")]
    public bool useColoredScattering;


    public void SetShaderProperties(ref ComputeShader compute, ref int kernelID)
    {
        // Set Float:
        compute.SetFloat("power", power);
        compute.SetFloat("scatteringDensityMultiplier", scatteringDensityMultiplier);
        compute.SetFloat("lightAbsorptionThroughCloud", lightAbsorptionThroughClouds);
        compute.SetFloat("lightAbsorptionTowardSun", lightAbsorptionTowardsSun);
        compute.SetFloat("darknessThreshold", darknessThreshold);

        // Set Vector:
        compute.SetVector("phaseParams", new Vector4(forwardScattering, backScattering, baseBrightness, phaseFunctionMultiplier));


        if (useColoredScattering) 
            compute.EnableKeyword("COLOR_SCATTERING");
        else 
            compute.DisableKeyword("COLOR_SCATTERING");
    }

    public void Reset()
    {
        power = 200f;
        forwardScattering = 0.1f;
        backScattering = 0.3f;
        baseBrightness = 0.0f;
        phaseFunctionMultiplier = 1.0f;
    }
}
