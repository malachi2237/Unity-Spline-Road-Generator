using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A class used to generated extrude geometry along a set of waypoints
/// </summary>
public class SplineGeometry
{
    /// <value> The spline the geometry will be extruded along </value>
    private BezierSpline _spline;

    /// <value> Generates and stores the necessary data for extruded the geometry </value>
    private DeltaCrossSection _crossSection = new DeltaCrossSection();
    
    /// <summary>
    /// Initialize <c>SplineGeometry</c> so that it is immediately ready to be used
    /// </summary>
    /// <param name="waypoints"> The points which will make up the spline the geometry is extruded along </param>
    /// <param name="meshTemplate"> The mesh which will be extruded </param>
    public SplineGeometry(in ICollection<Vector3> waypoints, Mesh meshTemplate)
    {
        _spline = new BezierSpline(waypoints, 50);
        SetMeshTemplate(meshTemplate);
    }

    /// <summary>
    /// Creates the list of curves to be used
    /// </summary>
    /// <param name="waypoints">A collection of waypoints for new curves</param>
    public void SetCurves(in ICollection<Vector3> waypoints)
    {
        _spline = new BezierSpline(waypoints);
    }

    /// <summary>
    /// Set the new mesh to be extruded along the curve
    /// </summary>
    /// <param name="mesh">The new mesh to be used</param>
    public void SetMeshTemplate(Mesh mesh)
    {
        _crossSection.UseMesh(mesh);
    }
    
    /// <summary>
    /// Creates a set of meshes extruded along the given <c>BezierCurve</c>s. Must be called after <c>SetCurves</c> and <c>SetMeshTemplate</c> are used.
    /// </summary>
    /// <param name="spline">The set of <c>DiscreteCurve</c>s used to generate the meshes. </param>
    /// <param name="curvesPerMesh">The amount of curves that will be used to create one mesh. By default, a new mesh is created for each curve</param>
    /// <returns>A <c>List</c> of meshes that connect to form what appears to be a continuous piece of geometry</returns>
    public List<Mesh> GenerateMeshes(out List<BezierCurve> spline,int curvesPerMesh = 1)
    {
        var meshes = new List<Mesh>();
        List<DiscreteCurve> dCurves = (List<DiscreteCurve>)_spline.CalculateDiscreteCurves(1.0f);
        var csVertCount = _crossSection.VertexCount;

        List<Vector3> csPositions = _crossSection.GetPositions();
        List<Vector3> csNormals = _crossSection.GetNormals();

        var posBuffer = new List<Vector3>();
        var norBuffer = new List<Vector3>();
        var uvBuffer = new List<Vector2>();
        var triBuffer = new List<int>();

        float distanceFromFirstCs = 0.0f;

        //for each curve
        for (int i = 0; i < dCurves.Count; i += curvesPerMesh)
        {
            var newMesh = new Mesh();
            int csCount = 1;  

            Vector3 lastCurvePos = dCurves[i].Positions[0];

            //add the first cross section without adding triangles
            posBuffer.AddRange(csPositions.Select(pos => Matrix4x4.TRS(Vector3.zero, dCurves[i].Rotations[0], Vector3.one).MultiplyPoint3x4(pos)));
            norBuffer.AddRange(csNormals);
            uvBuffer.AddRange(_crossSection.GetAdjustedUvs(distanceFromFirstCs));

            //optionally allow for there to be multiple curves building a mesh
            for (int curveCounter = 0; curveCounter < curvesPerMesh; curveCounter++)
            {
                int curveIndex = i + curveCounter;

                //for each point on the curve, build a cross section
                for (int j = 1; j < dCurves[curveIndex].Length; j++)
                {
                    distanceFromFirstCs += Vector3.Distance(lastCurvePos, dCurves[curveIndex].Positions[j]);

                    posBuffer.AddRange(csPositions.Select(pos => Matrix4x4.TRS(dCurves[curveIndex].Positions[j] - dCurves[i].Positions[0], dCurves[curveIndex].Rotations[j], Vector3.one).MultiplyPoint3x4(pos)));
                    uvBuffer.AddRange(_crossSection.GetAdjustedUvs(distanceFromFirstCs));

                    norBuffer.AddRange(csNormals);
                    triBuffer.AddRange(_crossSection.GetNextTriangles(csCount++));

                    lastCurvePos = dCurves[curveIndex].Positions[j];
                }
            }

            newMesh.vertices = posBuffer.ToArray();
            newMesh.normals = norBuffer.ToArray();
            newMesh.uv = uvBuffer.ToArray();
            newMesh.triangles = triBuffer.ToArray();

            meshes.Add(newMesh);

            posBuffer.Clear();
            uvBuffer.Clear();
            norBuffer.Clear();
            triBuffer.Clear();
        }

        spline = (List<BezierCurve>)_spline.GetComposingCurves();

        return meshes;
    }

    /// <summary>
    /// A utility function to reorient a mesh to follow Unity's system
    /// </summary>
    /// <param name="mesh">The mesh to be corrected</param>
    /// <param name="upAxis">The mesh's up axis</param>
    /// <param name="forwardAxis">The meshes forward axis</param>
    /// <returns>An array of the mesh's positions but with their directions corrected</returns>
    public static Vector3[] CorrectVertexOrientation(Mesh mesh, Vector3 upAxis, Vector3 forwardAxis)
    {
        System.Func<Vector3, Vector3, Vector3, Vector3> correctionAlgorithm = (Vector3 vert, Vector3 up, Vector3 forward) =>
        {
            var rightAxis = Vector3.Cross(up, forward);
            var correctedVec = new Vector3();

            //forward
            correctedVec.z = Vector3.Dot(vert, forwardAxis);
            //up
            correctedVec.y = Vector3.Dot(vert, upAxis);
            //right
            correctedVec.x = Vector3.Dot(vert, rightAxis);

            return correctedVec;
        };

        return mesh.vertices.Select((Vector3 vert) => correctionAlgorithm(vert, upAxis, forwardAxis)).ToArray();
    }
}
