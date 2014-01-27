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
class PolygonRef
{
    Path polygon;
    PolygonRef()
    {}

    public PolygonRef(Path polygon)
    {
this.polygon = polygon;
    }
    
    public int size()
    {
        return polygon.Count;
    }
    
            public Point this[int index]
            {
                get { return polygon[index]; }
            }
    
    public void add(Point p)
    {
        polygon.Add(p);
    }

    public void remove(int index)
    {
        POLY_ASSERT(index < size());
        polygon.erase(polygon.begin() + index);
    }
    
    public void clear()
    {
        polygon.clear();
    }

    public bool orientation()
    {
        return ClipperLib.Orientation(polygon);
    }
    
    public void reverse()
    {
        ClipperLib.ReversePath(polygon);
    }

    public long polygonLength() 
    {
        long length = 0;
        Point p0 = polygon[polygon.Count-1];
        for(unsigned int n=0; n<polygon.Count; n++)
        {
            Point p1 = polygon[n];
            length += vSize(p0 - p1);
            p0 = p1;
        }
        return length;
    }
    
    public double area() 
    {
        return ClipperLib::Area(polygon);
    }

    public Point centerOfMass() 
    {
        double x = 0, y = 0;
        Point p0 = (polygon)[polygon.Count-1];
        for(unsigned int n=0; n<polygon.Count; n++)
        {
            Point p1 = (polygon)[n];
            double second_factor = (p0.X * p1.Y) - (p1.X * p0.Y);
            
            x += double(p0.X + p1.X) * second_factor;
            y += double(p0.Y + p1.Y) * second_factor;
            p0 = p1;
        }

        double area = Area(polygon);
        x = x / 6 / area;
        y = y / 6 / area;

        if (x < 0)
        {
            x = -x;
            y = -y;
        }
        return Point(x, y);
    }
    
    friend class Polygons;
}

public class _Polygon : public PolygonRef
{
    Path poly;

public _Polygon()
    : PolygonRef(poly)
    {
    }
}

//#define Polygon _Polygon

public class Polygons
{
    ClipperLib.Paths polygons;
    public int size()
    {
        return polygons.Count;
    }
    
    public PolygonRef operator[] (unsigned int index)
    {
        POLY_ASSERT(index < size());
        return PolygonRef(polygons[index]);
    }
    public void remove(unsigned int index)
    {
        POLY_ASSERT(index < size());
        polygons.erase(polygons.begin() + index);
    }
    public void clear()
    {
        polygons.clear();
    }
    public void add(PolygonRef poly)
    {
        polygons.Add(poly.polygon);
    }
    public void add(Polygons other)
    {
        for(unsigned int n=0; n<other.polygons.Count; n++)
            polygons.Add(other.polygons[n]);
    }
    public PolygonRef newPoly()
    {
        polygons.Add(ClipperLib::Path());
        return PolygonRef(polygons[polygons.Count-1]);
    }
    
    public Polygons() {}
    public Polygons(Polygons other) { polygons = other.polygons; }
    public Polygons operator=( Polygons other) { polygons = other.polygons; return *this; }
    public Polygons difference( Polygons other) 
    {
        Polygons ret;
        ClipperLib::Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib::ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib::ptClip, true);
        clipper.Execute(ClipperLib::ctDifference, ret.polygons);
        return ret;
    }
    public Polygons unionPolygons( Polygons other) 
    {
        Polygons ret;
        ClipperLib::Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib::ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib::ptSubject, true);
        clipper.Execute(ClipperLib::ctUnion, ret.polygons, ClipperLib::pftNonZero, ClipperLib::pftNonZero);
        return ret;
    }
    public Polygons intersection( Polygons other) 
    {
        Polygons ret;
        ClipperLib::Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib::ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib::ptClip, true);
        clipper.Execute(ClipperLib::ctIntersection, ret.polygons);
        return ret;
    }
    public Polygons offset(int distance) 
    {
        Polygons ret;
        ClipperLib::ClipperOffset clipper;
        clipper.AddPaths(polygons, ClipperLib::jtMiter, ClipperLib::etClosedPolygon);
        clipper.MiterLimit = 2.0;
        clipper.Execute(ret.polygons, distance);
        return ret;
    }
    public List<Polygons> splitIntoParts(bool unionAll = false) 
    {
        List<Polygons> ret;
        ClipperLib::Clipper clipper;
        ClipperLib::PolyTree resultPolyTree;
        clipper.AddPaths(polygons, ClipperLib::ptSubject, true);
        if (unionAll)
            clipper.Execute(ClipperLib::ctUnion, resultPolyTree, ClipperLib::pftNonZero, ClipperLib::pftNonZero);
        else
            clipper.Execute(ClipperLib::ctUnion, resultPolyTree);
        
        _processPolyTreeNode(&resultPolyTree, ret);
        return ret;
    }
    void _processPolyTreeNode(PolyNode node, List<Polygons> ret) 
    {
        for(int n=0; n<node.ChildCount(); n++)
        {
            ClipperLib::PolyNode* child = node.Childs[n];
            Polygons polygons;
            polygons.add(child.Contour);
            for(int i=0; i<child.ChildCount(); i++)
            {
                polygons.add(child.Childs[i].Contour);
                _processPolyTreeNode(child.Childs[i], ret);
            }
            ret.Add(polygons);
        }
    }
    public Polygons processEvenOdd() 
    {
        Polygons ret;
        ClipperLib::Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib::ptSubject, true);
        clipper.Execute(ClipperLib::ctUnion, ret.polygons);
        return ret;
    }
    
    public long polygonLength() 
    {
        long length = 0;
        for(unsigned int i=0; i<polygons.Count; i++)
        {
            Point p0 = polygons[i][polygons[i].Count-1];
            for(unsigned int n=0; n<polygons[i].Count; n++)
            {
                Point p1 = polygons[i][n];
                length += vSize(p0 - p1);
                p0 = p1;
            }
        }
        return length;
    }

    void applyMatrix( PointMatrix matrix)
    {
        for(unsigned int i=0; i<polygons.Count; i++)
        {
            for(unsigned int j=0; j<polygons[i].Count; j++)
            {
                polygons[i][j] = matrix.apply(polygons[i][j]);
            }
        }
    }
};

/* Axis aligned boundary box */
public class AABB
{
    public Point min, max;
    
    public AABB()
    : min(LLONG_MIN, LLONG_MIN), max(LLONG_MIN, LLONG_MIN)
    {
    }
    public AABB(Polygons polys)
    : min(LLONG_MIN, LLONG_MIN), max(LLONG_MIN, LLONG_MIN)
    {
        calculate(polys);
    }
    
    public void calculate(Polygons polys)
    {
        min = Point(LLONG_MAX, LLONG_MAX);
        max = Point(LLONG_MIN, LLONG_MIN);
        for(unsigned int i=0; i<polys.Count; i++)
        {
            for(unsigned int j=0; j<polys[i].Count; j++)
            {
                if (min.X > polys[i][j].X) min.X = polys[i][j].X;
                if (min.Y > polys[i][j].Y) min.Y = polys[i][j].Y;
                if (max.X < polys[i][j].X) max.X = polys[i][j].X;
                if (max.Y < polys[i][j].Y) max.Y = polys[i][j].Y;
            }
        }
    }
    
    public bool hit(AABB other)
    {
        if (max.X < other.min.X) return false;
        if (min.X > other.max.X) return false;
        if (max.Y < other.min.Y) return false;
        if (min.Y > other.max.Y) return false;
        return true;
    }
}
}