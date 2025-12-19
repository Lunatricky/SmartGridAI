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

        private Vector3I forward = new Vector3I();
        private Vector3I backward = new Vector3I();
        private Vector3I up = new Vector3I();
        private Vector3I down = new Vector3I();
        private Vector3I left = new Vector3I();
        private Vector3I right = new Vector3I();
        private IMyFunctionalBlock referenceBlock;
        private MyBlockOrientation _referenceBlockOrientation;

        public Vector3D ForwardOffset
        {
            get
            {
                return GetWorldPosition(forward);
            }
        }
        public Vector3D BackwardOffset
        {
            get
            {
                return GetWorldPosition(backward);
            }
        }
        public Vector3D UpOffset
        {
            get
            {
                return GetWorldPosition(up);
            }
        }
        public Vector3D DownOffset
        {
            get
            {
                return GetWorldPosition(down);
            }
        }
        public Vector3D LeftOffset
        {
            get
            {
                return GetWorldPosition(left);
            }
        }
        public Vector3D RightOffset
        {
            get
            {
                return GetWorldPosition(right);
            }
        }

        public WorldCoordinates(IMyFunctionalBlock referenceBlock)
        {
            this.referenceBlock = referenceBlock;
            _referenceBlockOrientation = referenceBlock.Orientation;
            SetGridVectorOffsets();
        }

        private void SetGridVectorOffsets()
        {
            Vector3I home = referenceBlock.Position;

            if (_referenceBlockOrientation == null)
            {
                return;
            }

            forward = home + Base6Directions.GetIntVector(_referenceBlockOrientation.Forward) * 1000;
            backward = home - Base6Directions.GetIntVector(_referenceBlockOrientation.Forward) * 1000;
            up = home + Base6Directions.GetIntVector(_referenceBlockOrientation.Up) * 1000;
            down = home - Base6Directions.GetIntVector(_referenceBlockOrientation.Up) * 1000;
            left = home + Base6Directions.GetIntVector(_referenceBlockOrientation.Left) * 1000;
            right = home - Base6Directions.GetIntVector(_referenceBlockOrientation.Left) * 1000;

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
