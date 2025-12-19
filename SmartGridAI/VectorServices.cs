using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript
{
    partial class VectorServices
    {
        // -------------------- Parsing helpers --------------------
        public static bool TryGetVector(MyIGCMessage msg, out Vector3D v)
        {
            v = new Vector3D();
            if (msg.Data is Vector3D)
            {
                v = (Vector3D)msg.Data;
                return true;
            }

            string s = msg.Data as string;
            if (s != null)
                return TryParseSimpleVector(s, out v);

            return false;
        }

        private static bool TryParseSimpleVector(string s, out Vector3D v)
        {
            v = new Vector3D();
            if (string.IsNullOrWhiteSpace(s)) return false;
            var parts = s.Split(',');
            if (parts.Length != 3) return false;

            double x, y, z;
            if (!double.TryParse(parts[0], out x)) return false;
            if (!double.TryParse(parts[1], out y)) return false;
            if (!double.TryParse(parts[2], out z)) return false;

            v = new Vector3D(x, y, z);
            return true;
        }

        // GPS parser for "GPS:name:X:Y:Z:color:" format
        public static bool TryParseGPS(string gps, out Vector3D result)
        {
            result = new Vector3D();
            if (string.IsNullOrWhiteSpace(gps)) return false;
            if (!gps.StartsWith("GPS:")) return false;

            var parts = gps.Split(':');
            if (parts.Length < 6) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], out x)) return false;
            if (!double.TryParse(parts[3], out y)) return false;
            if (!double.TryParse(parts[4], out z)) return false;

            result = new Vector3D(x, y, z);
            return true;
        }

        public static string FormatVec(Vector3D v)
        {
            return string.Format("{0:0.##},{1:0.##},{2:0.##}", v.X, v.Y, v.Z);
        }
    }
}
