using System.Collections.Generic;
using System.Linq;
using Sledge.Formats.Precision;

namespace Sledge.Formats.Map.Objects
{
    /// <summary>
    /// This is a sneaky replacement of Sledge's Solid class for Scopa purposes!
    /// When importing Sledge.Formats, make sure you delete its default Solid.cs
    /// </summary>
    public class Solid : MapObject
    {
        public List<Face> Faces { get; set; }
        public List<Mesh> Meshes { get; set; }

        public Solid()
        {
            Faces = new List<Face>();
            Meshes = new List<Mesh>();
        }

        public void ComputeVertices()
        {
            // doesn't do anything; we compute this later in ScopaMesh
            // if (Faces.Count < 4) return;

            // var poly = new Polyhedron(Faces.Select(x => new Plane(x.Plane.Normal.ToPrecisionVector3(), x.Plane.D)));

            // foreach (var face in Faces)
            // {
            //     var pg = poly.Polygons.FirstOrDefault(x => x.Plane.Normal.EquivalentTo(face.Plane.Normal.ToPrecisionVector3(), 0.001d)); // Magic number that seems to match VHE
            //     if (pg != null)
            //     {
            //         face.Vertices.AddRange(pg.Vertices.Select(x => x.ToStandardVector3()));
            //     }
            // }
        }
    }
}