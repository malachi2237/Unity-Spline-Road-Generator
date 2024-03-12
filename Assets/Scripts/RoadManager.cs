using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;


/// <summary>
/// A class that generates and holds connected road objects.
/// </summary>
public class RoadManager : MonoBehaviour
{
    /// <summary> The editor inputted data used to generate road geometry and populate other gameplay data for each road instance </summary>
    /// <remarks> This property should be placed in a <c>PropertyDrawer</c> class as it is only necessary for testing in-editor. </remarks>
    [SerializeField]
    private RoadMeshData _meshData = null;

    /// <summary> The editor inputted profiles that are used to define the generated shape of the road </summary>
    /// <remarks> This property should be placed in a <c>PropertyDrawer</c> class as it is only necessary for testing in-editor. </remarks>
    [SerializeField]
    private List<RoadStretchProfile> _profiles = new List<RoadStretchProfile>();

    /// <summary> The editor inputted probabilites that the corresponding member of <c>_profiles</c> will be selected to generated a stretch of road </summary>
    /// <remarks> This property should be placed in a <c>PropertyDrawer</c> class as it is only necessary for testing in-editor. </remarks>
    [SerializeField]
    private List<float> _probabilities = new List<float>();

    /// <summary> The prefab that will be instantiated for each road segment. This will hold all generated data. </summary>
    [SerializeField]
    private GameObject _roadSegmentPrefab = null;

    [SerializeField]
    private int stretchNum;

    /// <summary>
    /// Generates a continuous road with all necessary functionality. Roads are stored as children of this <c>Transform</c>.
    /// </summary>
    /// <param name="meshData"> The <c>RoadMeshData</c> containing the visual data for the generated roads </param>
    /// <param name="stretchProfiles"> A list of data used to create and place road geomety. The <c>float</c> values
    /// are the probabilities that the corresponding <c>RoadStretchProfile</c> will be used. </param>
    /// <param name="numStretches"> The number of road stretches to be generated. </param>
    public void RandomlyGenerateRoads(in RoadMeshData meshData, in List<(RoadStretchProfile, float)> stretchProfiles, int numStretches)
    {
        SplineGeometry splineGeo;
        List<Vector3> waypoints;
        List<Mesh> generatedMeshes;
        List<BezierCurve> spline;

        var correctedMesh = new Mesh();

        Debug.Assert(_roadSegmentPrefab);
        Debug.Assert(stretchProfiles.Count > 0);

        var roadGenerator = new RoadGenerator();

        correctedMesh.vertices = SplineGeometry.CorrectVertexOrientation(meshData.MeshTemplate, meshData.UpAxis, meshData.ForwardAxis);
        correctedMesh.normals = meshData.MeshTemplate.normals;
        correctedMesh.uv = meshData.MeshTemplate.uv;
        correctedMesh.triangles = meshData.MeshTemplate.triangles;

        roadGenerator.GenerateWaypoints(stretchProfiles, numStretches);
        waypoints = roadGenerator.GetWaypoints();

        splineGeo = new SplineGeometry(waypoints, correctedMesh);

        generatedMeshes = splineGeo.GenerateMeshes(out spline);

        CreateRoadObjects(spline, generatedMeshes, meshData);
        TranslateRoadsOntoSpline(waypoints);
    }

     /// <summary>
     ///  HACK
     /// </summary>
     /// <returns></returns>
    public (Vector3 pos, Quaternion rot) GetStartingLineTransform()
    {
        RoadSegmentBehavior[] roads = GetComponentsInChildren<RoadSegmentBehavior>(true);
        var cps = roads[0].GetComponent<CurveComponent>().Curve;

        return (cps.BlendPosition(1.0f), cps.BlendRotation(1.0f));
    }

    /// <summary>
    ///  HACK
    /// </summary>
    /// <returns></returns>
    public (Vector3 pos, Quaternion rot) GetFinishLineTransform()
    {
        RoadSegmentBehavior[] roads = GetComponentsInChildren<RoadSegmentBehavior>(true);
        var cps = roads[roads.Length - 1].GetComponent<CurveComponent>().Curve;

        return (cps.BlendPosition(0.0f), cps.BlendRotation(0.0f));
    }

    /// <summary>
    /// Creates <c>n</c> road segments as children of this object's transform, where <c>n</c> is the size of <c>meshes</c>.
    /// </summary>
    /// <param name="spline"> The collection of <c>BezierCurve</c>s that was used to generated the meshes in <c>meshes</c> </param>
    /// <param name="meshes"> The meshes to be assigned to the road segments </param>
    /// <param name="meshData"> The <c>RoadMeshData</c> that is being used to generate the roads </param>
    private void CreateRoadObjects(in IList<BezierCurve> spline, in IList<Mesh> meshes, in RoadMeshData meshData)
    {
        DestroyAllRoads();

        for (int i = 0; i < spline.Count; i++)
        {
            Mesh mesh = meshes[i];
            BezierCurve curve = spline[i];
            GameObject newRoadSeg = Instantiate(_roadSegmentPrefab, Vector3.zero, Quaternion.identity);
            var roadSegment = newRoadSeg.GetComponent<RoadSegmentBehavior>();

            Debug.Assert(roadSegment);

            newRoadSeg.transform.SetParent(transform);

            roadSegment.SetMeshData(mesh, meshData);
            roadSegment.AssignCurve(curve);
        }
    }

    /// <summary>
    /// Translates this objects children into position based following <c>waypoints</c>
    /// </summary>
    /// <param name="waypoints"> The curve knots which the roads should be placed on. </param>
    private void TranslateRoadsOntoSpline(in List<Vector3> waypoints)
    {
        Transform[] childrenTransforms = GetComponentsInChildren<Transform>(true);

        for (int i = 1; i < childrenTransforms.Length; i++)
        {
            childrenTransforms[i].Translate(waypoints[i - 1]);
        }
    }

    /// <summary>
    /// Immediately destroys all children of the attached object
    /// </summary>
    [ContextMenu("Remove Roads")]
    private void DestroyAllRoads()
    {
        while(transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }


    /// <summary>
    /// Generate a road using values inputted in editor
    /// </summary>
    [ContextMenu("Generate Road")]
    public void GenerateRoadInEditor()
    {
        var combinedList = new List<(RoadStretchProfile, float)>();

        for (int i = 0; i < _profiles.Count; i++)
        {
            combinedList.Add((_profiles[i], _probabilities[i]));
        }

        RandomlyGenerateRoads(_meshData, combinedList, stretchNum);
    }

}
