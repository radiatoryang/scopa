using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scopa.Formats.Map.Objects
{
    public abstract class MapObject
    {
        public List<MapObject> Children { get; set; }
        public List<int> Visgroups { get; set; }
        public Color Color { get; set; }

        protected MapObject()
        {
            Children = new List<MapObject>();
            Visgroups = new List<int>();
            Color = Color.white;
        }

        public List<MapObject> FindAll()
        {
            return Find(x => true);
        }

        public List<MapObject> Find(Predicate<MapObject> matcher)
        {
            var list = new List<MapObject>();
            FindRecursive(list, matcher);
            return list;
        }

        private void FindRecursive(ICollection<MapObject> items, Predicate<MapObject> matcher)
        {
            var thisMatch = matcher(this);
            if (thisMatch)
            {
                items.Add(this);
            }
            foreach (var mo in Children)
            {
                mo.FindRecursive(items, matcher);
            }
        }

        public override string ToString()
        {
            if ( Children != null && Children.Count > 0)
                return string.Join( "\n", Children.Select( child => child.ToString() ) );
            else
                return "";
        }

    }
}