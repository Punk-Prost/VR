using UnityEngine;

public class MissionMarker : MonoBehaviour
{
    Vector3 baseScale;
    void Start() => baseScale = transform.localScale;
    void Update()
    {
        transform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
        float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.08f;
        transform.localScale = baseScale * pulse;
    }
}
