using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagnifyingObject : MonoBehaviour
{
    static readonly int ObjScreenPos = Shader.PropertyToID("_ObjScreenPos");

    Renderer _renderer;
    Material _material;
    Camera _cam;
    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _material = _renderer.material;
        _cam = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 screenPoint = _cam.WorldToScreenPoint(transform.position);
        screenPoint.x = screenPoint.x / Screen.width;
        screenPoint.y = screenPoint.y / Screen.height;
        _material.SetVector(ObjScreenPos, screenPoint);
    }
}
