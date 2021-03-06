﻿using System;
using System.IO;
using System.Linq;
using ExcelDna.AddIn.Tasks.Logging;
using Microsoft.Build.Framework;
using ExcelDna.AddIn.Tasks.Utils;

namespace ExcelDna.AddIn.Tasks
{
    public class SetDebuggerOptions : AbstractTask
    {
        private readonly IBuildLogger _log;
        private readonly IExcelDetector _excelDetector;
        private readonly IExcelDnaProject _dte;
        private BuildTaskCommon _common;

        public SetDebuggerOptions()
        {
            _log = new BuildLogger(this, "ExcelDnaSetDebuggerOptions");
            _excelDetector = new ExcelDetector();
            _dte = new ExcelDnaProject(_log);
        }

        public SetDebuggerOptions(IBuildLogger log, IExcelDetector excelDetector, IExcelDnaProject dte)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _excelDetector = excelDetector ?? throw new ArgumentNullException(nameof(excelDetector));
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
        }

        public override bool Execute()
        {
            try
            {
                _log.Debug("Running SetDebuggerOptions MSBuild Task");

                FilesInProject = FilesInProject ?? new ITaskItem[0];
                _log.Debug("Number of files in project: " + FilesInProject.Length);

                var excelExePath = GetExcelPath();
                var addInForDebugging = GetAddInForDebugging(excelExePath);

                LogDiagnostics();

                if (!_dte.TrySetDebuggerOptions(ProjectName, excelExePath, addInForDebugging))
                {
                    _log.Warning("DNA" + "DTE".GetHashCode(), "Unable to set the debugger options within Visual Studio. Please restart Visual Studio and try again.");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, ex.Message);
            }

            // Setting the debugger options is not essential to the build process, thus if anything
            // goes wrong, we'll report errors and warnings, but will not fail the build because of that
            return true;
        }

        private string GetExcelPath()
        {
            var excelExePath = ExcelExePath;

            try
            {
                if (string.IsNullOrWhiteSpace(excelExePath))
                {
                    if (!_excelDetector.TryFindLatestExcel(out excelExePath))
                    {
                        _log.Warning("DNA" + "EXCEL.EXE".GetHashCode(), "Unable to find path where EXCEL.EXE is located");
                        return excelExePath;
                    }
                }

                if (!File.Exists(excelExePath))
                {
                    _log.Warning("DNA" + "EXCEL.EXE".GetHashCode(),
                        "EXCEL.EXE not found on disk at location " + excelExePath);
                }
            }
            finally
            {
                _log.Information("EXCEL.EXE path for debugging: " + excelExePath);
            }

            return excelExePath;
        }

        private string GetAddInForDebugging(string excelExePath)
        {
            var addInForDebugging = AddInForDebugging;

            try
            {
                if (string.IsNullOrWhiteSpace(addInForDebugging))
                {
                    if (!TryGetExcelAddInForDebugging(excelExePath, out addInForDebugging))
                    {
                        _log.Warning("DNA" + "ADDIN".GetHashCode(), "Unable to find add-in to Debug");
                    }
                }
            }
            finally
            {
                _log.Information("Add-In for debugging: " + addInForDebugging);
            }

            return addInForDebugging;
        }

        private bool TryGetExcelAddInForDebugging(string excelExePath, out string addinForDebugging)
        {
            addinForDebugging = null;

            if (!_excelDetector.TryFindExcelBitness(excelExePath, out var excelBitness))
            {
                return false;
            }

            _common = new BuildTaskCommon(FilesInProject, OutDirectory, FileSuffix32Bit, FileSuffix64Bit);

            var outputBuildItems = _common.GetBuildItemsForDnaFiles();

            var firstAddIn = outputBuildItems.FirstOrDefault();
            if (firstAddIn == null) return false;

            switch (excelBitness)
            {
                case Bitness.Bit32:
                {
                    addinForDebugging = firstAddIn.OutputXllFileNameAs32Bit;
                    return true;
                }
                case Bitness.Bit64:
                {
                    addinForDebugging = firstAddIn.OutputXllFileNameAs64Bit;
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// The name of the project being compiled
        /// </summary>
        [Required]
        public string ProjectName { get; set; }

        /// <summary>
        /// The path to EXCEL.EXE that should be used for debugging
        /// This overrides the automatic detection of the latest Excel installed
        /// </summary>
        public string ExcelExePath { get; set; }

        /// <summary>
        /// The path to .XLL file name that should be used for debugging
        /// This overrides the automatic detection depending on Excel's bitness
        /// </summary>
        public string AddInForDebugging { get; set; }        

        /// <summary>
        /// The list of files in the project marked as Content or None
        /// </summary>
        [Required]
        public ITaskItem[] FilesInProject { get; set; }

        /// <summary>
        /// The directory in which the built files were written to
        /// </summary>
        [Required]
        public string OutDirectory { get; set; }

        /// <summary>
        /// The name suffix for 32-bit .dna files
        /// </summary>
        public string FileSuffix32Bit { get; set; }

        /// <summary>
        /// The name suffix for 64-bit .dna files
        /// </summary>
        public string FileSuffix64Bit { get; set; }

        private void LogDiagnostics()
        {
            _log.Debug("----Arguments----");
            _log.Debug("ProjectName: " + ProjectName);
            _log.Debug("ExcelExePath: " + ExcelExePath);
            _log.Debug("AddInForDebugging: " + AddInForDebugging);
            _log.Debug("FilesInProject: " + (FilesInProject ?? new ITaskItem[0]).Length);
            _log.Debug("OutDirectory: " + OutDirectory);
            _log.Debug("FileSuffix32Bit: " + FileSuffix32Bit);
            _log.Debug("FileSuffix64Bit: " + FileSuffix64Bit);
            _log.Debug("-----------------");
        }
    }
}
