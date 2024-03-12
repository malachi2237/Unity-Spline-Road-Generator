using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Struct <c>DiscreteCurve</c> models a curve as a discrete set of positions and rotations
/// </summary>
public readonly struct DiscreteCurve
{
    /// <value>Property <c>Positions</c> defines the set of positions </value>
    public readonly System.Collections.ObjectModel.ReadOnlyCollection<Vector3> Positions;

    /// <value>Property <c>Rotations</c> defines the set of rotations </value>
    public readonly System.Collections.ObjectModel.ReadOnlyCollection<Quaternion> Rotations;

    /// <value> The number of points used to represent the curve</value>
    public readonly int Length;

    public DiscreteCurve(Vector3[] positions, Quaternion[] rotations)
    {
        Positions = System.Array.AsReadOnly(positions);
        Rotations = System.Array.AsReadOnly(rotations);
        Length = positions.Length;
    }
}

/// <summary>
/// Class <c>BezierCurve</c> models a bezier curve by calculating points along it or by calulating a list of points representing itself.
/// </summary>
[System.Serializable]
public class BezierCurve
{
    /// <summary>
    /// The length of the curve in world units
    /// </summary>
    public float Length { get; private set; }

    /// <summary>
    /// The number of control points which define this curve
    /// </summary>
    public int Count
    { 
        get { return _controlPoints != null ? _controlPoints.Count : 0; }
        private set { } 
    }

    /// <summary>
    /// The starting knot of the curve. Also considered the first control point
    /// </summary>
    public Vector3 InitialKnot
    {
        get { return _controlPoints[0]; }
        private set { }
    }

    /// <summary>
    /// The final knot of the curve. Also considered the final control point
    /// </summary>
    public Vector3 TerminalKnot
    {
        get { return _controlPoints[_controlPoints.Count - 1]; }
        private set { }
    }

    /// <summary>
    /// A hardcorded array of the first 17 factorials to reduce calculations
    /// </summary>
    private static float[] s_factorial = new float[]
   {
        1.0f,
        1.0f,
        2.0f,
        6.0f,
        24.0f,
        120.0f,
        720.0f,
        5040.0f,
        40320.0f,
        362880.0f,
        3628800.0f,
        39916800.0f,
        479001600.0f,
        6227020800.0f,
        87178291200.0f,
        1307674368000.0f,
        20922789888000.0f,
   };

    /// <summary>
    /// List of control points for the curve.
    /// </summary>
    [SerializeField]
    private List<Vector3> _controlPoints;

    /// <summary>
    /// Defines the amount of segments in a discrete representation of the <c>BezierCurve</c>
    /// </summary>
    [SerializeField]
    private int _numSegments = 0;

    /// <summary>
    /// A list of control points which define the derivative of this curve
    /// </summary>
    private List<Vector3> _derivativeControlPoints;

    /// <summary>
    /// Gets a control point composing the curve
    /// </summary>
    /// <param name="i"> The index of the control point </param>
    /// <returns> The <c>i</c>th control point defining the curve </returns>
    public Vector3 this[int i] => _controlPoints[i];

    /// <summary>
    /// Initializes the <c>BezierCurve</c>
    /// </summary>
    /// <param name="points">A sequence of knots and control points for the <c>BezierCurve</c>. M</param>
    /// <param name="maxSegments">Sets the count of discrete segments</param>
    /// <param name="previous">Sets the previous connected <c>BezierCurve</c></param>
    /// <param name="next"> Sets the next connected <c>BezierCurve</c></param>
    public BezierCurve(in ICollection<Vector3> points, in int maxSegments = 100)
    {
        Debug.Assert(points != null, "BezierCurve initialized with 'null' point List");

        _controlPoints = new List<Vector3>(points);

        if (maxSegments > 1)
            _numSegments = maxSegments;

        CalculateDerivative();
        CalculateLength();
    }

    /// <summary>
    /// Initialize the curve with a set of knots and control points.
    /// </summary>
    /// <param name="knots">An <c>IList</c> of exactly two points representing the beginning an ending knots</param>
    /// <param name="controlPoints">Points representing the control points for the curve. Can be empty.</param>
    /// <param name="maxSegments">Sets the count of discrete segments</param>
    public BezierCurve(in IList<Vector3> knots, in ICollection<Vector3> controlPoints, in int maxSegments)
    {
        Debug.Assert(knots.Count == 2, "BezierCurve must be initizalized with exactly two knots");
        _controlPoints = new List<Vector3>();

        _controlPoints.Add(knots[0]);
        _controlPoints.AddRange(controlPoints);
        _controlPoints.Add(knots[1]);

        _numSegments = maxSegments;

        CalculateDerivative();
        CalculateLength();
    }

    public Vector3[] GetControlPoints()
    {
        return _controlPoints.ToArray();
    }

    /// <summary>
    /// Calculates the positions of the curve with maximum precision
    /// </summary>
    /// <returns>A list containing evenly distributed positions along the <c>BezierCurve</c></returns>
    public List<Vector3> GetComposingPoints()
    {
        List<Vector3> pointList = new List<Vector3>();

        for (int i = 0; i <= _numSegments; i++)
        {
            pointList.Add(BlendPosition((float)i / (float)_numSegments));
        }

        return pointList;
    }

    /// <summary>
    /// Calculates the rotations of the curve with maximum precision
    /// </summary>
    /// <returns>A list containing evenly distributed rotations along the <c>BezierCurve</c></returns>
    public List<Quaternion> GetComposingPointRotations()
    {
        List<Quaternion> pointList = new List<Quaternion>();

        for (int i = 0; i <= _numSegments; i++)
        {
            pointList.Add(BlendRotation((float)i / (float)_numSegments));
        }

        return pointList;
    }

    /// <summary>
    /// Calculates an optimisted list of composing positions for the curve
    /// </summary>
    /// <param name="angularPrecision">The maximum amount a segment may turn, in degrees.</param>
    /// <returns>An optimised list of positions on the curve.</returns>
    public List<Vector3> GetOptimizedComposingPoints(float angularPrecision = 5.0f)
    {
        List<Vector3> allPoints = GetComposingPoints();
        List<Quaternion> allRotations = GetComposingPointRotations();

        List<Vector3> optimizedList = new List<Vector3>();

        float currentAngle = 0.0f;

        optimizedList.Add(allPoints[0]);

        for (int i = 0, j = 1; j < allPoints.Count; i++, j++)
        {
            float angle = Quaternion.Angle(allRotations[i], allRotations[j]);
            currentAngle += angle;

            if (currentAngle >= angularPrecision || j == allPoints.Count - 1)
            {
                optimizedList.Add(allPoints[j]);
                currentAngle = 0.0f;
            }
        }

        return optimizedList;
    }

    /// <summary>
    /// Calculates an optimized discrete representation of the curve.
    /// </summary>
    /// <param name="angularPrecision">The maximum amount a segment may turn, in degrees.</param>
    /// <returns>The optimized <c>DiscreteCurve</c> representing the curve</returns>
    public DiscreteCurve GetOptimizedDiscreteCurve(float angularPrecision = 5.0f)
    {
        float minRotation = angularPrecision;

        List<Vector3> allPoints = GetComposingPoints();
        List<Quaternion> allRotations = GetComposingPointRotations();

        DiscreteCurve optimizedList;

        List<Vector3> optimizedPositions = new List<Vector3>();
        List<Quaternion> optimizedRotations = new List<Quaternion>();

        float currentAngle = 0.0f;

        optimizedPositions.Add(allPoints[0]);
        optimizedRotations.Add(allRotations[0]);

        for (int i = 0, j = 1; j < allPoints.Count; i++, j++)
        {
            float angle = Quaternion.Angle(allRotations[i], allRotations[j]);
            currentAngle += angle;

            if (currentAngle >= minRotation || j == allPoints.Count - 1)
            {
                optimizedPositions.Add(allPoints[j]);
                optimizedRotations.Add(allRotations[j]);
                currentAngle = 0.0f;
            }
        }

        optimizedList = new DiscreteCurve(optimizedPositions.ToArray(), optimizedRotations.ToArray());

        return optimizedList;
    }

    /// <summary>
    /// Populates <c>_derivativeControlPoints</c> with the positions of the derivative control points
    /// </summary>
    private void CalculateDerivative()
    {
        _derivativeControlPoints = new List<Vector3>();

        for (int i = 0; i < _controlPoints.Count - 1; i++)
        {
            _derivativeControlPoints.Add((_controlPoints.Count - 1) * (_controlPoints[i + 1] - _controlPoints[i]));
        }
    }

    /// <summary>
    /// The blending function for the bezier curve. Call once for each control point.
    /// </summary>
    /// <param name="u">The distance along the curve. Restricted to 0.0f <= <c>u</c> <= 1.0f</param>
    /// <param name="i">An integer representing the index of the control point that is being blended</param>
    /// <returns>A scalar representing the influence the control point given by <c>i</c>.</returns>
    private float BlendingFunction(float u, int i, int n)
    {
        u = Mathf.Clamp01(u);

        float choose = (float)s_factorial[n] / (s_factorial[i] * s_factorial[n - i]);
        float firstTerm = Mathf.Pow(1 - u, n - i);
        float secondTerm = Mathf.Pow(u, i);
        return choose * firstTerm * secondTerm;
    }

    /// <summary>
    /// Calculates the world position of a point along the curve.
    /// </summary>
    /// <param name="u">The distance along the curve. Restricted to 0.0f <= <c>u</c> <= 1.0f</param>
    /// <returns>The world position <c>u</c> distance along curve.</returns>
    public Vector3 BlendPosition(float u)
    {
        Vector3 returnPoint = new Vector3(0, 0, 0);

        if (u <= 0.0f)
            return InitialKnot;
        else if (u >= 1.0f)
            return TerminalKnot;

        for (int i = 0; i < _controlPoints.Count; i++)
        {
            //for each control point, calculate x, y, and z, given by P(n) * B(u)
            float blend = BlendingFunction(u, i, _controlPoints.Count - 1);

            returnPoint += _controlPoints[i] * blend;
        }

        return returnPoint;
    }

    /// <summary>
    /// Calculates the world rotation of a point along the curve.
    /// </summary>
    /// <param name="u">The distance along the curve. Restricted to 0.0f <= <c>u</c> <= 1.0f</param>
    /// <returns>The world rotation <c>u</c> distance along curve.</returns>
    public Quaternion BlendRotation(float u)
    {
        if (u <= 0.0f)
            return Quaternion.LookRotation(_derivativeControlPoints[0]);
        else if (u >= 1.0f)
            return Quaternion.LookRotation(_derivativeControlPoints[_derivativeControlPoints.Count - 1]);

        Vector3 tangentVector = new Vector3(0, 0, 0);

        for (int i = 0; i < _derivativeControlPoints.Count; i++)
        {
            //for each control point, calculate x, y, and z, given by P(n) * B(u)
            float blend = BlendingFunction(u, i, _derivativeControlPoints.Count - 1);

            tangentVector += _derivativeControlPoints[i] * blend;
        }

        return Quaternion.LookRotation(tangentVector);
    }


    /// <summary>
    /// Calculates the length of the curve. Precision is defined by the max segments used in initialization,
    /// </summary>
    /// <returns>The length of the curve</returns>
    private void CalculateLength()
    {
        float distSum = 0.0f;
        float stepSize = (float)1 / (float)_numSegments;
        Vector3 currPoint = _controlPoints[0];
        Vector3 tempPoint = Vector3.zero;

        for (int i = 0; i <= _numSegments; i++)
        {
            tempPoint = BlendPosition(stepSize * (float)i);
            distSum += Vector3.Distance(currPoint, tempPoint);
            currPoint = tempPoint;
        }

        Length = distSum;
    }
}
