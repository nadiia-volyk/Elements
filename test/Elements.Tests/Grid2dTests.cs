﻿using System;
using System.IO;
using System.Linq;
using Elements.Spatial;
using Xunit;
using Newtonsoft.Json;
using Elements.Geometry;
using System.Collections.Generic;

namespace Elements.Tests
{
    public class Grid2dTests : ModelTest
    {
        [Fact]
        public void GenerateAndSubdivide2d()
        {
            var grid = new Grid2d(10, 10);
            grid.U.SplitAtPosition(2);
            grid.U.SplitAtPosition(7);
            grid.V.SplitAtPosition(5);
            var subGrid = grid[1, 0];
            subGrid.U.DivideByCount(5);
            var subGrid2 = grid[1, 1];
            subGrid2.V.DivideByFixedLengthFromPosition(0.5, 8);

            Assert.Equal(6, grid.CellsFlat.Count);
            Assert.Equal(19, grid.GetCells().Count);
        }

        [Fact]
        public void TrimBehavior()
        {
            var polygonjson = "[{\"discriminator\":\"Elements.Geometry.Polygon\",\"Vertices\":[{\"X\":-14.371519985751306,\"Y\":-4.8816304299427005,\"Z\":0.0},{\"X\":-17.661873645682569,\"Y\":9.2555712951713573,\"Z\":0.0},{\"X\":12.965610421927806,\"Y\":9.2555712951713573,\"Z\":0.0},{\"X\":12.965610421927806,\"Y\":3.5538269529982784,\"Z\":0.0},{\"X\":6.4046991240848143,\"Y\":3.5538269529982784,\"Z\":0.0},{\"X\":1.3278034769444158,\"Y\":-4.8816304299427005,\"Z\":0.0}]},{\"discriminator\":\"Elements.Geometry.Polygon\",\"Vertices\":[{\"X\":-9.4508365123690652,\"Y\":0.20473478280229102,\"Z\":0.0},{\"X\":-1.8745460850979974,\"Y\":0.20473478280229102,\"Z\":0.0},{\"X\":-1.8745460850979974,\"Y\":5.4378426037008651,\"Z\":0.0},{\"X\":-9.4508365123690652,\"Y\":5.4378426037008651,\"Z\":0.0}]}]\r\n";
            var polygons = JsonConvert.DeserializeObject<List<Polygon>>(polygonjson);
            var grid = new Grid2d(polygons);
            foreach (var pt in polygons[1].Vertices)
            {
                grid.SplitAtPoint(pt);
            }
            grid.CellsFlat.ForEach(c => c.U.DivideByApproximateLength(1.0, EvenDivisionMode.RoundDown));

            var trimmedCells = grid.GetCells().Select(c => new Dictionary<string, object>
                {
                    {"TrimmedGeometry", c.GetTrimmedCellGeometry()},
                    {"BaseRect", c.GetCellGeometry() },
                    {"IsTrimmed", c.IsTrimmed()}
                }
            );
            Assert.Equal(87, trimmedCells.Count());
            Assert.Equal(18, trimmedCells.Count(c => (bool)c["IsTrimmed"]));
            var output = new Dictionary<string, object>
            {
                {"Polygons", polygons },
                {"Cells", trimmedCells }
            };
            File.WriteAllText("/Users/andrewheumann/Desktop/CellTest.json", JsonConvert.SerializeObject(output));
        }

        [Fact]
        public void RotationOfTransform()
        {
            var rectangle = Polygon.Rectangle(10, 6);
            var rotation = new Transform(Vector3.Origin, 30); //30 degree rotation
            var rotatedRectangle = rotation.OfPolygon(rectangle);
            var grid = new Grid2d(rotatedRectangle, rotation);
            grid.U.DivideByCount(20);
            grid.V.DivideByCount(12);
            var output = grid.GetCells().Select(c => c.GetTrimmedCellGeometry());
            File.WriteAllText("/Users/andrewheumann/Desktop/rotationCheck.json", JsonConvert.SerializeObject(output));
            Assert.Equal(0.5, grid[5, 5].U.Domain.Length, 3);
            Assert.Equal(0.5, grid[5, 5].V.Domain.Length, 3);

        }

        [Fact]
        public void NoExceptionsThrownWithAnyRotation()
        {
            for (int rotation = 0; rotation < 360; rotation += 10)
            {
                var a = new Vector3(0.03, 5.08);
                var b = new Vector3(4.28, 9.80);
                var c = new Vector3(9.69, 9.50);
                var d = new Vector3(9.63, 2.43);
                var e = new Vector3(4.72, -0.86);
                var f = new Vector3(1.78, -0.75);

                var polygon = new Polygon(new[] { a, b, c, d, e, f });

                var g = new Vector3(7.735064, 5.746821);
                var h = new Vector3(6.233137, 7.248748);
                var i = new Vector3(3.660163, 4.675775);
                var j = new Vector3(5.162091, 3.173848);

                var polygon2 = new Polygon(new[] { g, h, i, j });

                var alignment = new Transform();
                alignment.Rotate(Vector3.ZAxis, 45);
                var grid = new Grid2d(new[] { polygon, polygon2 }, alignment);
                grid.U.DivideByCount(10);
                var panelA = ("A", 1.0);
                var panelB = ("B", 0.5);
                var panelC = ("C", 1.5);
                var pattern = new[] { panelA, panelB, panelC };
                var pattern2 = new[] { panelB, panelA };
                var patterns = new[] { pattern, pattern2 };

                for (int index = 0; index < grid.CellsFlat.Count; index++)
                {
                    var vDomain = grid.CellsFlat[index].V.Domain;
                    var start = 0.1.MapToDomain(vDomain);
                    grid.CellsFlat[index].V.DivideByPattern(patterns[index % patterns.Count()], PatternMode.Cycle, FixedDivisionMode.RemainderAtBothEnds);
                }
                var cells = grid.GetCells();
                var geo = cells.Select(cl => cl.GetTrimmedCellGeometry());
                var types = cells.Select(cl => cl.Type);
                var trimmed = cells.Select(cl => cl.IsTrimmed());
            }
            //Test verifies no exceptions are thrown at any rotation
        }

        [Fact]
        public void SeparatorsFromNestedGridFromPolygons()
        {
            var a = new Vector3(0.03, 5.08);
            var b = new Vector3(4.28, 9.80);
            var c = new Vector3(9.69, 9.50);
            var d = new Vector3(9.63, 2.43);
            var e = new Vector3(4.72, -0.86);
            var f = new Vector3(1.78, -0.75);

            var polygon = new Polygon(new[] { a, b, c, d, e, f });

            var g = new Vector3(7.735064, 5.746821);
            var h = new Vector3(6.233137, 7.248748);
            var i = new Vector3(3.660163, 4.675775);
            var j = new Vector3(5.162091, 3.173848);

            var polygon2 = new Polygon(new[] { g, h, i, j });

            var orientation = new Transform();
            orientation.Rotate(Vector3.ZAxis, 15);

            var grid = new Grid2d(new[] { polygon, polygon2 }, orientation);
            grid.U.DivideByCount(3);
            grid.V.SplitAtParameter(0.5);
            grid[1, 0].V.DivideByCount(5);
            var cells = grid.GetCells();
            var geo = cells.Select(cl => cl.GetTrimmedCellGeometry());
            var types = cells.Select(cl => cl.Type);
            var trimmed = cells.Select(cl => cl.IsTrimmed());
            var uLines = grid.GetCellSeparators(GridDirection.U);
            var vLines = grid.GetCellSeparators(GridDirection.V);
            var dict = new Dictionary<string, object>
            {
                {"Cell Geometry", geo },
                {"U Lines", uLines },
                {"V Lines", vLines }
            };

        }

        [Fact]
        public void GridInheritsNamesFromBothDirections()
        {
            var grid = new Grid2d(20, 20);
            var uPattern = new[] { 1.0, 2.0, 3.0 };
            var vPattern = new[] { ("Large", 5.0), ("Small", 1.0) };
            grid.U.DivideByPattern(uPattern);
            grid.V.DivideByPattern(vPattern);
            Assert.Equal("B / Large", grid[1, 0].Type);
        }

        [Fact]
        public void InvalidSourceDomain()
        {
            var ex = Assert.ThrowsAny<Exception>(() => new Grid2d(0, 5));
        }

        [Fact]
        public void GetSeparators()
        {
            var grid = new Grid2d(20, 50);
            grid.U.DivideByCount(5);
            grid.V.DivideByFixedLength(8);
            var uLines = grid.GetCellSeparators(GridDirection.U);
            var vLines = grid.GetCellSeparators(GridDirection.V);
            var dict = new Dictionary<string, object>
            {
                { "U", uLines },
                { "V", vLines }
            };
        }
    }
}