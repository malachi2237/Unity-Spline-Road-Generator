using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A wrapper class to allow a <c>BezierCurve</c> to be attached to an object as a component
/// </summary>
public class CurveComponent : MonoBehaviour
{
    /// <summary>
    /// The <c>BezierCurve</c> this component is wrapped around
    /// </summary>
    public BezierCurve Curve = null;

    /// <summary>
    /// Produces a new <c>BezierCurve</c> with the same rotation as <c>Curve</c> at any given <c>u</c> value, but with different starting and ending position.
    /// </summary>
    /// <param name="scale"> The scale of the new curve in relation to <c>Curve</c> </param>
    /// <returns> A similar curve to <c>Curve</c> with a different scale. </returns>
    public BezierCurve GetSimilarCurve(in float scale)
    {
        Vector3[] cps = new Vector3[Curve.Count];

        Vector3 firstDisplacementVector = CalculateTranslationVector(Curve.BlendRotation(0.0f), scale);
        Vector3 secondDisplacementVector = CalculateTranslationVector(Curve.BlendRotation(1.0f), scale);
        Vector3 averageDisplacement = (firstDisplacementVector + secondDisplacementVector) / 2.0f;

        cps[0] = Curve[0] + firstDisplacementVector;

        for (int i = 1; i < Curve.Count - 1; i++)
            cps[i] = Curve[i] + averageDisplacement;

        cps[Curve.Count - 1] = Curve[Curve.Count - 1] + secondDisplacementVector;

        return new BezierCurve(cps);
    }

    /// <summary>
    /// Calculates a translation vector for the start and end points of a <c>NavigationPath</c>/lane to the left or right of the center of the road
    /// </summary>
    /// <param name="rotation"> The rotation of the road at the point the vector is being calculated </param>
    /// <param name="translation"> The distance left or right of the center of the road the calculated vector should be. Positive values are right and negative are left. </param>
    /// <returns> A right facing vector local to the center of the road with a rotation of <c>rotation</c> and a magnitude of <c>displacement</c> </returns>
    private static Vector3 CalculateTranslationVector(in Quaternion rotation, in float translation)
    {
        Vector3 localRightAxis = rotation * Vector3.right;
        localRightAxis.Normalize();

        return localRightAxis * translation;
    }
}
