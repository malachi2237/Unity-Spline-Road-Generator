using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct PointList
{
    public List<Vector3> points;
}
public class TestSplineGeometry : MonoBehaviour
{
    public Mesh geometryTemplate;
    [SerializeField]
    public List<PointList> CurvePoints = new List<PointList>();
    private MeshFilter meshRenderer;

    private List<Vector3> waypoints = new List<Vector3>();
    private SplineGeometry sG;
    private List<Vector3>  dSpline = new List<Vector3>();
    private List<Vector3> cps = new List<Vector3>();
    private void Awake()
    {
        meshRenderer = gameObject.GetComponent<MeshFilter>();
    }

    [ContextMenu("Let's do it")]
    public void GenerateMesh()
    {
        //Mesh meshCopy = new Mesh();
        //meshRenderer = gameObject.GetComponent<MeshFilter>();

        //meshCopy.vertices = SplineGeometry.CorrectVertexOrientation(geometryTemplate, Vector3.up, Vector3.right);
        //meshCopy.normals = geometryTemplate.normals;
        //meshCopy.uv = geometryTemplate.uv;
        //meshCopy.triangles = geometryTemplate.triangles;
        //sG.SetCurves(new List<Vector3[]>(CurvePoints.Select(curveList => curveList.points.ToArray())));
        //sG.SetMeshTemplate(meshCopy);

        //meshRenderer.mesh = sG.GenerateMeshes(CurvePoints.Count)[0];

    }

    [ContextMenu("Randomly Generate Mesh")]
    public void RandomlyGenerateMesh()
    {
        float timeElapsed = Time.realtimeSinceStartup;
        Mesh meshCopy = new Mesh();
        meshRenderer = gameObject.GetComponent<MeshFilter>();

        meshCopy.vertices = SplineGeometry.CorrectVertexOrientation(geometryTemplate, Vector3.up, Vector3.forward);
        meshCopy.normals = geometryTemplate.normals;
        meshCopy.uv = geometryTemplate.uv;
        meshCopy.triangles = geometryTemplate.triangles;

        Random.InitState(System.DateTime.Now.Millisecond);
        var roadGenerator = new RoadGenerator();

        var straight = new RoadStretchProfile(3, 50.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        var switchback = new RoadStretchProfile(4, 50.0f, 0.25f, 0.75f, 10.0f, 20.0f, 1.0f);
        var bigTurn = new RoadStretchProfile(3, 50.0f, 0.1f, 0.15f, 10.0f, 20.0f, 0.0f);

        var profiles = new List<(RoadStretchProfile, float)>();
        
        profiles.Add((straight, .5f));
        profiles.Add((switchback, .25f));
        profiles.Add((bigTurn, .25f));
        roadGenerator.GenerateWaypoints(profiles, 5);

        waypoints = roadGenerator.GetWaypoints();
        //var curves = roadGenerator.GenerateCurves();
        var bSpline = new BezierSpline(waypoints);
        var temp = bSpline.CalculateDiscreteCurves();

        dSpline.Clear();
        foreach (var spline in temp)
            dSpline.AddRange(spline.Positions);

        cps = new List<Vector3>(bSpline.GetControlPoints());
        if (sG == null)
            sG = new SplineGeometry(waypoints, meshCopy);
        else
        {
            sG.SetCurves(waypoints);
            sG.SetMeshTemplate(meshCopy);
        }
        

        meshRenderer.mesh = sG.GenerateMeshes(out _,waypoints.Count - 1)[0];

        timeElapsed = Time.realtimeSinceStartup - timeElapsed;

        Debug.Log($"Generation took {timeElapsed} seconds to complete.");
    }

    [ContextMenu("Debug GetWaypoints")]
    public void DebugWaypoints()
    {
        Random.InitState(System.DateTime.Now.Millisecond);
        var roadGenerator = new RoadGenerator();
        var straight = new RoadStretchProfile(3, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        var switchback = new RoadStretchProfile(4, 10.0f, 0.0f, 0.0f, 30.0f, 60.0f, 1.0f);
        
        var profiles = new List<(RoadStretchProfile, float)>();

        profiles.Add((straight, .5f));
        profiles.Add((switchback, .5f));
        roadGenerator.GenerateWaypoints(profiles, 4);

        waypoints = roadGenerator.GetWaypoints();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        if (waypoints.Count > 0)
        {
            foreach (var point in waypoints)
                Gizmos.DrawWireSphere(point, .5f);
        }

        if (cps.Count > 0)
        {
            int i = 0;
            foreach (var point in cps)
            {
                if ((i++ / 2) % 2 == 0)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(point, .5f);
            }
        }
        if (dSpline.Count > 1)
        {
            var c = Color.green;
            c.a = 0.5f;
            Gizmos.color = c;
            for (int i = 1; i < dSpline.Count; i++)
            {
                Gizmos.DrawLine(dSpline[i - 1], dSpline[i]);
            }
        }
    }
}
