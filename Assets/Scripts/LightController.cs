using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightController : MonoBehaviour
{
    private new Transform transform;

    void Start()
    {
        transform = GetComponent<Transform>();
    }

    void Update()
    {
        transform.position = new Vector3(4.0f * Mathf.Cos(Time.frameCount / 60.0f * 2.0f), 8.0f, 4.0f * Mathf.Sin(Time.frameCount / 60.0f * 2.0f));
    }
}
