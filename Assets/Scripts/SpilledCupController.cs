using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal; // CRUCIAL: Gives us access to the DecalProjector component

public class SpilledCupController : MonoBehaviour
{
    [Header("Visual Effects References")]
    [Tooltip("Drag your CoffeeStream particle system here")]
    public ParticleSystem particleStream;

    [Tooltip("Drag the Decal Projector component here")]
    public DecalProjector puddleDecal;

    [Header("Puddle Animation Settings")]
    [Tooltip("The final Width, Height, and Depth (Z) of the projector")]
    public Vector3 targetPuddleSize = new Vector3(1f, 1f, 1f);

    [Tooltip("How fast the puddle spreads (lower is slower)")]
    public float puddleGrowSpeed = 0.5f;

    void Start()
    {
        // 1. Start the particle stream immediately upon spawning
        if (particleStream != null)
        {
            particleStream.Play();
        }

        // 2. Shrink the projector's size to zero initially, then start growing it
        if (puddleDecal != null)
        {
            puddleDecal.size = Vector3.zero;
            StartCoroutine(GrowPuddle());
        }
    }

    private IEnumerator GrowPuddle()
    {
        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * puddleGrowSpeed;

            // SmoothStep makes the growth look organic
            float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);

            // Modify the 'size' property of the Decal Projector directly
            puddleDecal.size = Vector3.Lerp(Vector3.zero, targetPuddleSize, smoothedProgress);

            yield return null;
        }

        // Snap to the exact target size at the very end to prevent floating point errors
        puddleDecal.size = targetPuddleSize;
    }
}