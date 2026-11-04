using System;
using System.IO;
using System.Reflection;

namespace TACEUI.Core
{
    public static class AppInfo
    {
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    string versionFile = Path.Combine(PathHelper.GetInstallDirectory(), "config", "version.txt");
                    if (File.Exists(versionFile))
                    {
                        string ver = File.ReadAllText(versionFile).Trim();
                        if (!string.IsNullOrWhiteSpace(ver)) return ver;
                    }

                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    if (version != null && version.Major > 0)
                    {
                        return $"{version.Major}.{version.Minor}.{version.Build}";
                    }
                }
                catch { }
                return "0.0.0";
            }
        }
    }
}
