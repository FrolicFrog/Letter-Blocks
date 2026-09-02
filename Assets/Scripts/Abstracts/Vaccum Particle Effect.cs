using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class ProceduralPowerfulVacuumURP : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("The core color of the vacuum suction streaks.")]
    public Color effectColor = new Color(0.3f, 0.8f, 1f, 1f); // Default to vibrant blue

    [Header("Shape Settings")]
    [Tooltip("The angle of the suction cone.")]
    public float coneAngle = 35f;
    [Tooltip("The base radius of the suction cone.")]
    public float coneRadius = 3.5f;

    void Start()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = GetComponent<ParticleSystemRenderer>();

        // 1. Core Main Module
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-45f, -25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);

        // Apply the public color variable here
        main.startColor = effectColor;

        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;

        // 2. Emission Module
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 150f;

        // 3. Shape Module
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;

        // Apply the public shape variables here
        shape.angle = coneAngle;
        shape.radius = coneRadius;

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        // 4. Size Over Lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

        // 5. Color Over Lifetime
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            // Use white keys so the opacity gradient smoothly multiplies against your custom effectColor
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.15f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // 6. Renderer Module
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.cameraVelocityScale = 0f;
        psRenderer.velocityScale = 0.05f;
        psRenderer.lengthScale = 6f;

        // 7. URP Additive Material Generation
        if (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.name == "Default-Material")
        {
            Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticleShader != null)
            {
                Material additiveMat = new Material(urpParticleShader);
                additiveMat.SetFloat("_Surface", 1);
                additiveMat.SetFloat("_Blend", 0);
                additiveMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                additiveMat.SetFloat("_DstBlend", (float)BlendMode.One);
                additiveMat.SetFloat("_ZWrite", 0);
                additiveMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                psRenderer.sharedMaterial = additiveMat;
            }
        }

        ps.Stop();
        ps.Play();
    }
}