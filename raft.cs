/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

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
    public static class Raft
    {
        public static void generateRaft(SliceDataStorage storage, int distance)
        {
            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                if (storage.volumes[volumeIdx].layers.Count < 1) continue;
                SliceLayer layer = storage.volumes[volumeIdx].layers[0];
                for (int i = 0; i < layer.parts.Count; i++)
                {
                    storage.raftOutline = storage.raftOutline.CreateUnion(layer.parts[i].outline.Offset(distance));
                    storage.raftOutline = storage.raftOutline.CreateUnion(storage.wipeTower);
                }
            }

            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, 0);
            storage.raftOutline = storage.raftOutline.CreateUnion(supportGenerator.polygons);
        }
    }
}