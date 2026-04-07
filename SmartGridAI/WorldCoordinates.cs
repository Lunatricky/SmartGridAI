using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript
{
    partial class WorldCoordinates
    {
        private double PHI = Math.PI * (3.0 - Math.Sqrt(5.0)); // golden angle

        private IMyFunctionalBlock referenceBlock;
        private MyBlockOrientation _referenceBlockOrientation;

        public WorldCoordinates(IMyFunctionalBlock referenceBlock)
        {
            this.referenceBlock = referenceBlock;
        }

        Vector3D GetPoint(int i, int n, double radius, Vector3D center)
        {
            double y = 1 - (i / (double)(n - 1)) * 2; // from 1 to -1
            double r = Math.Sqrt(1 - y * y);

            double theta = PHI * i;

            double x = Math.Cos(theta) * r;
            double z = Math.Sin(theta) * r;

            return center + new Vector3D(x, y, z) * radius;
        }

        public Vector3D GetWorldPosition(Vector3I localPosition)
        {
            // Convert the grid coordinates to a local position in 3D space
            Vector3D localCoords = (Vector3D)localPosition * referenceBlock.CubeGrid.GridSize;

            // Transform the local position to world coordinates using the grid's WorldMatrix
            Vector3D worldCoords = Vector3D.Transform(localCoords, referenceBlock.CubeGrid.WorldMatrix);

            return worldCoords;
        }
    }
}
