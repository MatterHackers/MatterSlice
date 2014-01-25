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

using ClipperLib;
using System.Collections.Generic;

namespace MatterHackers.MatterSlice
{
    public static class Raft
    {
        public static void generateRaft(SliceDataStorage storage, int distance, int supportAngle, bool supportEverywhere, int supportDistance)
        {
            Clipper raftUnion = new Clipper();
            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                if (storage.volumes[volumeIdx].layers.Count < 1)
                {
                    continue;
                }

                SliceLayer layer = storage.volumes[volumeIdx].layers[0];
                for (int i = 0; i < layer.parts.Count; i++)
                {
                    Polygons raft = Clipper.OffsetPolygons(layer.parts[i].outline, distance, ClipperLib.JoinType.jtSquare, 2, false);
                    raftUnion.AddPolygon(raft[0], ClipperLib.PolyType.ptSubject);
                }
            }

            if (supportAngle > -1)
            {
                SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, 0, supportAngle, supportEverywhere, supportDistance, 0);
                raftUnion.AddPolygons(supportGenerator.polygons, ClipperLib.PolyType.ptSubject);
            }

            Polygons raftResult = new Polygons();
            raftUnion.Execute(ClipperLib.ClipType.ctUnion, raftResult, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);
            for (int n = 0; n < raftResult.Count; n++)
            {
                storage.raftOutline.Add(raftResult[n]);
            }
        }
    }
}


