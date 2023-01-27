using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Sledge.Formats.Geometric;

namespace Sledge.Formats
{
    public class Pointfile
    {
        public List<Line> Lines { get; set; }

        public Pointfile()
        {
            Lines = new List<Line>();
        }

        public static Pointfile Parse(IEnumerable<string> lines)
        {
            var pf = new Pointfile();
            var list = lines.ToList();
            if (!list.Any()) return pf;

            // Format detection: look at one line
            // .lin format: coordinate - coordinate
            // .pts format: coordinate
            var detect = list[0].Split(' ');
            var lin = detect.Length == 7;
            var pts = detect.Length == 3;
            if (!lin && !pts) throw new Exception("Invalid pointfile format.");

            Vector3? previous = null;
            foreach (var line in list)
            {
                var split = line.Split(' ');
                var point = NumericsExtensions.Parse(split[0], split[1], split[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                if (lin)
                {
                    var point2 = NumericsExtensions.Parse(split[4], split[5], split[6], NumberStyles.Float, CultureInfo.InvariantCulture);
                    pf.Lines.Add(new Line(point2, point));
                }
                else // pts
                {
                    if (previous.HasValue) pf.Lines.Add(new Line(previous.Value, point));
                    previous = point;
                }
            }

            return pf;
        }
    }
}
