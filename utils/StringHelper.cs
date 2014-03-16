using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterSlice
{
    public static class StringHelper
    {
        public static string FormatWith(this string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return string.Format(format, args);
        }

        public static string FormatWith(this string format, IFormatProvider provider, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return string.Format(provider, format, args);
        }
    }
}
