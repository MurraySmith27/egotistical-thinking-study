using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowObject : MonoBehaviour
{
    [SerializeField] private GameObject m_followObject;
    void Update()
    {
        transform.position = new Vector3(m_followObject.transform.position.x, m_followObject.transform.position.y, transform.position.z);
    }
}
