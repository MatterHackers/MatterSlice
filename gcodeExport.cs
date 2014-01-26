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
using System.IO;

namespace MatterHackers.MatterSlice
{
public class GCodeExport
{
    FileStream f;
    double extrusionAmount;
    double extrusionPerMM;
    double retractionAmount;
    double extruderSwitchRetraction;
    double minimalExtrusionBeforeRetraction;
    double extrusionAmountAtPreviousRetraction;
    Point3 currentPosition;
    Point[] extruderOffset = new Point[ConfigSettings.MAX_EXTRUDERS];
    int currentSpeed, retractionSpeed;
    int zPos;
    bool isRetracted;
    int extruderNr;
    int currentFanSpeed;
    int flavor;
    
    double[] totalFilament = new double[ConfigSettings.MAX_EXTRUDERS];
    double totalPrintTime;
    TimeEstimateCalculator estimateCalculator;
public:
    
GCodePlanner(GCodeExport& gcode, int travelSpeed, int retractionMinimalDistance)
: gcode(gcode), travelConfig(travelSpeed, 0, "travel")
{
    lastPosition = gcode.getPositionXY();
    comb = NULL;
    extrudeSpeedFactor = 100;
    travelSpeedFactor = 100;
    extraTime = 0.0;
    totalPrintTime = 0.0;
    forceRetraction = false;
    alwaysRetract = false;
    currentExtruder = gcode.getExtruderNr();
    this.retractionMinimalDistance = retractionMinimalDistance;
}
    
    void replaceTagInStart(string tag, string replaceValue);
    
    void setExtruderOffset(int id, Point p);
    
    void setFlavor(int flavor);
    int getFlavor();
    
    void setFilename(string filename);
    
    bool isValid();
    
    void setExtrusion(int layerThickness, int filamentDiameter, int flow);
    
    void setRetractionSettings(int retractionAmount, int retractionSpeed, int extruderSwitchRetraction, int minimalExtrusionBeforeRetraction);
    
    void setZ(int z);
    
    Point getPositionXY();
    
    int getPositionZ();

    int getExtruderNr();
    
double GCodeExport::getTotalFilamentUsed(int e)
{
    if (e == extruderNr)
        return totalFilament[e] + extrusionAmount;
    return totalFilament[e];
}

double GCodeExport::getTotalPrintTime()
{
    return totalPrintTime;
}

    void GCodeExport::updateTotalPrintTime()
{
    totalPrintTime += estimateCalculator.calculate();
    estimateCalculator.reset();
}

void GCodeExport::addComment(string comment, ...)
{
    va_list args;
    va_start(args, comment);
    fprintf(f, ";");
    vfprintf(f, comment, args);
    fprintf(f, "\n");
    va_end(args);
}

void GCodeExport::addLine(string line, ...)
{
    va_list args;
    va_start(args, line);
    vfprintf(f, line, args);
    fprintf(f, "\n");
    va_end(args);
}
    
void GCodeExport::resetExtrusionValue()
{
    if (extrusionAmount != 0.0)
    {
        fprintf(f, "G92 E0\n");
        totalFilament[extruderNr] += extrusionAmount;
        extrusionAmountAtPreviousRetraction -= extrusionAmount;
        extrusionAmount = 0.0;
    }
}
    
void GCodeExport::addDelay(double timeAmount)
{
    fprintf(f, "G4 P%d\n", int(timeAmount * 1000));
    totalPrintTime += timeAmount;
}
    
void GCodeExport::addMove(Point p, int speed, int lineWidth)
{
    if (lineWidth != 0)
    {
        Point diff = p - getPositionXY();
        if (isRetracted)
        {
            if (flavor == GCODE_FLAVOR_ULTIGCODE)
            {
                fprintf(f, "G11\n");
            }else{
                fprintf(f, "G1 F%i E%0.5lf\n", retractionSpeed * 60, extrusionAmount);
                currentSpeed = retractionSpeed;
                estimateCalculator.plan(TimeEstimateCalculator::Position(double(p.X) / 1000.0, (p.Y) / 1000.0, double(zPos) / 1000.0, extrusionAmount), currentSpeed);
            }
            if (extrusionAmount > 10000.0) //According to https://github.com/Ultimaker/CuraEngine/issues/14 having more then 21m of extrusion causes inaccuracies. So reset it every 10m, just to be sure.
                resetExtrusionValue();
            isRetracted = false;
        }
        extrusionAmount += extrusionPerMM * double(lineWidth) / 1000.0 * vSizeMM(diff);
        fprintf(f, "G1");
    }else{
        fprintf(f, "G0");
    }
    
    if (currentSpeed != speed)
    {
        fprintf(f, " F%i", speed * 60);
        currentSpeed = speed;
    }
    fprintf(f, " X%0.2f Y%0.2f", float(p.X - extruderOffset[extruderNr].X)/1000, float(p.Y - extruderOffset[extruderNr].Y)/1000);
    if (zPos != currentPosition.z)
        fprintf(f, " Z%0.2f", float(zPos)/1000);
    if (lineWidth != 0)
        fprintf(f, " E%0.5lf", extrusionAmount);
    fprintf(f, "\n");
    
    currentPosition = Point3(p.X, p.Y, zPos);
    estimateCalculator.plan(TimeEstimateCalculator::Position(double(currentPosition.x) / 1000.0, (currentPosition.y) / 1000.0, double(currentPosition.z) / 1000.0, extrusionAmount), currentSpeed);
}
    
void GCodeExport::addRetraction()
{
    if (retractionAmount > 0 && !isRetracted && extrusionAmountAtPreviousRetraction + minimalExtrusionBeforeRetraction < extrusionAmount)
    {
        if (flavor == GCODE_FLAVOR_ULTIGCODE)
        {
            fprintf(f, "G10\n");
        }else{
            fprintf(f, "G1 F%i E%0.5lf\n", retractionSpeed * 60, extrusionAmount - retractionAmount);
            currentSpeed = retractionSpeed;
            estimateCalculator.plan(TimeEstimateCalculator::Position(double(currentPosition.x) / 1000.0, (currentPosition.y) / 1000.0, double(currentPosition.z) / 1000.0, extrusionAmount - retractionAmount), currentSpeed);
        }
        extrusionAmountAtPreviousRetraction = extrusionAmount;
        isRetracted = true;
    }
}
    
void GCodeExport::switchExtruder(int newExtruder)
{
    if (extruderNr == newExtruder)
        return;
    
    resetExtrusionValue();
    extruderNr = newExtruder;

    if (flavor == GCODE_FLAVOR_ULTIGCODE)
    {
        fprintf(f, "G10 S1\n");
    }else{
        fprintf(f, "G1 F%i E%0.4lf\n", retractionSpeed * 60, extrusionAmount - extruderSwitchRetraction);
        currentSpeed = retractionSpeed;
    }
    isRetracted = true;
    if (flavor == GCODE_FLAVOR_MAKERBOT)
        fprintf(f, "M135 T%i\n", extruderNr);
    else
        fprintf(f, "T%i\n", extruderNr);
}
    
void GCodeExport::addCode(string str)
{
    fprintf(f, "%s\n", str);
}
    
void GCodeExport::addFanCommand(int speed)
{
    if (currentFanSpeed == speed)
        return;
    if (speed > 0)
    {
        if (flavor == GCODE_FLAVOR_MAKERBOT)
            fprintf(f, "M126 T0 ; value = %d\n", speed * 255 / 100);
        else
            fprintf(f, "M106 S%d\n", speed * 255 / 100);
    }
    else
    {
        if (flavor == GCODE_FLAVOR_MAKERBOT)
            fprintf(f, "M127 T0\n");
        else
            fprintf(f, "M107\n");
    }
    currentFanSpeed = speed;
}

int getFileSize(){
    return ftell(f);
}

    void GCodeExport::tellFileSize() {
    float fsize = (float) ftell(f);
    if(fsize > 1024*1024) {
        fsize /= 1024.0*1024.0;
        log("Wrote %5.1f MB.\n",fsize);
    }
    if(fsize > 1024) {
        fsize /= 1024.0;
        log("Wrote %5.1f kilobytes.\n",fsize);
    }
}

};

class GCodePathConfig
{
public:
    int speed;
    int lineWidth;
    string name;
    bool spiralize;
    
    GCodePathConfig() : speed(0), lineWidth(0), name(NULL), spiralize(false) {}
    GCodePathConfig(int speed, int lineWidth, string name) : speed(speed), lineWidth(lineWidth), name(name), spiralize(false) {}
    
    void setData(int speed, int lineWidth, string name)
    {
        this.speed = speed;
        this.lineWidth = lineWidth;
        this.name = name;
    }
};

class GCodePath
{
public:
    GCodePathConfig* config;
    bool retract;
    int extruder;
    vector<Point> points;
    bool done;//Path is finished, no more moves should be added, and a new path should be started instead of any appending done to this one.
};

class GCodePlanner
{
private:
    GCodeExport& gcode;
    
    Point lastPosition;
    vector<GCodePath> paths;
    Comb* comb;
    
    GCodePathConfig travelConfig;
    int extrudeSpeedFactor;
    int travelSpeedFactor;
    int currentExtruder;
    int retractionMinimalDistance;
    bool forceRetraction;
    bool alwaysRetract;
    double extraTime;
    double totalPrintTime;
private:
GCodePath* getLatestPathWithConfig(GCodePathConfig* config)
{
    if (paths.size() > 0 && paths[paths.size()-1].config == config && !paths[paths.size()-1].done)
        return &paths[paths.size()-1];
    paths.push_back(GCodePath());
    GCodePath* ret = &paths[paths.size()-1];
    ret.retract = false;
    ret.config = config;
    ret.extruder = currentExtruder;
    ret.done = false;
    return ret;
}

    void forceNewPathStart()
{
    if (paths.size() > 0)
        paths[paths.size()-1].done = true;
}
public:
    GCodePlanner(GCodeExport& gcode, int travelSpeed, int retractionMinimalDistance);
    ~GCodePlanner();
    
    bool setExtruder(int extruder)
    {
        if (extruder == currentExtruder)
            return false;
        currentExtruder = extruder;
        return true;
    }
    
    int getExtruder()
    {
        return currentExtruder;
    }

    void setCombBoundary(Polygons* polygons)
    {
        if (comb)
            delete comb;
        if (polygons)
            comb = new Comb(*polygons);
        else
            comb = NULL;
    }
    
    void setAlwaysRetract(bool alwaysRetract)
    {
        this.alwaysRetract = alwaysRetract;
    }
    
    void forceRetract()
    {
        forceRetraction = true;
    }
    
    void setExtrudeSpeedFactor(int speedFactor)
    {
        if (speedFactor < 1) speedFactor = 1;
        this.extrudeSpeedFactor = speedFactor;
    }
    int getExtrudeSpeedFactor()
    {
        return this.extrudeSpeedFactor;
    }
    void setTravelSpeedFactor(int speedFactor)
    {
        if (speedFactor < 1) speedFactor = 1;
        this.travelSpeedFactor = speedFactor;
    }
    int getTravelSpeedFactor()
    {
        return this.travelSpeedFactor;
    }
    
void GCodePlanner::addTravel(Point p)
{
    GCodePath* path = getLatestPathWithConfig(&travelConfig);
    if (forceRetraction)
    {
        if (!shorterThen(lastPosition - p, retractionMinimalDistance))
        {
            path.retract = true;
        }
        forceRetraction = false;
    }else if (comb != NULL)
    {
        vector<Point> pointList;
        if (comb.calc(lastPosition, p, pointList))
        {
            for(int n=0; n<pointList.size(); n++)
            {
                path.points.push_back(pointList[n]);
            }
        }else{
            if (!shorterThen(lastPosition - p, retractionMinimalDistance))
                path.retract = true;
        }
    }else if (alwaysRetract)
    {
        if (!shorterThen(lastPosition - p, retractionMinimalDistance))
            path.retract = true;
    }
    path.points.push_back(p);
    lastPosition = p;
}
    
void addExtrusionMove(Point p, GCodePathConfig* config)
{
    getLatestPathWithConfig(config).points.push_back(p);
    lastPosition = p;
}
    
void GCodePlanner::moveInsideCombBoundary(int distance)
{
    if (!comb || comb.checkInside(lastPosition)) return;
    Point p = lastPosition;
    if (comb.moveInside(&p, distance))
    {
        //Move inside again, so we move out of tight 90deg corners
        comb.moveInside(&p, distance);
        if (comb.checkInside(p))
        {
            addTravel(p);
            //Make sure the that any retraction happens after this move, not before it by starting a new move path.
            forceNewPathStart();
        }
    }
}

void addPolygon(PolygonRef polygon, int startIdx, GCodePathConfig* config)
{
    Point p0 = polygon[startIdx];
    addTravel(p0);
    for(int i=1; i<polygon.size(); i++)
    {
        Point p1 = polygon[(startIdx + i) % polygon.size()];
        addExtrusionMove(p1, config);
        p0 = p1;
    }
    if (polygon.size() > 2)
        addExtrusionMove(polygon[startIdx], config);
}

void addPolygonsByOptimizer(Polygons& polygons, GCodePathConfig* config)
{
    PathOrderOptimizer orderOptimizer(lastPosition);
    for(int i=0;i<polygons.size();i++)
        orderOptimizer.addPolygon(polygons[i]);
    orderOptimizer.optimize();
    for(int i=0;i<orderOptimizer.polyOrder.size();i++)
    {
        int nr = orderOptimizer.polyOrder[i];
        addPolygon(polygons[nr], orderOptimizer.polyStart[nr], config);
    }
}
    
void forceMinimalLayerTime(double minTime, int minimalSpeed)
{
    Point p0 = gcode.getPositionXY();
    double travelTime = 0.0;
    double extrudeTime = 0.0;
    for(int n=0; n<paths.size(); n++)
    {
        GCodePath* path = &paths[n];
        for(int i=0; i<path.points.size(); i++)
        {
            double thisTime = vSizeMM(p0 - path.points[i]) / double(path.config.speed);
            if (path.config.lineWidth != 0)
                extrudeTime += thisTime;
            else
                travelTime += thisTime;
            p0 = path.points[i];
        }
    }
    double totalTime = extrudeTime + travelTime;
    if (totalTime < minTime && extrudeTime > 0.0)
    {
        double minExtrudeTime = minTime - travelTime;
        if (minExtrudeTime < 1)
            minExtrudeTime = 1;
        double factor = extrudeTime / minExtrudeTime;
        for(int n=0; n<paths.size(); n++)
        {
            GCodePath* path = &paths[n];
            if (path.config.lineWidth == 0)
                continue;
            int speed = path.config.speed * factor;
            if (speed < minimalSpeed)
                factor = double(minimalSpeed) / double(path.config.speed);
        }
        
        //Only slow down with the minimal time if that will be slower then a factor already set. First layer slowdown also sets the speed factor.
        if (factor * 100 < getExtrudeSpeedFactor())
            setExtrudeSpeedFactor(factor * 100);
        else
            factor = getExtrudeSpeedFactor() / 100.0;
        
        if (minTime - (extrudeTime / factor) - travelTime > 0.1)
        {
            //TODO: Use up this extra time (circle around the print?)
            this.extraTime = minTime - (extrudeTime / factor) - travelTime;
        }
        this.totalPrintTime = (extrudeTime / factor) + travelTime;
    }else{
        this.totalPrintTime = totalTime;
    }
}
    
void writeGCode(bool liftHeadIfNeeded, int layerThickness)
{
    GCodePathConfig* lastConfig = NULL;
    int extruder = gcode.getExtruderNr();

    for(int n=0; n<paths.size(); n++)
    {
        GCodePath path = paths[n];
        if (extruder != path.extruder)
        {
            extruder = path.extruder;
            gcode.switchExtruder(extruder);
        }else if (path.retract)
        {
            gcode.addRetraction();
        }
        if (path.config != &travelConfig && lastConfig != path.config)
        {
            gcode.addComment("TYPE:%s", path.config.name);
            lastConfig = path.config;
        }
        int speed = path.config.speed;
        
        if (path.config.lineWidth != 0)// Only apply the extrudeSpeedFactor to extrusion moves
            speed = speed * extrudeSpeedFactor / 100;
        else
            speed = speed * travelSpeedFactor / 100;
        
        if (path.points.size() == 1 && path.config != &travelConfig && shorterThen(gcode.getPositionXY() - path.points[0], path.config.lineWidth * 2))
        {
            //Check for lots of small moves and combine them into one large line
            Point p0 = path.points[0];
            int i = n + 1;
            while(i < paths.size() && paths[i].points.size() == 1 && shorterThen(p0 - paths[i].points[0], path.config.lineWidth * 2))
            {
                p0 = paths[i].points[0];
                i ++;
            }
            if (paths[i-1].config == &travelConfig)
                i --;
            if (i > n + 2)
            {
                p0 = gcode.getPositionXY();
                for(int x=n; x<i-1; x+=2)
                {
                    int64_t oldLen = vSize(p0 - paths[x].points[0]);
                    Point newPoint = (paths[x].points[0] + paths[x+1].points[0]) / 2;
                    int64_t newLen = vSize(gcode.getPositionXY() - newPoint);
                    if (newLen > 0)
                        gcode.addMove(newPoint, speed, path.config.lineWidth * oldLen / newLen);
                    
                    p0 = paths[x+1].points[0];
                }
                gcode.addMove(paths[i-1].points[0], speed, path.config.lineWidth);
                n = i - 1;
                continue;
            }
        }
        
        bool spiralize = path.config.spiralize;
        if (spiralize)
        {
            //Check if we are the last spiralize path in the list, if not, do not spiralize.
            for(int m=n+1; m<paths.size(); m++)
            {
                if (paths[m].config.spiralize)
                    spiralize = false;
            }
        }
        if (spiralize)
        {
            //If we need to spiralize then raise the head slowly by 1 layer as this path progresses.
            float totalLength = 0.0;
            int z = gcode.getPositionZ();
            Point p0 = gcode.getPositionXY();
            for(int i=0; i<path.points.size(); i++)
            {
                Point p1 = path.points[i];
                totalLength += vSizeMM(p0 - p1);
                p0 = p1;
            }
            
            float length = 0.0;
            p0 = gcode.getPositionXY();
            for(int i=0; i<path.points.size(); i++)
            {
                Point p1 = path.points[i];
                length += vSizeMM(p0 - p1);
                p0 = p1;
                gcode.setZ(z + layerThickness * length / totalLength);
                gcode.addMove(path.points[i], speed, path.config.lineWidth);
            }
        }else{
            for(int i=0; i<path.points.size(); i++)
            {
                gcode.addMove(path.points[i], speed, path.config.lineWidth);
            }
        }
    }
    
    gcode.updateTotalPrintTime();
    if (liftHeadIfNeeded && extraTime > 0.0)
    {
        gcode.addComment("Small layer, adding delay of %f", extraTime);
        gcode.addRetraction();
        gcode.setZ(gcode.getPositionZ() + 3000);
        gcode.addMove(gcode.getPositionXY(), travelConfig.speed, 0);
        gcode.addMove(gcode.getPositionXY() - Point(-20000, 0), travelConfig.speed, 0);
        gcode.addDelay(extraTime);
    }
}
}

GCodeExport::GCodeExport()
: currentPosition(0,0,0)
{
    extrusionAmount = 0;
    extrusionPerMM = 0;
    retractionAmount = 4.5;
    minimalExtrusionBeforeRetraction = 0.0;
    extrusionAmountAtPreviousRetraction = -10000;
    extruderSwitchRetraction = 14.5;
    extruderNr = 0;
    currentFanSpeed = -1;
    
    totalPrintTime = 0.0;
    for(int e=0; e<MAX_EXTRUDERS; e++)
        totalFilament[e] = 0.0;
    
    currentSpeed = 0;
    retractionSpeed = 45;
    isRetracted = true;
    memset(extruderOffset, 0, sizeof(extruderOffset));
    f = stdout;
}

GCodeExport::~GCodeExport()
{
    if (f)
        fclose(f);
}

void GCodeExport::replaceTagInStart(string tag, string replaceValue)
{
    off64_t oldPos = ftello64(f);
    
    char buffer[1024];
    fseeko64(f, 0, SEEK_SET);
    fread(buffer, 1024, 1, f);
    
    char* c = strstr(buffer, tag);
    memset(c, ' ', strlen(tag));
    if (c) memcpy(c, replaceValue, strlen(replaceValue));
    
    fseeko64(f, 0, SEEK_SET);
    fwrite(buffer, 1024, 1, f);
    
    fseeko64(f, oldPos, SEEK_SET);
}

void GCodeExport::setExtruderOffset(int id, Point p)
{
    extruderOffset[id] = p;
}

void GCodeExport::setFlavor(int flavor)
{
    this.flavor = flavor;
}
int GCodeExport::getFlavor()
{
    return this.flavor;
}

void GCodeExport::setFilename(string filename)
{
    f = fopen(filename, "w+");
}

bool GCodeExport::isValid()
{
    return f != NULL;
}

void GCodeExport::setExtrusion(int layerThickness, int filamentDiameter, int flow)
{
    double filamentArea = M_PI * (double(filamentDiameter) / 1000.0 / 2.0) * (double(filamentDiameter) / 1000.0 / 2.0);
    if (flavor == GCODE_FLAVOR_ULTIGCODE)//UltiGCode uses volume extrusion as E value, and thus does not need the filamentArea in the mix.
        extrusionPerMM = double(layerThickness) / 1000.0;
    else
        extrusionPerMM = double(layerThickness) / 1000.0 / filamentArea * double(flow) / 100.0;
}

void GCodeExport::setRetractionSettings(int retractionAmount, int retractionSpeed, int extruderSwitchRetraction, int minimalExtrusionBeforeRetraction)
{
    this.retractionAmount = double(retractionAmount) / 1000.0;
    this.retractionSpeed = retractionSpeed;
    this.extruderSwitchRetraction = double(extruderSwitchRetraction) / 1000.0;
    this.minimalExtrusionBeforeRetraction = double(minimalExtrusionBeforeRetraction) / 1000.0;
}

void GCodeExport::setZ(int z)
{
    this.zPos = z;
}

Point GCodeExport::getPositionXY()
{
    return Point(currentPosition.x, currentPosition.y);
}

int GCodeExport::getPositionZ()
{
    return currentPosition.z;
}

int GCodeExport::getExtruderNr()
{
    return extruderNr;
}













}