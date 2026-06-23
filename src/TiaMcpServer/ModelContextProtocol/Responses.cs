using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseMessage
    {
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
    }

    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseSoftwareInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceItemInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseBlockInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public string? MemoryLayout { get; set; }
        public bool? IsConsistent { get; set; }
        public string? HeaderName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }
    public class ResponseBlocksWithHierarchy : ResponseMessage
    {
        public BlockGroupInfo? Root { get; set; }
    }

    public class ResponseTypeInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public string? Namespace { get; set; }
        public bool? IsConsistent { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseProjectInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
    }

    public class ResponseConnect : ResponseMessage
    {
    }

    public class ResponseDisconnect : ResponseMessage
    {
    }

    public class ResponseState : ResponseMessage
    {
        public bool? IsConnected { get; set; }
        public string? Project { get; set; }
        public string? Session { get; set; }
    }

    public class ResponseGetProjects : ResponseMessage
    {
        public IEnumerable<ResponseProjectInfo>? Items { get; set; }
    }

    public class ResponseOpenProject : ResponseMessage
    {
    }

    public class ResponseSaveProject : ResponseMessage
    {
    }

    public class ResponseSaveAsProject : ResponseMessage
    {
    }

    public class ResponseArchiveProject : ResponseMessage
    {
    }

    public class ResponseRetrieveProject : ResponseMessage
    {
    }

    public class ResponseCloseProject : ResponseMessage
    {
    }

    public class ResponseTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseProjectTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseSoftwareTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseDevices : ResponseMessage
    {
        public IEnumerable<ResponseDeviceInfo>? Items { get; set; }
    }
    
    public class ResponseCompileSoftware : ResponseMessage
    {
    }
    
    public class ResponseBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseExportBlock : ResponseMessage
    {
    }

    public class ResponseImportBlock : ResponseMessage
    {
    }

    public class ResponseExportBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }

    public class ResponseTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    public class ResponseExportType : ResponseMessage
    {
    }

    public class ResponseImportType : ResponseMessage
    {
    }

    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }
    }

    public class ResponseExportAsDocuments : ResponseMessage
    {
    }

    public class ResponseExportBlocksAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseImportFromDocuments : ResponseMessage
    {
    }

    public class ResponseImportBlocksFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseCreateProject : ResponseMessage
    {
    }

    public class ResponseCompileHardware : ResponseMessage
    {
    }

    public class ResponsePlcTagTableInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponsePlcTagTables : ResponseMessage
    {
        public IEnumerable<ResponsePlcTagTableInfo>? Items { get; set; }
    }

    public class ResponsePlcTagInfo : ResponseMessage
    {
        public string? Name { get; set; }
        public string? DataTypeName { get; set; }
        public string? LogicalAddress { get; set; }
        public string? Comment { get; set; }
    }

    public class ResponsePlcTags : ResponseMessage
    {
        public IEnumerable<ResponsePlcTagInfo>? Items { get; set; }
    }

    public class ResponseExportPlcTagTable : ResponseMessage
    {
    }

    public class ResponseImportPlcTagTable : ResponseMessage
    {
    }

    public class ResponseHmiScreenInfo : ResponseMessage
    {
        public string? Name { get; set; }
    }

    public class ResponseHmiScreens : ResponseMessage
    {
        public IEnumerable<ResponseHmiScreenInfo>? Items { get; set; }
    }

    public class ResponseExportHmiScreen : ResponseMessage
    {
    }

    public class ResponseImportHmiScreen : ResponseMessage
    {
    }

    public class ResponseLibraries : ResponseMessage
    {
        public IEnumerable<string>? Items { get; set; }
    }

    public class ResponseLibraryMasterCopies : ResponseMessage
    {
        public IEnumerable<string>? Items { get; set; }
    }

    public class ResponseCopyFromLibrary : ResponseMessage
    {
    }

    public class ResponseNetworkInterfaces : ResponseMessage
    {
        public IEnumerable<Dictionary<string, string>>? Items { get; set; }
    }

    public class ResponseSubnets : ResponseMessage
    {
        public IEnumerable<Dictionary<string, string>>? Items { get; set; }
    }

    public class ResponseAddDevice : ResponseMessage
    {
    }

    public class ResponseRemoveDevice : ResponseMessage
    {
    }

    public class ResponseCreateSubnet : ResponseMessage
    {
    }

    public class ResponseConnectToSubnet : ResponseMessage
    {
    }

    public class ResponseSetNetworkAttribute : ResponseMessage
    {
    }

    // Phase 6 — Download & Online
    public class ResponseDownloadToDevice : ResponseMessage
    {
        public Dictionary<string, string>? Items { get; set; }
    }

    public class ResponseGoOnline : ResponseMessage
    {
        public Dictionary<string, string>? Items { get; set; }
    }

    public class ResponseGoOffline : ResponseMessage
    {
    }

    // Phase 7 — Safety
    public class ResponseSafetyInfo : ResponseMessage
    {
        public Dictionary<string, string>? Items { get; set; }
    }

    public class ResponseCompileSafety : ResponseMessage
    {
        public Dictionary<string, string>? Items { get; set; }
    }

    // Phase 8 — Hardware Catalog
    public class ResponseSearchHardwareCatalog : ResponseMessage
    {
        public IEnumerable<Dictionary<string, string>>? Items { get; set; }
    }
}
