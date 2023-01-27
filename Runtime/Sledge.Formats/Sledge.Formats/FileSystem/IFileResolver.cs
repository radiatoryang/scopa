using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sledge.Formats.FileSystem
{
    public interface IFileResolver
    {
        Stream OpenFile(string path);
        string[] OpenFolder(string path);
    }
}
