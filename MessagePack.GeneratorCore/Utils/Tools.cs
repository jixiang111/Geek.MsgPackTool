using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MessagePackCompiler
{
    public static class Tools
    {
        public static string Enumerable2String<T>(this IEnumerable<T> list)
        {
            return "[" + string.Join(",", list) + "]";
        }

        public static void CreateAsDirectory(this string path, bool isFile = false)
        {
            if (isFile)
                path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void CreateDirectory(this string path, bool delFirset = true)
        {
            if (path.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(path))
            {
                if (delFirset)
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(path);
            }
        }

    }
}
