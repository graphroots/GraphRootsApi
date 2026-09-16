using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GraphRoots.GraphDbTests
{
    static class TestUtilities
    {
        static string TestdataDirectory()
        {
            var testdata = Path.Combine(AppContext.BaseDirectory, "testdata");
            if (Directory.Exists(testdata))
            {
                return testdata;
            }

            throw new Exception($"Directory {testdata} does not exist.");
        }

        /// <summary>
        /// Get the full path of a test file. The file is expected to be in the "testdata" directory.
        /// </summary>
        public static string TestFilePath(string fileName)
        {
            string ghxFileExtension = ".ghx";
            if (!fileName.EndsWith(ghxFileExtension))
            {
                fileName += ghxFileExtension;
            }

            string fullPath = Path.Combine(TestdataDirectory(), fileName);

            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new Exception($"File {fullPath} does not exist.");
        }

        /// <summary>
        /// Get all Grasshopper XML files in the specified path under testdata.
        /// Pass an empty string to enumerate the testdata directory itself.
        /// </summary>
        public static IEnumerable<string> GetGrasshopperFilesInPath(string pathName)
        {
            string fullPath = string.IsNullOrEmpty(pathName)
                ? TestdataDirectory()
                : Path.Combine(TestdataDirectory(), pathName);

            if (Directory.Exists(fullPath))
            {
                string[] filesGhx = Directory.GetFiles(fullPath, "*.ghx", SearchOption.AllDirectories);
                string[] filesGh = Directory.GetFiles(fullPath, "*.gh", SearchOption.AllDirectories);
                return filesGhx.ToArray().Concat(filesGh);
            }

            throw new Exception($"Directory {fullPath} does not exist.");
        }
    }
}
