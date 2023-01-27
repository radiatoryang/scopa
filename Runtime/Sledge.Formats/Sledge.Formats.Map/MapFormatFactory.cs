using System.Collections.Generic;
using Sledge.Formats.Map.Formats;

namespace Sledge.Formats.Map
{
    public static class MapFormatFactory
    {
        private static readonly List<IMapFormat> _formats;

        static MapFormatFactory()
        {
            _formats = new List<IMapFormat>
            {
                new QuakeMapFormat(),
                new WorldcraftRmfFormat()
            };
        }

        public static void Register(IMapFormat loader)
        {
            _formats.Add(loader);
        }
    }
}
