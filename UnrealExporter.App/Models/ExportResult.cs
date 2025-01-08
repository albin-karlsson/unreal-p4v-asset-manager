using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnrealExporter.App.Models
{
    public class ExportResult
    {
        public string[] ExportedFiles { get; set; } = Array.Empty<string>();
        public required bool Success { get; init; }
    }
}
