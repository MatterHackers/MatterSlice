/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
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

using MatterSlice.ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

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
        public AABB boundaryBox = new AABB();
        public Polygons outline = new Polygons();
        public Polygons combBoundery = new Polygons();
        public List<Polygons> insets = new List<Polygons>();
        public Polygons skinOutline = new Polygons();
        public Polygons sparseOutline = new Polygons();
        public int bridgeAngle;
    };

    public class SliceLayer
    {
        public long printZ;
        public List<SliceLayerPart> parts = new List<SliceLayerPart>();
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
        public int endAngle;
        public bool generateInternalSupport;
        public int supportXYDistance_um;
        public int supportZDistance_um;

        public IntPoint gridOffset;
        public int gridScale;
        public int gridWidth, gridHeight;
        public List<List<SupportPoint>> xYGridOfSupportPoints = new List<List<SupportPoint>>();
        
        public SupportStorage()
        {
            xYGridOfSupportPoints = null;
        }
    }

    /******************/

    public class SliceVolumeStorage
    {
        public List<SliceLayer> layers = new List<SliceLayer>();
    }

    public class SliceDataStorage
    {
        public Point3 modelSize, modelMin, modelMax;
        public Polygons skirt = new Polygons();
        public Polygons raftOutline = new Polygons();
        public List<Polygons> wipeShield = new List<Polygons>();
        public List<SliceVolumeStorage> volumes = new List<SliceVolumeStorage>();

        public SupportStorage support = new SupportStorage();
        public Polygons wipeTower = new Polygons();
        public IntPoint wipePoint;
    }
}
