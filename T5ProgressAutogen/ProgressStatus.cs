using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace T5ProgressAutogen
{
    public enum ProgressStatus
    {
        Injected,
        NotInjected,
        NotDecompiled
    }

    public enum ReportType
    {
        Normal,
        Excessive = 999
    }

    static class ProgressDataExtensions
    {

        public static string ToProgress(this ProgressStatus value)
        {
            switch (value)
            {
                case ProgressStatus.Injected:
                    return "+";
                case ProgressStatus.NotInjected:
                    return "x";
                default:
                    return "";
            }
        }

        public static void FromProgress(this ref ProgressStatus value, string progress)
        {
            if (progress == "+")
            {
                value = ProgressStatus.Injected;
                return;
            }
            if (progress == "x")
            {
                value = ProgressStatus.NotInjected;
                return;
            }
            value = ProgressStatus.NotDecompiled;
        }
    }

    class ProgressData
    {
        public string SourceFile;
        public string Address;
        public ProgressStatus Status;
        public string FuncName;

        public ProgressData(string address, ProgressStatus status, string funcName, string sourceFile)
        {
            Address = address;
            Status = status;
            FuncName = funcName;
            SourceFile = sourceFile;
        }

        public ProgressData(string address, string status, string funcName, string sourceFile) : this(address, ProgressStatus.NotDecompiled, funcName, sourceFile)
        {
            Status.FromProgress(status);
        }
    }
}
