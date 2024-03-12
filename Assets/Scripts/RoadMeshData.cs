using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <c>RoadMeshData</c> is a scriptable object that holds all the necessary data to create a gameplay-ready road segment.
/// </summary>
[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/RoadManager", order = 1)]
public class RoadMeshData : ScriptableObject
{
    /// <summary> The <c>Mesh</c> that the generated road segments will resemble. </summary>
    /// <remarks> This mesh must be a tube that is not completely connected for it to be used as a base for <c>SplineGeometry</c>. </remarks>
    public Mesh MeshTemplate;

    /// <summary> The material the roads will use. </summary>
    public Material MeshMaterial;

    /// <summary> The up axis of the supplied <c>MeshTemplate</c> in its current orientation. </summary>
    public Vector3 UpAxis;

    /// <summary> The forward axis of the supplied <c>MeshTemplate</c> in its current orientation. </summary>
    public Vector3 ForwardAxis;

    /// <summary> The width of the legally drivable area of the ride or the combined width of all lanes. </summary>
    public float DrivableWidth = 0.0f;

    /// <summary> The number of lanes supported by the <c>MeshTemplate</c> and <c>MeshMaterial</c> combination. </summary>
    public int NumberOfLanes = 0;

    /// <summary> Is this data for a one-way street? </summary>
    public bool OneWay = false;
}
