using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadSampleManager : MonoBehaviour
{
    [SerializeField]
    private GameObject _roadManager = null;

    private void Start()
    {
        Random.InitState(System.DateTime.Now.Millisecond);
        RoadManager rm = _roadManager.GetComponent<RoadManager>();

        if (rm)
        {
            rm.GenerateRoadInEditor();
        }
    }
}
