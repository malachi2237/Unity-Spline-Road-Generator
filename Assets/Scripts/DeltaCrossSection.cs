using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A point in a <c>DeltaCrossSection</c>. Used to represent points in a cross section of geometry.
/// </summary>
public struct DeltaVertex
{
    /// <value>The position of the point. The value at the forward axis is assumed to be the axis origin.</value>
    public Vector3 Position;

    /// <value>The normal of the point.</value>
    public Vector3 Normal;

    /// <value>The initial uv coordinates of the point.</value>
    public Vector2 Uv;

    /// <value>The change in uv of the point per world unit.</value>
    public Vector2 DeltaUv;
}

/// <summary>
/// A class that provides functionality for extruding a piece of geometry.
/// </summary>
public class DeltaCrossSection
{
    private static Vector3 s_forward = Vector3.forward;

    /// <value>The cross-section represented by a graph of <c>DeltaVertex</c>. Each item is a subgraph of the cross-section ordered by ccw vertex order</value>
    private List<List<DeltaVertex>> _crossSection = new List<List<DeltaVertex>>();

    public int VertexCount
    {
        get { return _crossSection.Sum(subGraph => subGraph.Count); }
    }

    /// <summary>
    /// Initialize the <c>DeltaCrossSection</c>
    /// </summary>
    public DeltaCrossSection()
    {

    }

    /// <summary>
    /// Initializes the <c>DeltaCrossSection</c> and builds the cross section from a mesh.
    /// The same as calling <c>UseMesh</c> after initializing.
    /// </summary>
    /// <param name="inputMesh">The mesh to be analyzed.</param>
    public DeltaCrossSection(Mesh inputMesh)
    {
        UseMesh(inputMesh);
    }

    /// <summary>
    /// Analyzes a mesh and builds the cross section based on the the forward-most edge loop of the mesh.
    /// </summary>
    /// <param name="inputMesh">The mesh to be analyzed.</param>
    public void UseMesh(Mesh inputMesh)
    {
        Debug.Assert(inputMesh != null, "InputMesh must not be null");

        Vector3[] positions = inputMesh.vertices;
        Vector3[] normals = inputMesh.normals;
        Vector2[] uvs = inputMesh.uv;
        int[] triangles = inputMesh.triangles;

        List<int> crossSectionVertList = GetTargetPoints(positions);
        Dictionary<int, List<int>> csTriangleMap = GetTargetVertexTriangles(crossSectionVertList, triangles);
        List<List<int>> orderedCsGraph = BuildGraphInVertexOrder(crossSectionVertList, csTriangleMap);

        _crossSection.Clear();

        // create cross-section and set positions and normals
        foreach (var subGraph in orderedCsGraph)
        {
            var deltaSubGraph = new List<DeltaVertex>();

            foreach (var vertex in subGraph)
            {
                var newDP = new DeltaVertex();

                newDP.Position = positions[vertex];

                //position should be at the origin of the forward axis
                newDP.Position.z = 0.0f;
                newDP.Normal = normals[vertex];

                deltaSubGraph.Add(newDP);
            }

            _crossSection.Add(deltaSubGraph);
        }

        CalculateCsUvData(orderedCsGraph, GetCrossSectionPairs(orderedCsGraph, csTriangleMap, positions), uvs, positions);
    }

    /// <summary>
    /// Calculates the triangles for the mesh between n-1 and nth cross-sections
    /// </summary>
    /// <param name="n">The count of the cross-section being added. Must be greater than 0</param>
    /// <returns>A list of indices to be passed to a mesh. If n < 1, the list will be empty.</returns>
    public List<int> GetNextTriangles(int n)
    {
        var indices = new List<int>();
        var vertCount = VertexCount;
        int indexOffset = 0;

        if (n > 0)
        {
            //go through each subgraph
            for (int i = 0; i < _crossSection.Count; i++)
            {
                //assign triangles quad by quad
                for (int j = 0; j < _crossSection[i].Count - 1; j++, indexOffset++)
                {
                    indices.Add(vertCount * n + indexOffset + 1);
                    indices.Add(vertCount * (n - 1) + indexOffset + 1);
                    indices.Add(vertCount * (n - 1) + indexOffset);

                    indices.Add(vertCount * n + indexOffset);
                    indices.Add(vertCount * n + indexOffset + 1);
                    indices.Add(vertCount * (n - 1) + indexOffset);
                }
                indexOffset++;
            }
        }

        return indices;
    }

    /// <summary>
    /// Retrieves a list of the positions of the vertices in the cross-section
    /// </summary>
    /// <returns>A <c>List</c> containing the positions of the cross-section vertices</returns>
    public List<Vector3> GetPositions()
    {
        return GetCsPropertyList(v => v.Position);
    }

    /// <summary>
    /// Retrieves a list of the normals of the vertices in the cross-section
    /// </summary>
    /// <returns>A <c>List</c> containing the normals of the cross-section vertices</returns>
    public List<Vector3> GetNormals()
    {
        return GetCsPropertyList(v => v.Normal);
    }

    /// <summary>
    /// Retrieves a list of the starting uvs of the vertices in the cross-section. Nearly identical to calling <c>GetAdjustedUvs</c> with 0.0f distance.
    /// </summary>
    /// <returns>A <c>List</c> containing the starting uvs of the cross-section vertices</returns>
    public List<Vector2> GetUvs()
    {
        return GetCsPropertyList(v => v.Uv);
    }

    /// <summary>
    /// Calculates the uvs of the entire cross-section given a displacement
    /// </summary>
    /// <param name="distance">The distance from the first cross-section. Used to calculate what the change in uv is.</param>
    /// <returns>A <c>List</c> containing the distance-adjusted uvs of the cross-section vertices</returns>
    public List<Vector2> GetAdjustedUvs(float distance)
    {
        return GetCsPropertyList(v => v.Uv + Mathf.Abs(distance) * v.DeltaUv);
    }

    /// <summary>
    /// A method to retrieve a property from every <c>DeltaVertex</c> in the <c>DeltaCrossSection</c> via a selector.
    /// </summary>
    /// <typeparam name="T">The type of data to be retrieved from the cross-section</typeparam>
    /// <param name="selector">A function that takes a <c>DeltaVertex</c> as a parameter and returns data from it.</param>
    /// <returns>A list of the data retrieved from the cross-section in the order the vertices are stored</returns>
    private List<T> GetCsPropertyList<T>(System.Func<DeltaVertex, T> selector)
    {
        var propertyList = new List<T>();

        foreach (var subGraph in _crossSection)
        {
            foreach (var vertex in subGraph)
            {
                propertyList.Add(selector(vertex));
            }
        }

        return propertyList;
    }

    /// <summary>
    /// Selects the forward value of a vector. Forward is arbitrary but unique to the class.
    /// </summary>
    /// <param name="vec">The vector whose values are to be selected</param>
    /// <returns>The forward value of <c>vec</c></returns>
    private static float GetForwardValue(Vector3 vec)
    {
        return Vector3.Magnitude(Vector3.Cross(vec, s_forward));
    }

    /// <summary>
    /// Finds the vertices that will be used for the cross-section by selecting the forward-most edge loop.
    /// </summary>
    /// <param name="positions">The position array of the target mesh</param>
    /// <returns>A <c>List</c> of the indices of the cross-section vertices</returns>
    private static List<int> GetTargetPoints(Vector3[] positions)
    {
        float mostForwardValue = Mathf.Max(positions.Select(pos => pos.z).ToArray());
        var crossSectionIndices = new List<int>();

        for (int i = 0; i < positions.Length; i++)
        {
            if (Mathf.Approximately(positions[i].z, mostForwardValue))
                crossSectionIndices.Add(i);
        }

        return crossSectionIndices;
    }

    /// <summary>
    /// Creates an edge map for the <c>targetVertices</c>. Edges are represented once for each triangle in <c>tri</c>
    /// </summary>
    /// <param name="targetVertices">A list of indices in <c>tris</c></param>
    /// <param name="tris">A list of indices from a mesh.</param>
    /// <returns>An edge map where the keys are items from <c>targetVertices</c> and values are a list of edges ordered by triangle.</returns>
    private static Dictionary<int, List<int>> GetTargetVertexTriangles(List<int> targetVertices, int[] tris)
    {
        // for each triangle in tri
        //      for each vertex in tri
        //          if vertex is in targetVertices, add other vertices in tri to list if they are not also in targetVertices
        var targetTriangleMap = new Dictionary<int, List<int>>();

        targetVertices.ForEach(vert => targetTriangleMap[vert] = new List<int>());

        for (int i = 0; i < tris.Length; i += 3)
        {
            for (int j = 0; j < 3; j++)
            {
                int targetIndex = targetVertices.FindIndex(index => index == tris[i + j]);
                if (targetIndex >= 0)
                {
                    targetTriangleMap[targetVertices[targetIndex]].Add(tris[i + (j + 1) % 3]);
                    targetTriangleMap[targetVertices[targetIndex]].Add(tris[i + (j + 2) % 3]);
                }
            }
        }

        return targetTriangleMap;
    }

    /// <summary>
    /// Builds a graph containing the cross-section indices in cw vertex order
    /// </summary>
    /// <param name="targets">The list of cross-section indices</param>
    /// <param name="edgeMap">An adjacency list generated by <c>GetTargetVertexTriangles</c>. Assumes edges are in their winding order/param>
    /// <returns>A graph of the cross-section indices represented by a list of lists. Each list in the list is an unconnected subgraph of the graph.</returns>
    private List<List<int>> BuildGraphInVertexOrder(List<int> targets, Dictionary<int, List<int>> edgeMap)
    {
        var consideredVertices = new Dictionary<int, int>();
        var sortedVertices = new List<List<int>>();

        foreach (var vertex in targets)
        {
            if (!consideredVertices.ContainsKey(vertex))
            {
                var sortedVertSet = new List<int>();
                int activeVert = vertex;
                int i = 1;

                //search the edge list of current vertex for a more clockwise vertex
                //breaks when the most cw vertex is found
                while (i < edgeMap[activeVert].Count)
                {
                    //if there is a target vertex that is more cw, move search to that vertex
                    if (edgeMap.ContainsKey(edgeMap[activeVert][i]))
                    {
                        activeVert = edgeMap[activeVert][i];
                        i = 1;
                    }
                    else
                        i+=2;
                }

                i = 0;
                sortedVertSet.Add(activeVert);
                consideredVertices[activeVert] = activeVert;

                //go through the edge tree again, this time moving from most cw to least
                while (i < edgeMap[activeVert].Count)
                {
                    //if there is a target vertex that is less cw
                    if (edgeMap.ContainsKey(edgeMap[activeVert][i]))
                    {
                        activeVert = edgeMap[activeVert][i];
                        i = 0;

                        sortedVertSet.Add(activeVert);
                        consideredVertices[activeVert] = activeVert;
                    }
                    else
                        i += 2;
                }

                sortedVertices.Add(sortedVertSet);
            }
        }

        return sortedVertices;
    }

    /// <summary>
    /// Determines the vertices from which the cross-section vertices extruded
    /// </summary>
    /// <param name="csGraph">A graph of the cross-section vertices.</param>
    /// <param name="csEdgeGraph">An edge map of the target vertices. All of the indices in <c>csGraph</c> must be keys for this <c>Dictionary</c></param>
    /// <param name="positions">The position data from the input mesh</param>
    /// <returns>A graph of the vertices from which the cross-section vertices extruded. Paired vertices are in the same order as their partners in <c>csGraph</c></returns>
    private List<List<int>> GetCrossSectionPairs(List<List<int>> csGraph, Dictionary<int, List<int>> csEdgeGraph, Vector3[] positions)
    {
        var pairedVerts = new List<List<int>>();

        foreach (var subGraph in csGraph)
        {
            var subGraphPairs = new List<int>();

            foreach (var vertex in subGraph)
            {
                //Consider only distinct edges that are not a part of the target cross-section
                var edgesInDomain = new List<int>(csEdgeGraph[vertex].Distinct());
                edgesInDomain = edgesInDomain.FindAll(v => !csEdgeGraph.ContainsKey(v));

                var minEdgeDistance = float.PositiveInfinity;
                var closestEdge = 0;

                foreach (var edge in edgesInDomain)
                {
                    var dist = Vector3.Distance(positions[vertex], positions[edge]);

                    if (dist < minEdgeDistance)
                    {
                        minEdgeDistance = dist;
                        closestEdge = edge;
                    }
                }

                subGraphPairs.Add(closestEdge);
            }

            pairedVerts.Add(subGraphPairs);
        }

        return pairedVerts;
    }

    /// <summary>
    /// Calculates and assigns the uvs and deltaUv for the crossSection. Assumes that <c>_crossSection</c> is otherwise built
    /// </summary>
    /// <param name="csGraph">A graph of the cross-section vertices.</param>
    /// <param name="pairedVertices">A graph of the vertices from which the vertices in <c>csGraph</c> extruded. Assumes the pairs are in the same order ar <c>csGraph</c></param>
    /// <param name="uvs">Uv data from the input mesh</param>
    /// <param name="positions">Position data from the input mesh</param>
    private void CalculateCsUvData(List<List<int>> csGraph, List<List<int>> pairedVertices, Vector2[] uvs, Vector3[] positions)
    {
        for (int i = 0; i < csGraph.Count; i++)
        {
            for (int j = 0; j < csGraph[i].Count; j++)
            {
                DeltaVertex dV = _crossSection[i][j];

                //starting uv comes from edge loop before last
                dV.Uv = uvs[pairedVertices[i][j]];

                //calculate change in uv from paired vertex to target vertex. Then, divide by distance between the vertices to get uv/dist
                Vector2 nextUv = uvs[csGraph[i][j]];
                Vector2 uvChange = nextUv - dV.Uv;
                Vector3 posA = positions[csGraph[i][j]];
                Vector3 posB = positions[pairedVertices[i][j]];
                dV.DeltaUv = uvChange / Vector3.Distance(posA, posB);

                _crossSection[i][j] = dV;
            }
        }
    }
}
