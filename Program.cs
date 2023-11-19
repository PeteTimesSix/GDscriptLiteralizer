using StringLiteralizer;
using System.Text;
using System.Text.RegularExpressions;

namespace StringLiteralizer
{

    public class Program
    {
        const StringSplitOptions SPLIT_OPTIONS = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

        static void Main(string[] args)
        {
            var fileDataDict = ParseGDscript();
            InsertLocalStaticFuncsRecursive(fileDataDict);
            ReplaceConstantsWithStringLiterals(fileDataDict);
            ReplaceExtendsWithStringLiterals(fileDataDict);
            RemoveStrongTyping(fileDataDict);


            foreach (var (path, file) in fileDataDict)
            {
                File.WriteAllText(path, file.RawText);
            }
        }

        private static void InsertLocalStaticFuncsRecursive(Dictionary<string, GDscriptFileData> fileDataDict)
        {
            bool anyAdditions;
            do
            {
                anyAdditions = InsertLocalStaticFuncs(fileDataDict);
            } while (anyAdditions);
        }

        private static Dictionary<string, GDscriptFileData> ParseGDscript()
        {
            Dictionary<string, GDscriptFileData> fileDataDict = new();
            var workingDirectoryPath = Directory.GetCurrentDirectory();
            foreach (var directory in Directory.GetDirectories(workingDirectoryPath, "*", SearchOption.AllDirectories))
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (!file.EndsWith(".gd"))
                        continue;
                    var relativePath = file.Substring(workingDirectoryPath.Length + 1);
                    var fileData = new GDscriptFileData() { Path = relativePath };
                    fileDataDict[file] = fileData;
                    fileData.RawText = File.ReadAllText(file);
                    using (var stream = fileData.RawText.ToStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            bool inStaticFunc = false;
                            string staticFunc = "";
                            string? staticFuncName = null;
                            while (!sr.EndOfStream)
                            {
                                var line = sr.ReadLine();
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                var whitespaceSplit = line.Split((char[]?)null, SPLIT_OPTIONS);
                                if (inStaticFunc && line[0] != '\t')
                                {
                                    inStaticFunc = false;
                                    fileData.StaticFuncs[staticFuncName] = staticFunc;
                                    staticFunc = "";
                                    staticFuncName = null;
                                }

                                if (whitespaceSplit.Length == 0)
                                    continue;

                                if (whitespaceSplit[0].ToLower() == "static" && whitespaceSplit[1].ToLower() == "func")
                                {
                                    inStaticFunc = true;
                                    staticFuncName = whitespaceSplit[2].Split('(')[0];
                                }
                                if (whitespaceSplit.Length > 1 && whitespaceSplit[0].ToLower() == "extends")
                                {
                                    fileData.Extends = whitespaceSplit[1];
                                    continue;
                                }
                                if (whitespaceSplit.Length > 1 && whitespaceSplit[0].ToLower() == "class_name")
                                {
                                    fileData.ClassName = whitespaceSplit[1];
                                    continue;
                                }
                                
                                
                                if(inStaticFunc) 
                                {
                                    staticFunc += line + "\n";
                                }

                                var assingment = line.Split("=", SPLIT_OPTIONS);
                                if (assingment.Length > 1)
                                {
                                    var assignTo = assingment[0];
                                    var assignValue = assingment[1];

                                    var assignToSplit = assignTo.Split((char[]?)null, SPLIT_OPTIONS);
                                    if (assignToSplit.Length > 1)
                                    {
                                        if (assignToSplit[0].ToLower() == "const")
                                        {
                                            fileData.DeclaredConstants[assignToSplit[1]] = assignValue; 
                                        }
                                    }
                                }
                            }

                            if (inStaticFunc)
                            {
                                inStaticFunc = false;
                                fileData.StaticFuncs[staticFuncName] = staticFunc;
                                staticFunc = "";
                                staticFuncName = null;
                            }
                        }
                    }
                }
            }
            return fileDataDict;
        }

        private static bool InsertLocalStaticFuncs(Dictionary<string, GDscriptFileData> fileDataDict)
        {
            bool anyAdditions = false;
            var allStaticFuncs = new Dictionary<(string withClass, string withoutClass), string>();
            foreach(var fileData in fileDataDict.Values) 
            {
                if (fileData.ClassName == null)
                    continue;

                foreach(var (staticFuncName, staticFunc) in fileData.StaticFuncs)
                    allStaticFuncs[(fileData.ClassName+"."+staticFuncName, staticFuncName)] = staticFunc;
            }

            foreach (var fileData in fileDataDict.Values)
            {
                Dictionary<string, string> toAppend = new();
                foreach (var (staticFuncName, staticFunc) in allStaticFuncs)
                {
                    if(fileData.RawText.Contains(staticFuncName.withClass))
                    {
                        anyAdditions = true;
                        toAppend[staticFuncName.withoutClass] = staticFunc;
                        fileData.RawText = fileData.RawText.Replace(staticFuncName.withClass, staticFuncName.withoutClass);
                    }
                }
                foreach(var (toAppendName, toAppendFunc) in toAppend)
                {
                    fileData.RawText += "\n\n" + toAppendFunc;
                }
            }
            return anyAdditions;
        }

        private static void ReplaceConstantsWithStringLiterals(Dictionary<string, GDscriptFileData> fileDataDict) 
        {
            var allConstants = new Dictionary<string, string>();
            foreach (var fileData in fileDataDict.Values)
            {
                if (fileData.ClassName == null)
                    continue;

                foreach (var (declaredConstant, value) in fileData.DeclaredConstants)
                    allConstants[fileData.ClassName + "." + declaredConstant] = value;
            }
            foreach (var fileData in fileDataDict.Values)
            {
                Dictionary<string, string> toAppend = new();
                foreach (var (constantName, value) in allConstants)
                {
                    if (fileData.RawText.Contains(constantName))
                    {
                        fileData.RawText = fileData.RawText.Replace(constantName, value);
                    }
                }
            }
        }

        private static void ReplaceExtendsWithStringLiterals(Dictionary<string, GDscriptFileData> fileDataDict)
        {
            var allClasses = new Dictionary<string, GDscriptFileData>();
            foreach (var fileData in fileDataDict.Values)
            {
                if (fileData.ClassName == null)
                    continue;

                allClasses[fileData.ClassName] = fileData;
            }
            foreach (var fileData in fileDataDict.Values)
            {
                if(fileData.Extends != null && allClasses.ContainsKey(fileData.Extends))
                {
                    fileData.RawText = fileData.RawText.Replace(fileData.Extends, allClasses[fileData.Extends].ResPath);
                }
            }
        }

        private static void RemoveStrongTyping(Dictionary<string, GDscriptFileData> fileDataDict)
        {
            var allClasses = new Dictionary<string, GDscriptFileData>();
            foreach (var fileData in fileDataDict.Values)
            {
                if (fileData.ClassName == null)
                    continue;

                allClasses[fileData.ClassName] = fileData;
            }
            foreach (var fileData in fileDataDict.Values)
            {
                foreach(var className in allClasses.Values.Select(c => c.ClassName)) 
                {
                    fileData.RawText = Regex.Replace(fileData.RawText, $": *{className}", "");
                    fileData.RawText = Regex.Replace(fileData.RawText, $" +as +{className}", "");
                }
            }
        }
    }
}