using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A behavior script which accepts and interprets data to establish a function road segment <c>GameObject</c>
/// </summary>
[RequireComponent(typeof(MeshCollider), typeof(MeshFilter), typeof(MeshRenderer))]

public class RoadSegmentBehavior : MonoBehaviour
{
    /// <summary> The data used to set the mesh, collider, and lane navigation data of the object </summary>
    private RoadMeshData _meshData = null;

    /// <summary>
    /// Sets the visible mesh and collider mesh for the object as well as additional data from <c>newMeshData</c>
    /// </summary>
    /// <param name="generatedMesh"> The mesh the attached object should use for rendering and collision </param>
    /// <param name="newMeshData"> The additional mesh data use to create <c>generatedMesh</c> </param>
    public void SetMeshData(in Mesh generatedMesh, in RoadMeshData newMeshData)
    {
        var meshFilter = GetComponent<MeshFilter>();
        var meshRenderer = GetComponent<MeshRenderer>();
        var collider = GetComponent<MeshCollider>();

        meshFilter.mesh = generatedMesh;
        collider.sharedMesh = generatedMesh;

        meshRenderer.material = newMeshData.MeshMaterial;

        _meshData = newMeshData;
    }

    /// <summary>
    /// Assigns a <c>BezierCurve</c> for the segment to use in navigation generation.
    /// </summary>
    /// <param name="curve"> The curve for the behavior to use </param>
    /// <remarks> Must be called after <c>void SetMeshData(in Mesh generatedMesh, in RoadMeshData newMeshData)</c> </remarks>
    public void AssignCurve(in BezierCurve curve)   
    {
        CurveComponent curveComp = GetComponent<CurveComponent>();
        //bool opposingLane;
        //float currentDisplacement, laneWidth;

        if (!_meshData)
        {
            Debug.Assert(false, "RoadSegmentBehavior has not been initialized with SetMeshData()");
            return;
        }

        if (!curveComp)
        {
            Debug.Assert(false, "CurveComponent not found on GameObject");
            return;
        }

        curveComp.Curve = curve;
    }
}
