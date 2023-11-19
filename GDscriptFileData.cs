using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralizer
{
    public record GDscriptFileData
    {
        public string Path { get; init; }
        public string ClassName { get; set; }
        public string Extends { get; set; }
        public Dictionary<string, string> DeclaredConstants { get; set; } = new();
        public Dictionary<string, string> StaticFuncs { get; set; } = new();

        public string ResPath => "\"res://" + Path.Replace('\\', '/') + "\"";

        public string RawText { get; set; }

        public override string ToString()
        {
            return Path.Split('\\').Last();
        }
    }
}
