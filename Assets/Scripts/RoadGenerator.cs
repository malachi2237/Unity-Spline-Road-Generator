using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Struct <c>RoadStretchProfile</c> holds the necessary data for constructing waypoints for a stretch of road.
/// </summary>
[System.Serializable]
public struct RoadStretchProfile
{   
    /// <value>The number of road segments to include in this stretch</value>
    public uint NumSegments;

    /// <value>The length of each segment measured from waypoint to waypoint</value>
    public float SegmentLength;

    /// <value>The minimum bend of the of the road segments on this stretch. Does not affect the turn degree</value>
    public float MinTurnIntensity;

    /// <value>The maximum bend of the of the road segments on this stretch. Does not affect the turn degree</value>
    public float MaxTurnIntensity;

    /// <value>The minimum turning angle, in degrees of road segments on this stretch</value>
    public float MinTurningAngle;

    /// <value>The maximum turning angle, in degrees of road segments on this stretch</value>
    public float MaxTurningAngle;

    /// <value>The percentage of times the direction of the stretch will change</value>
    public float DirectionChangeChance;

    public RoadStretchProfile(uint numSegs, float segLength, float minIntensity, float maxIntensity, float minAngle, float maxAngle, float changeChance)
    {
        NumSegments = numSegs;
        SegmentLength = segLength;
        MinTurnIntensity = minIntensity;
        MaxTurnIntensity = maxIntensity;
        MinTurningAngle = minAngle;
        MaxTurningAngle = maxAngle;
        DirectionChangeChance = changeChance;
    }
}

public class RoadGenerator
{
    /// <value> The list of stretch profiles directly used to generate road stretches.</value>
    /// <remarks> MIGHT be refactored out.  </remarks>
    private List<RoadStretchProfile> _stretchProfiles = new List<RoadStretchProfile>();

    /// <value>T he list of generated waypoints. Initially empty, but populated by <c>GenerateWaypoints()</c> </value>
    private List<Vector3> _waypoints = new List<Vector3>();

    /// <summary> A scalar limiting the how far the midsection of a road can stretch left or right </summary>
    private static float s_maxLengthToCPDist = 0.25f;

    /// <summary>
    /// Generates a list of waypoints based on a <c>RoadStretchProfile</c>s. Waypoints are not affected by any other factors.
    /// </summary>
    /// <param name="stretchProfiles">A <c>List</c> of <c>RoadStretchProfile</c>s paired with their intended frequencies. Frequencies are the ratio
    /// the stretches will appear in the whole road.</param>
    /// <param name="numStretches">The number of stretches to be used to construct the road</param>
    /// <remarks> If the sum of the frequencies in <c>stretchProfiles</c> does not equal 1.0, unexpected results may appear.</remarks>
    public void GenerateWaypoints(in List<(RoadStretchProfile Profile, float Frequency)> stretchProfiles, int numStretches)
    {
        //Store the random state do we can preserve it at the end
        var state = Random.state;

        _stretchProfiles.Clear();
        // populate _stretchProfiles
        for (int i = 0; i < numStretches; i++)
        {
            float randomSelection = Random.value;

            float sum = .0f;
            RoadStretchProfile selectedProfile = default;

            foreach (var profilePair in stretchProfiles)
            {
                sum = Mathf.Clamp01(sum + profilePair.Frequency);
                
                if (sum >= randomSelection)
                {
                    selectedProfile = profilePair.Profile;
                    break;
                }
            }

            //TODO Check if selectedProfile == default. DO SOMETHING if it does
            _stretchProfiles.Add(selectedProfile);
        }

        CreateWaypoints(numStretches);

        //Restore the random state
        Random.state = state;
    }

    /// <summary>
    /// Returns a copy of the internal list of waypoints.
    /// </summary>
    /// <returns>The list of waypoints used by the class. Will be null if called before <c>GenerateWaypoints</c></returns>
    public List<Vector3> GetWaypoints()
    {
        return new List<Vector3>(_waypoints);
    }

    /// <summary>
    /// Finds the midpoint of two vectors.
    /// </summary>
    /// <param name="A"> The first vector </param>
    /// <param name="B"> The second vector </param>
    /// <returns> The point between <c>A</c> and <c>B</c> </returns>
    private static Vector3 CalculateMidpoint(Vector3 A, Vector3 B)
    {
        return (A + B) / 2;
    }

    /// <summary>
    /// Populate <c>_waypoints</c> using data from <c>_stretchProfiles</c>
    /// </summary>
    /// <param name="numStretches">The number of stretches for which we create waypoints</param>
    private void CreateWaypoints(int numStretches)
    {
        // left = 0, right = 1
        
        Vector3 lastDir = Vector3.forward;
        bool rightTurn = Random.Range(0, 2) == 1;
        _waypoints.Clear();
        _waypoints.Add(Vector3.zero);

        foreach (var profile in _stretchProfiles)
        {
            
            float turningAngle = Random.Range(profile.MinTurningAngle, profile.MaxTurningAngle);
            float magnitude = Random.Range(profile.MinTurnIntensity, profile.MaxTurnIntensity) * s_maxLengthToCPDist * profile.SegmentLength;

            // To create a smooth spline later, waypoints (knots) must be placed on minima/maxima and inflection points
            for (int i = 0; i < profile.NumSegments; i++)
            {
                // A new inflection point
                Vector3 newPoint = TransformWaypoint(profile, lastDir, turningAngle, rightTurn);

                // A new minima/maxima
                Vector3 midPoint, maxima;
                Vector3 midPRightAxis;
                float rightAxisDir;

                lastDir = Vector3.Normalize(newPoint);

                if (Random.value <= profile.DirectionChangeChance)
                    rightTurn = !rightTurn;

                newPoint += _waypoints.Last();

                midPoint = CalculateMidpoint(_waypoints.Last(), newPoint);
                midPRightAxis = Vector3.Cross(newPoint - _waypoints.Last(), Vector3.up).normalized;
                rightAxisDir = (rightTurn ? 1.0f : -1.0f);//ControlPointTranslationDirection(_waypoints.Last(), newPoint);

                maxima = midPoint + midPRightAxis * magnitude * rightAxisDir;

                
                // Midpoint gets translated perpendicularly to the line from the last waypoint to newPoint, creating a bend
                _waypoints.Add(maxima);
                _waypoints.Add(newPoint);
            }
            
            lastDir = Vector3.Normalize(_waypoints.Last() - _waypoints[_waypoints.Count -2]);
            rightTurn = !rightTurn;
        }
    }

    /// <summary>
    /// Rotate and translate around the global origin procedurally based on <c>profile</c>
    /// </summary>
    /// <param name="profile">The profile used to determine turning angle and stretch length</param>
    /// <param name="lastDirection">The direction from which the waypoint should rotate</param>
    /// <param name="turningDir">The value determining whether the waypoint turns left or right. Left == 0 and Right == 1</param>
    /// <returns></returns>
    private Vector3 TransformWaypoint(RoadStretchProfile profile, Vector3 lastDirection, float turningAngle, bool turnRight)
    {
        var rotationMatrix = 
                Matrix4x4.Rotate(Quaternion.AngleAxis(turningAngle * (turnRight ? 1.0f : -1.0f), Vector3.up));

        return rotationMatrix.MultiplyPoint3x4(lastDirection.normalized) * profile.SegmentLength;
    }
}
