using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace T5ProgressAutogen
{
    class Program
    {
        // Config
        static string sourceDir = @"C:\Users\Username\Documents\GitHub\TOMB5\TOMB5";
        static string progressSrc = Path.Combine(sourceDir, "global\\progress.txt");
        static string objectsStartLine = "// Objects checklist";
        static string autogenStartLine = "// AUTOGEN UNKNOWNS PLEASE FIX ASAP";
        static string gameStartLine = "// GAME";

        // Vars
        static List<ProgressData> fileAutoPD = new List<ProgressData>();
        static List<ProgressData> fileGamePD = new List<ProgressData>();
        static List<string> objectLines = new List<string>();
        static List<ProgressData> codeProgressData = new List<ProgressData>();
        static void Main(string[] args)
        {
            // Just in case
            sourceDir = Path.GetFullPath(sourceDir);

            ParseProgressFile();
            WriteProgressFile(fileGamePD, objectLines);
            WriteReport(fileGamePD);
            ParseCode(writeReport: true, reportType: ReportType.Normal);

            Console.WriteLine("// ======================================");
            Console.WriteLine("// UPDATE FROM CODE");
            Console.WriteLine("// ======================================");
            Console.WriteLine();

            // Let's try to update this
            var testUpdateNoNewStuff = fileGamePD.ToList();
            var notProcessedCodePDs = codeProgressData.ToList();
            var autoList = new List<ProgressData>();
            var matchingMultiple = new List<ProgressData>();
            var wrongNameSomewhere = new List<ProgressData>();
            var wrongFileSomewhere = new List<ProgressData>();
            var updatedStatus = new List<ProgressData>();
            foreach (var codePD in codeProgressData)
            {
                var matchedPDs = testUpdateNoNewStuff.Where(x => x.Address == codePD.Address);
                if (matchedPDs.Count() == 0)
                {
                    // Throw it as unknown, let the humans handle it
                    autoList.Add(codePD);
                    notProcessedCodePDs.Remove(codePD);
                    continue;
                }
                if (matchedPDs.Count() > 1)
                {
                    matchingMultiple.Add(codePD);
                    notProcessedCodePDs.Remove(codePD);
                    Console.WriteLine("Matched multiple?!");
                    Console.WriteLine("Code says:");
                    Console.WriteLine(GetProgressString(codePD, true));
                    Console.WriteLine("Progress says:");
                    foreach (var something in matchedPDs)
                    {
                        Console.WriteLine(GetProgressString(something, true));
                    }
                    Console.WriteLine();
                    continue;
                }
                var match = matchedPDs.First();
                if (match.FuncName != codePD.FuncName)
                {
                    wrongNameSomewhere.Add(codePD);
                    notProcessedCodePDs.Remove(codePD);
                    Console.WriteLine("Got different name?!");
                    Console.WriteLine("Code: {0}", GetProgressString(codePD, true));
                    Console.WriteLine("Prog: {0}", GetProgressString(match, true));
                    Console.WriteLine();
                    continue;
                }
                if (match.SourceFile != codePD.SourceFile)
                {
                    wrongFileSomewhere.Add(codePD);
                    notProcessedCodePDs.Remove(codePD);
                    Console.WriteLine("Got different sourcefile?!");
                    Console.WriteLine("Code: {0}", GetProgressString(codePD, true));
                    Console.WriteLine("Prog: {0}", GetProgressString(match, true));
                    Console.WriteLine();
                    continue;
                }
                if (match.Status != codePD.Status)
                {
                    notProcessedCodePDs.Remove(codePD);
                    // Don't mess with funcs marked as NotInjected, code parser doesn't tell us
                    // about commented inject lines and functions :(
                    if (match.Status != ProgressStatus.NotInjected)
                    {
                        Console.WriteLine("Updating status:");
                        Console.WriteLine("Code: {0}", GetProgressString(codePD, true));
                        Console.WriteLine("Prog: {0}", GetProgressString(match, true));
                        Console.WriteLine();
                        updatedStatus.Add(codePD);
                        match.Status = codePD.Status;
                    }
                    else
                    {

                        Console.WriteLine("Status mismatch, can't update:");
                        Console.WriteLine("Code: {0}", GetProgressString(codePD, true));
                        Console.WriteLine("Prog: {0}", GetProgressString(match, true));
                        Console.WriteLine();
                    }
                    continue;
                }

                // Everything matches :)
                notProcessedCodePDs.Remove(codePD);
            }

            Console.WriteLine("// ======================================");
            Console.WriteLine("// VERIFY PROG STATE");
            Console.WriteLine("// ======================================");
            Console.WriteLine();

            var claimsToBeInjected = testUpdateNoNewStuff.Where(x => x.Status == ProgressStatus.Injected).ToList();
            var areInjected = codeProgressData.Where(x => x.Status == ProgressStatus.Injected).ToList();
            var notActuallyInjected = new List<ProgressData>();
            var autoListInjected = autoList.Where(x => x.Status == ProgressStatus.Injected).ToList();
            foreach (var filePD in claimsToBeInjected)
            {
                if (filePD.Address == "##########")
                {
                    continue;
                }
                var matchedPDs = areInjected.Where(x => x.Address == filePD.Address);
                if (matchedPDs.Count() == 0)
                {
                    notActuallyInjected.Add(filePD);
                    Console.WriteLine("Not actually injected!");
                    Console.WriteLine("Prog: {0}", GetProgressString(filePD, true));
                    Console.WriteLine();
                }
            }

            var DEBUGTEST = areInjected.Where(x => (!claimsToBeInjected.Where(y => y.Address == x.Address).Any() && !autoListInjected.Where(y => y.Address == x.Address).Any())).ToList();


            Console.WriteLine("// ======================================");
            Console.WriteLine("// STATS");
            Console.WriteLine("// ======================================");
            Console.WriteLine();

            Console.WriteLine("{0} code functions not processed.", notProcessedCodePDs.Count());
            Console.WriteLine("{0} code functions on Autogen section.", autoList.Count());
            Console.WriteLine("{0} code functions matched multiple by address. Can't update that.", matchingMultiple.Count());
            Console.WriteLine("{0} code functions have the wrong name somewhere. Can't update that either.", wrongNameSomewhere.Count());
            Console.WriteLine("{0} code functions have been added to a different file than what progress claims. Can't update that.", wrongFileSomewhere.Count());
            Console.WriteLine("{0} functions have had their status updated. YAY!", updatedStatus.Count());
            Console.WriteLine("{0} claim to be injected, actually {1} are injected in code.", claimsToBeInjected.Count(), areInjected.Count());
            Console.WriteLine("{0} functions claim to be injected but they aren't.", notActuallyInjected.Count());

            WriteProgressFile(fileGamePD, objectLines, autoList);
        }

        private static void ParseCode(bool writeReport = false, ReportType reportType = ReportType.Normal)
        {
            var allCPPFiles = Directory.EnumerateFiles(sourceDir, "*.cpp", SearchOption.AllDirectories).ToList();
            var allHeaderFiles = Directory.EnumerateFiles(sourceDir, "*.h", SearchOption.AllDirectories).ToList();
            var unprocessedCPPFiles = allCPPFiles.ToList();
            var unprocessedHeaderFiles = allHeaderFiles.ToList();
            foreach (var currentCPPFile in allCPPFiles)
            {
                var cppLines = File.ReadAllLines(currentCPPFile);
                foreach (var line in cppLines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("INJECT"))
                    {
                        // We've got something decompiled. Need parsing!
                        //Console.WriteLine(trimmedLine);
                        var hexIdx = trimmedLine.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
                        var endIdx = trimmedLine.IndexOf(");", hexIdx);
                        var argsText = trimmedLine.Substring(hexIdx, endIdx - hexIdx).TrimEnd(')', ';', ' ', '\t').Trim();
                        var argsData = argsText.Split(',', StringSplitOptions.TrimEntries);
                        if (argsData.Length != 3)
                        {
                            Console.WriteLine("Error processing INJECT:");
                            Console.WriteLine("Y U DO DIS!: {0}", trimmedLine);
                            continue;
                        }
                        ProgressStatus isInjected = argsData[2] == "replace" ? ProgressStatus.Injected : ProgressStatus.NotInjected;
                        var progData = new ProgressData(argsData[0], isInjected, argsData[1], SanitizeFileName(currentCPPFile, sourceDir));
                        codeProgressData.Add(progData);
                    }
                }
                var headerPath = Path.ChangeExtension(currentCPPFile, ".h");
                var foundHeader = allHeaderFiles.Contains(headerPath);
                unprocessedCPPFiles.Remove(currentCPPFile);
                if (!foundHeader)
                {
                    // I know dllmain won't have a header, don't care either
                    if (Path.GetFileName(currentCPPFile) != "dllmain.cpp")
                    {
                        Console.WriteLine("Couldn't find header file for {0}", currentCPPFile);
                        Console.WriteLine("Looking for {0}", headerPath);
                    }
                    continue;
                }
                var headerLines = File.ReadAllLines(headerPath);
                foreach (var line in headerLines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("#define") && trimmedLine.EndsWith(")") && trimmedLine.Contains("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        // We might have one!
                        //Console.WriteLine(trimmedLine);
                        var progData = GetProgDataFromDefineLine(trimmedLine, currentCPPFile, sourceDir);
                        codeProgressData.Add(progData);
                    }
                }
                unprocessedHeaderFiles.Remove(headerPath);
            }
            if (writeReport)
            {
                WriteCodeParseReport(unprocessedHeaderFiles, unprocessedCPPFiles, reportType);
            }
        }

        static void WriteCodeParseReport(List<string> unprocessedHeaderFiles, List<string> unprocessedCPPFiles, ReportType reportType = ReportType.Normal)
        {
            using (var writer = new StreamWriter("codeparse_autogen.txt"))
            {
                writer.NewLine = "\n";
                var injected = codeProgressData.Where(x => x.Status == ProgressStatus.Injected).ToList();
                var defined = codeProgressData.Where(x => x.Status == ProgressStatus.NotDecompiled).ToList();
                writer.WriteLine("{0} functions are injected", injected.Count);
                if (reportType == ReportType.Excessive)
                {
                    foreach (var injectedFunc in injected)
                    {
                        writer.WriteLine(GetProgressString(injectedFunc, true));
                    }
                }
                writer.WriteLine();
                writer.WriteLine("{0} are defined :(", defined.Count);
                if (reportType == ReportType.Excessive)
                {
                    foreach (var definedFunc in defined)
                    {
                        writer.WriteLine(GetProgressString(definedFunc, true));
                    }
                }
                writer.WriteLine();
                writer.WriteLine("These {0} headers I didn't parse because there's no CPP :(", unprocessedHeaderFiles.Count);
                foreach (var headerPath in unprocessedHeaderFiles)
                {
                    writer.WriteLine(SanitizeFileName(headerPath, sourceDir));
                }
                writer.WriteLine();
                writer.WriteLine("These {0} CPP files I didn't parse because... why?!", unprocessedCPPFiles.Count);
                foreach (var cppPath in unprocessedCPPFiles)
                {
                    writer.WriteLine(SanitizeFileName(cppPath, sourceDir));
                }
            }
        }

        private static ProgressData GetProgDataFromDefineLine(string trimmedLine, string currentCPPFile, string sourceDir)
        {
            var withoutDefine = trimmedLine.Substring(7).Trim();
            var firstSeparatorIdx = withoutDefine.IndexOfAny(new[] { ' ', '\t', '(' });
            var funcName = withoutDefine.Substring(0, firstSeparatorIdx).Trim();
            var hexIdx = withoutDefine.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
            var hexEndIdx = withoutDefine.IndexOfAny(new[] { ' ', '\t', ')', ',' }, hexIdx);
            var address = withoutDefine.Substring(hexIdx, hexEndIdx - hexIdx);
            //Console.WriteLine("{0}\t\t{1}\t// {2}", addy, funcName, headerPath.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/'));
            return new ProgressData(address, ProgressStatus.NotDecompiled, funcName, SanitizeFileName(currentCPPFile, sourceDir));
        }

        static string SanitizeFileName(string filePath, string sourceDirPath)
        {
            return filePath.Substring(sourceDirPath.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
        }

        static void ParseProgressFile()
        {
            // Let's go!
            var progLines = File.ReadAllLines(progressSrc);
            string currentSourceFile = "NO_SOURCE_FILE_DETECTED_YET";
            bool inAutoGen = false;
            bool inObjects = false;
            foreach (var line in progLines)
            {
                var trimmedLine = line.Trim();
                if (!inObjects)
                {
                    if (trimmedLine == objectsStartLine)
                    {
                        // Funcs first, objects second, so we're done!
                        inObjects = true;
                    }
                    else if (trimmedLine == autogenStartLine)
                    {
                        inAutoGen = true;
                    }
                    else if (trimmedLine == gameStartLine)
                    {
                        inAutoGen = false;
                    }
                }
                if (inObjects)
                {
                    // We just store this to spit them out later
                    objectLines.Add(line);
                    continue;
                }
                if (line.StartsWith("\t\t") && trimmedLine.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))
                {
                    //Console.WriteLine("Got source file marker: {0}", trimmedLine);
                    //Console.WriteLine("File {0}", File.Exists(Path.Combine(sourceDir, trimmedLine)) ? "exists" : "missing!");
                    currentSourceFile = trimmedLine;
                    continue;
                }
                if (trimmedLine.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("##########\t"))
                {
                    if (trimmedLine.Contains("\t\t") || trimmedLine.Contains("\t+\t") || trimmedLine.Contains("\tx\t", StringComparison.OrdinalIgnoreCase))
                    {
                        // We got one!
                        var progDataText = trimmedLine.Trim().Split('\t');
                        var progData = new ProgressData(progDataText[0], ProgressStatus.NotDecompiled, progDataText[progDataText.Length - 1], currentSourceFile);
                        if (progDataText.Length > 2)
                        {
                            progData.Status.FromProgress(progDataText[1]);
                        }
                        if (inAutoGen)
                        {
                            fileAutoPD.Add(progData);
                        }
                        else
                        {
                            fileGamePD.Add(progData);
                        }
                    }
                }
            }
        }

        // This matches exactly the current format if you don't supply an autoList
        // It won't match through binary comparison because the current
        // progress.txt file is missing some line feeds and sometimes adds tabs on empty lines.
        private static void WriteProgressFile(List<ProgressData> fullList, List<string> objectLines, List<ProgressData> autoList = default)
        {
            using (var writer = new StreamWriter("progress_autogen.txt"))
            {
                writer.NewLine = "\n";
                writer.WriteLine("TOMB5 progress.");
                writer.WriteLine();
                writer.WriteLine("empty space for functions not decompiled yet.");
                writer.WriteLine("+ for functions decompiled and injected.");
                writer.WriteLine("x for functions decompiled but not injected, for one reason or another.");
                writer.WriteLine();
                if (autoList != default && autoList.Any())
                {
                    writer.WriteLine("// ======================================");
                    writer.WriteLine(autogenStartLine);
                    writer.WriteLine("// ======================================");
                    var auto_groupedBySourceName = autoList.GroupBy(x => x.SourceFile).ToList();
                    foreach (var currentSource in auto_groupedBySourceName)
                    {
                        writer.WriteLine();
                        writer.WriteLine("\t\t{0}", currentSource.Key);
                        writer.WriteLine();
                        foreach (var progData in currentSource)
                        {
                            writer.WriteLine(GetProgressString(progData));
                        }
                        writer.WriteLine();
                    }
                }
                bool stillInGAME = true;
                writer.WriteLine("// ======================================");
                writer.WriteLine(gameStartLine);
                writer.WriteLine("// ======================================");
                var groupedBySourceName = fullList.GroupBy(x => x.SourceFile).ToList();
                foreach (var currentSource in groupedBySourceName)
                {
                    if (stillInGAME && currentSource.Key.StartsWith("specific/", StringComparison.OrdinalIgnoreCase))
                    {
                        stillInGAME = false;
                        writer.WriteLine("// ======================================");
                        writer.WriteLine("// SPECIFIC");
                        writer.WriteLine("// ======================================");
                    }
                    writer.WriteLine();
                    writer.WriteLine("\t\t{0}", currentSource.Key);
                    writer.WriteLine();
                    foreach (var progData in currentSource)
                    {
                        writer.WriteLine(GetProgressString(progData));
                    }
                    writer.WriteLine();
                }
                writer.WriteLine("// ======================================");
                foreach (var objectLine in objectLines)
                {
                    writer.WriteLine(objectLine);
                }
            }
        }

        static string GetProgressString(ProgressData srcData, bool includeSourceComment = false)
        {
            string result = srcData.Address + "\t" + srcData.Status.ToProgress() + "\t" + srcData.FuncName;
            if (includeSourceComment)
            {
                result += "\t// " + srcData.SourceFile;
            }
            return result;
        }



        private static void WriteReport(List<ProgressData> fullList)
        {
            var MarkedAsInjected = fullList.Where(x => x.Status == ProgressStatus.Injected).ToList();
            var MarkedAsNotInjected = fullList.Where(x => x.Status == ProgressStatus.NotInjected).ToList();
            var duplicatesByAddress = fullList.GroupBy(x => x.Address).Where(x => x.Count() > 1).ToList();
            var duplicatesByName = fullList.GroupBy(x => x.FuncName).Where(x => x.Count() > 1).ToList();
            using (var writer = new StreamWriter("report_autogen.txt"))
            {
                writer.NewLine = "\n";
                writer.WriteLine("TOMB5 progress autogen report from {0}Z", DateTime.UtcNow.ToString("s"));
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine("Duplicates by address: {0}", duplicatesByAddress.Count);
                writer.WriteLine();
                foreach (var addy in duplicatesByAddress)
                {
                    foreach (var srcData in addy)
                    {
                        writer.WriteLine(GetProgressString(srcData, true));
                    }
                    writer.WriteLine();
                }
                writer.WriteLine("Duplicates by FuncName: {0}", duplicatesByName.Count);
                writer.WriteLine();
                foreach (var addy in duplicatesByName)
                {
                    foreach (var srcData in addy)
                    {
                        writer.WriteLine(GetProgressString(srcData, true));
                    }
                    writer.WriteLine();
                }
                writer.WriteLine("Marked as not injected: {0}", MarkedAsNotInjected.Count);
                writer.WriteLine();
                foreach (var srcData in MarkedAsNotInjected)
                {
                    writer.WriteLine(GetProgressString(srcData, true));
                }
            }
        }
    }
}
