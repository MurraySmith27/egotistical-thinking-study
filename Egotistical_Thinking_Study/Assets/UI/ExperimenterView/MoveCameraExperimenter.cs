
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MoveCameraExperimenter : MonoBehaviour
{
    [SerializeField] private float distancePerGridUnit = 7f;

    [SerializeField] private float scrollSpeed = 0.1f;

    private Vector2 _originalCameraPosition;

    private float _maxCameraStrayDistance = 10f;

    private bool _isDragging = false;
    
    private Vector2 _draggingStartPoint;

    private Vector2 _draggingStartPosition;

    private bool _initialized = false;
    
    public void Inititalize()
    {
        _originalCameraPosition = transform.position;

        _maxCameraStrayDistance = Mathf.Max(MapGenerator.Instance.map.Count, MapGenerator.Instance.map[0].Count) *
                                  distancePerGridUnit / 2f;

        _initialized = true;
    }

    public void OnMouseDown()
    {
        if (!_isDragging)
        {
            _isDragging = true;

            _draggingStartPoint = Input.mousePosition;

            _draggingStartPosition = transform.position;
        }
    }

    public void OnMouseUp()
    {
        _isDragging = false;
    }

    void Update()
    {
        if (_initialized) {
            if (Input.GetMouseButtonDown(0))
            {
                OnMouseDown();
            }

            if (Input.GetMouseButtonUp(0))
            {
                OnMouseUp();
            }
            
            if (_isDragging)
            {
                Vector2 difference = ((Vector2)Input.mousePosition - _draggingStartPoint) / new Vector2(Screen.width, Screen.height);

                Vector2 newDistance = difference * scrollSpeed;
                Vector2 newCameraPosition = _draggingStartPosition - newDistance;

                if (Vector3.Distance(newCameraPosition, _originalCameraPosition) > _maxCameraStrayDistance)
                {
                    newCameraPosition = (newCameraPosition - _originalCameraPosition).normalized * _maxCameraStrayDistance + _originalCameraPosition;
                }

                transform.position = new Vector3(newCameraPosition.x, newCameraPosition.y, transform.position.z);
            }
        }
    }
}
