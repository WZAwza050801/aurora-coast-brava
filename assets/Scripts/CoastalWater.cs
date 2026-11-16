using UnityEngine;

public sealed class CoastalWater : MonoBehaviour
{
    public float normalSpeed = 0.012f;
    private Material waterMaterial;
    private Vector2 offset;
    private void Start() { waterMaterial = GetComponent<Renderer>().material; }
    private void Update()
    {
        if (!waterMaterial) return;
        offset += new Vector2(normalSpeed, normalSpeed * 0.63f) * Time.deltaTime;
        waterMaterial.SetTextureOffset("_NormalMap", offset);
        waterMaterial.SetTextureOffset("_BaseColorMap", offset * 0.18f);
    }
}
