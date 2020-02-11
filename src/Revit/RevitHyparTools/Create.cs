﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Elements.Geometry;
using GeometryEx;
using ElemGeom = Elements.Geometry;

using Revit = Autodesk.Revit.DB;

namespace RevitHyparTools
{
    public static partial class Create
    {
        public static Elements.Floor[] FloorsFromRevitFloor(Revit.Document doc, Revit.Floor revitFloor)
        {
            var profiles = GetProfilesOfTopFacesOfFloor(doc, revitFloor);
            var thickness = revitFloor.LookupParameter("Thickness")?.AsDouble();

            var floors = new List<Elements.Floor>();
            foreach(var profile in profiles) 
            {
                var zMove = profile.Perimeter.Vertices.Max(v => v.Z);
                var transform = new ElemGeom.Transform(0,0,-zMove);
                
                var zeroedProfile = transform.OfProfile(profile);

                transform.Invert();
                var floor = new Elements.Floor(zeroedProfile, thickness.HasValue ? thickness.Value : 1, transform);
                floors.Add(floor);
            }
            return floors.ToArray();
        }


        private static ElemGeom.Profile[] GetProfilesOfTopFacesOfFloor(Document doc, Floor floor)
        {
            var geom = floor.get_Geometry(new Options());
            var topFaces = geom.Cast<Solid>().Where(g => g!=null).SelectMany(g => GetMostLikelyTopFacesOfSolid(g));
            var profiles = topFaces.SelectMany(f => GetProfilesOfFace(f));

            return profiles.ToArray();
        }

        private static ElemGeom.Profile[] GetProfilesOfFace(PlanarFace f)
        {
            var polygons = f.GetEdgesAsCurveLoops().Select(cL => CurveLoopToPolygon(cL));

            var polygonLoopDict = MatchOuterLoopPolygonsWithInnerHoles(polygons);

            var profiles = polygonLoopDict.Select(kvp => new ElemGeom.Profile(kvp.Key, kvp.Value, Guid.NewGuid(), "Floor Profile"));
            return profiles.ToArray();
        }

        private static Dictionary<Polygon, List<Polygon>> MatchOuterLoopPolygonsWithInnerHoles(IEnumerable<Polygon> polygons)
        {
            var polygonLoopDict = new Dictionary<Polygon, List<Polygon>>();
            foreach (var polygon in polygons)
            {
                bool polyIsInnerLoop = false;
                foreach (var outerLoop in polygonLoopDict.Keys)
                {
                    // TODO possibly replace this with the updated covers(polygon) method when it is brought into elements.
                    if (polygon.Vertices.All(v => outerLoop.Covers(v)))
                    {
                        polyIsInnerLoop = true;
                        polygonLoopDict[outerLoop].Add(polygon);
                        break;
                    }
                }
                if (!polyIsInnerLoop)
                {
                    polygonLoopDict.Add(polygon, new List<Polygon>());
                }
            }

            return polygonLoopDict;
        }

        private static Polygon CurveLoopToPolygon(CurveLoop cL)
        {
            return new ElemGeom.Polygon(cL.Select(l => l.GetEndPoint(0).ToVector3()).ToList());
        }

        private static PlanarFace[] GetMostLikelyTopFacesOfSolid(Solid solid) {
            var faces = new List<PlanarFace>();
            foreach(PlanarFace face in solid.Faces) {
                if (face.FaceNormal.DotProduct(XYZ.BasisZ) > 0.85 && face.FaceNormal.DotProduct(XYZ.BasisZ) <= 1)
                {
                    faces.Add(face);
                }
            }
            return faces.ToArray();
        }
    }
}
