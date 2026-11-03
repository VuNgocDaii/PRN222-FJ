using System;
using System.Collections.Generic;

namespace QuanLyCuDan.Models;

public partial class SchemaVersion
{
    public string VersionCode { get; set; } = null!;

    public DateTime InstalledAt
    {
        get; set;
    }
}
