using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// Binary Grasshopper archives store thumbnails as <see cref="Bitmap"/>. System.Drawing.Common 6 loads
    /// libgdiplus on Unix but does not probe Apple Silicon Homebrew, so stage it next to the drawing assembly.
    /// </summary>
    static class GdiPlusNative
    {
        internal static void Ensure()
        {
            AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var source = FindInstalledLibrary();
            if (source == null)
            {
                return;
            }

            foreach (var destinationDirectory in DestinationDirectories())
            {
                foreach (var fileName in DestinationFileNames(source))
                {
                    Stage(source, Path.Combine(destinationDirectory, fileName));
                }
            }
        }

        static IEnumerable<string> DestinationDirectories()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var directories = new List<string>();

            void Add(string? path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                var full = Path.GetFullPath(path);
                if (seen.Add(full))
                {
                    directories.Add(full);
                }
            }

            var drawingLocation = typeof(Bitmap).Assembly.Location;
            if (!string.IsNullOrEmpty(drawingLocation))
            {
                Add(Path.GetDirectoryName(drawingLocation));
            }

            Add(AppContext.BaseDirectory);
            Add(Path.Combine(AppContext.BaseDirectory, "runtimes", "unix", "lib", "net6.0"));
            return directories;
        }

        static IEnumerable<string> DestinationFileNames(string source)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            names.Add(Path.GetFileName(source));
            names.Add(OperatingSystem.IsMacOS() ? "libgdiplus.dylib" : "libgdiplus.so");
            return names;
        }

        static void Stage(string source, string destination)
        {
            try
            {
                var directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    return;
                }

                try
                {
                    File.CreateSymbolicLink(destination, source);
                }
                catch (Exception)
                {
                    File.Copy(source, destination, overwrite: false);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        static string? FindInstalledLibrary()
        {
            foreach (var candidate in CandidateLibraries())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        static IEnumerable<string> CandidateLibraries()
        {
            if (OperatingSystem.IsMacOS())
            {
                yield return "/opt/homebrew/lib/libgdiplus.dylib";
                yield return "/opt/homebrew/opt/mono-libgdiplus/lib/libgdiplus.dylib";
                yield return "/usr/local/lib/libgdiplus.dylib";
                yield return "/opt/local/lib/libgdiplus.dylib";
            }
            else
            {
                yield return "/usr/lib/libgdiplus.so.0";
                yield return "/usr/lib/libgdiplus.so";
                yield return "/usr/lib/x86_64-linux-gnu/libgdiplus.so.0";
                yield return "/usr/lib/aarch64-linux-gnu/libgdiplus.so.0";
                yield return "/usr/local/lib/libgdiplus.so.0";
                yield return "/usr/local/lib/libgdiplus.so";
            }
        }
    }
}
