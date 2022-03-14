using System.Collections.Generic;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public class Visgroup
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public Color Color { get; set; }
        public bool Visible { get; set; }
        public List<Visgroup> Children { get; set; }

        public Visgroup()
        {
            Children = new List<Visgroup>();
        }
    }
}