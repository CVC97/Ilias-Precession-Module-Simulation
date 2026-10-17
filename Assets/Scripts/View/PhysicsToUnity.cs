using UnityEngine;


public static class PhysicsToUnity {
    // right-handed, z up -> Unity: left-handed, y up  
    // Vectors: swap y and z, no sign change.
    public static Vector3 Vector(Vec3d v) => new Vector3((float)v.X, (float)v.Z, (float)v.Y);

    // converts an angle in rad to a Unity angle in degrees
    // also ensures the angle stays below 360 degrees
    public static float Angle(double angleRad) => (float)((-angleRad*Mathf.Rad2Deg) % 360);
}