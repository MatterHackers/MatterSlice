/*
Copyright (c) 2013, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    /*
    SliceData
    + Layers[]
      + LayerParts[]
        + OutlinePolygons[]
        + Insets[]
          + Polygons[]
        + SkinPolygons[]
    */

    public class SliceLayerPart
    {
        public AABB boundaryBox;
        public Polygons outline;
        public Polygons combBoundery;
        public List<Polygons> insets;
        public Polygons skinOutline;
        public Polygons sparseOutline;
        public int bridgeAngle;
    };

    public class SliceLayer
    {
        public int z;
        public List<SliceLayerPart> parts;
    };

    /******************/
    public class SupportPoint
    {
        public int z;
        public double cosAngle;

        public SupportPoint(int z, double cosAngle)
        {
            this.z = z;
            this.cosAngle = cosAngle;
        }
    }

    public class SupportStorage
    {
        public bool generated;
        public int angle;
        public bool everywhere;
        public int XYDistance;
        public int ZDistance;

        public Point gridOffset;
        public int gridScale;
        public int gridWidth, gridHeight;
        public List<List<SupportPoint>> grid;
        
        public SupportStorage()
        {
            grid = null;
        }
    }

    /******************/

    public class SliceVolumeStorage
    {
        public List<SliceLayer> layers;
    }

    public class SliceDataStorage
    {
        public Point3 modelSize, modelMin, modelMax;
        public Polygons skirt;
        public Polygons raftOutline;
        public List<Polygons> oozeShield;
        public List<SliceVolumeStorage> volumes;

        public SupportStorage support;
        public Polygons wipeTower;
        public Point wipePoint;
    }
}
