﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;

namespace MadsKristensen.EditorExtensions
{
    internal static class LessProjectCompiler
    {
        public static void CompileProject(Project project)
        {
            if (project != null && !string.IsNullOrEmpty(project.FullName))
            {
                Task.Run(() => Compile(project));
            }
        }

        private static async Task Compile(Project project)
        {
            string projectRoot = ProjectHelpers.GetRootFolder(project);
            var files = Directory.EnumerateFiles(projectRoot, "*.less", SearchOption.AllDirectories).Where(CanCompile);
            string fileBasePath = string.Empty;

            if (!files.Any()) return;

            fileBasePath = "/" + Path.GetDirectoryName(FileHelpers.RelativePath(projectRoot, files.First())).Replace("\\", "/");

            foreach (string file in files)
            {
                string cssFileName = MarginBase.GetCompiledFileName(file, ".css", WESettings.GetString(WESettings.Keys.LessCompileToLocation));
                var result = await LessCompiler.Compile(file, cssFileName, projectRoot + fileBasePath);

                if (result.IsSuccess)
                    WriteResult(result, cssFileName);
                else
                    Logger.Log(result.Error.Message ?? ("Error compiling LESS file: " + file));
            }
        }

        private static bool CanCompile(string fileName)
        {
            if (EditorExtensionsPackage.DTE.Solution.FindProjectItem(fileName) == null)
                return false;

            if (Path.GetFileName(fileName).StartsWith("_"))
                return false;

            string minFile = MarginBase.GetCompiledFileName(fileName, ".min.css", WESettings.GetString(WESettings.Keys.LessCompileToLocation));
            if (File.Exists(minFile) && WESettings.GetBoolean(WESettings.Keys.LessMinify))
                return true;

            string cssFile = MarginBase.GetCompiledFileName(fileName, ".css", WESettings.GetString(WESettings.Keys.LessCompileToLocation));
            if (!File.Exists(cssFile))
                return false;

            return true;
        }

        private static void WriteResult(CompilerResult result, string cssFileName)
        {
            MinifyFile(result.FileName, result.Result);
            if (!File.Exists(cssFileName))
                return;

            string old = File.ReadAllText(cssFileName);
            if (old == result.Result)
                return;

            ProjectHelpers.CheckOutFileFromSourceControl(cssFileName);
            try
            {
                using (StreamWriter writer = new StreamWriter(cssFileName, false, new UTF8Encoding(true)))
                {
                    writer.Write(result.Result);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void MinifyFile(string lessFileName, string source)
        {
            if (WESettings.GetBoolean(WESettings.Keys.LessMinify))
            {
                string content = MinifyFileMenu.MinifyString(".css", source);
                string minFile = MarginBase.GetCompiledFileName(lessFileName, ".min.css", WESettings.GetString(WESettings.Keys.LessCompileToLocation)); //lessFileName.Replace(".less", ".min.css");
                bool fileExist = File.Exists(minFile);
                string old = fileExist ? File.ReadAllText(minFile) : string.Empty;

                if (old != content)
                {
                    ProjectHelpers.CheckOutFileFromSourceControl(minFile);
                    using (StreamWriter writer = new StreamWriter(minFile, false, new UTF8Encoding(true)))
                    {
                        writer.Write(content);
                    }

                    if (!fileExist)
                        MarginBase.AddFileToProject(lessFileName, minFile);
                }
            }
        }
    }
}