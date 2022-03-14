using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Scopa.Formats.Map.Objects;

namespace Scopa.Formats.Map.Formats
{
    public static class MapFormatExtensions
    {
        public static MapFile ReadFromFile(this IMapFormat mapFormat, string fileName)
        {
            using (var fo = File.OpenRead(fileName))
            {
                return mapFormat.Read(fo);
            }
        }
    }
}
