using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralizer
{
    public static class StringExtensions
    {
        public static Stream ToStream(this string value, Encoding? encoding = null) => new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(value ?? string.Empty));
    }
}
