using System;


public struct Vec3d {
    public double X;
    public double Y;
    public double Z;

    // unit vectors
    public static readonly Vec3d UnitX = new Vec3d {
        X = 1,
    };

    public static readonly Vec3d UnitY = new Vec3d {
        Y = 1,
    };

    public static readonly Vec3d UnitZ = new Vec3d {
        Z = 1,
    };

    // operator +
    public static Vec3d operator +(Vec3d a, Vec3d b) => 
        new Vec3d {
            X = a.X + b.X,
            Y = a.Y + b.Y,
            Z = a.Z + b.Z,
        };

    // operator -
    public static Vec3d operator -(Vec3d a, Vec3d b) => 
        new Vec3d {
            X = a.X - b.X,
            Y = a.Y - b.Y,
            Z = a.Z - b.Z,
        };

    // operator * (scalar multiplication)
    public static Vec3d operator *(Vec3d a, double x) => 
        new Vec3d {
            X = a.X * x,
            Y = a.Y * x,
            Z = a.Z * x,
        };

    // method dot product
    public static double Dot(Vec3d a, Vec3d b) {
        double dotProduct = a.X*b.X + a.Y*b.Y + a.Z*b.Z;
        return dotProduct;
    }

    // method cross product
    public static Vec3d Cross(Vec3d a, Vec3d b) {
        Vec3d crossProduct = new Vec3d {
            X = a.Y*b.Z - a.Z*b.Y,
            Y = a.Z*b.X - a.X*b.Z,
            Z = a.X*b.Y - a.Y*b.X,
        };  
        return crossProduct;
    }
}