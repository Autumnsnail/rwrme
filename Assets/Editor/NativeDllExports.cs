using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NativeDllExports
{
    [MenuItem("Tools/Native DLL/List Exports (select dll)")]
    public static void ListExportsFromFilePanel()
    {
        var path = EditorUtility.OpenFilePanel("Select native DLL", Application.dataPath, "dll");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var exports = ListExports(path);
            Debug.Log($"Exports for: {path}\nCount: {exports.Count}\n" + string.Join("\n", exports));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to list exports for {path}\n{ex}");
        }
    }

    public static List<string> ListExports(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath)) throw new ArgumentException("dllPath is null/empty");
        if (!File.Exists(dllPath)) throw new FileNotFoundException("dll not found", dllPath);

        var bytes = File.ReadAllBytes(dllPath);
        using var ms = new MemoryStream(bytes, writable: false);
        using var br = new BinaryReader(ms);

        // DOS header
        if (br.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Not a PE file (missing MZ).");
        ms.Position = 0x3C;
        var e_lfanew = br.ReadInt32();
        if (e_lfanew <= 0 || e_lfanew >= bytes.Length - 256) throw new InvalidDataException("Invalid e_lfanew.");

        // NT headers
        ms.Position = e_lfanew;
        if (br.ReadUInt32() != 0x00004550) throw new InvalidDataException("Not a PE file (missing PE signature).");

        // FILE_HEADER
        br.ReadUInt16(); // Machine
        var numberOfSections = br.ReadUInt16();
        br.ReadUInt32(); // TimeDateStamp
        br.ReadUInt32(); // PointerToSymbolTable
        br.ReadUInt32(); // NumberOfSymbols
        var sizeOfOptionalHeader = br.ReadUInt16();
        br.ReadUInt16(); // Characteristics

        // OPTIONAL_HEADER
        var optionalHeaderStart = ms.Position;
        var magic = br.ReadUInt16();
        var isPE32Plus = magic == 0x20B;
        var isPE32 = magic == 0x10B;
        if (!isPE32Plus && !isPE32) throw new InvalidDataException($"Unknown PE optional header magic: 0x{magic:X}");

        // DataDirectory offset differs between PE32 and PE32+
        // We skip to the DataDirectory[0] by using known offsets.
        // PE32: data directories start at optionalHeaderStart + 0x60
        // PE32+: start at optionalHeaderStart + 0x70
        ms.Position = optionalHeaderStart + (isPE32Plus ? 0x70 : 0x60);
        var exportRva = br.ReadUInt32();
        var exportSize = br.ReadUInt32();
        if (exportRva == 0 || exportSize == 0) return new List<string>();

        // Section headers
        ms.Position = optionalHeaderStart + sizeOfOptionalHeader;
        var sections = new List<Section>(numberOfSections);
        for (var i = 0; i < numberOfSections; i++)
        {
            var nameBytes = br.ReadBytes(8);
            var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
            br.ReadUInt32(); // VirtualSize
            var virtualAddress = br.ReadUInt32();
            var sizeOfRawData = br.ReadUInt32();
            var pointerToRawData = br.ReadUInt32();
            br.ReadUInt32(); // PointerToRelocations
            br.ReadUInt32(); // PointerToLinenumbers
            br.ReadUInt16(); // NumberOfRelocations
            br.ReadUInt16(); // NumberOfLinenumbers
            br.ReadUInt32(); // Characteristics

            sections.Add(new Section(name, virtualAddress, sizeOfRawData, pointerToRawData));
        }

        long RvaToFileOffset(uint rva)
        {
            foreach (var s in sections)
            {
                if (rva >= s.VirtualAddress && rva < s.VirtualAddress + s.SizeOfRawData)
                {
                    return s.PointerToRawData + (rva - s.VirtualAddress);
                }
            }
            throw new InvalidDataException($"RVA 0x{rva:X} not found in any section.");
        }

        // IMAGE_EXPORT_DIRECTORY (40 bytes)
        ms.Position = RvaToFileOffset(exportRva);
        br.ReadUInt32(); // Characteristics
        br.ReadUInt32(); // TimeDateStamp
        br.ReadUInt16(); // MajorVersion
        br.ReadUInt16(); // MinorVersion
        br.ReadUInt32(); // Name RVA
        br.ReadUInt32(); // Base
        br.ReadUInt32(); // NumberOfFunctions
        var numberOfNames = br.ReadUInt32();
        br.ReadUInt32(); // AddressOfFunctions RVA
        var addressOfNamesRva = br.ReadUInt32();
        br.ReadUInt32(); // AddressOfNameOrdinals RVA

        var exports = new List<string>((int)Math.Min(numberOfNames, 100000));
        if (numberOfNames == 0) return exports;

        ms.Position = RvaToFileOffset(addressOfNamesRva);
        var nameRvas = new uint[numberOfNames];
        for (var i = 0; i < numberOfNames; i++) nameRvas[i] = br.ReadUInt32();

        foreach (var nameRva in nameRvas)
        {
            ms.Position = RvaToFileOffset(nameRva);
            exports.Add(ReadNullTerminatedAscii(br));
        }

        exports.Sort(StringComparer.Ordinal);
        return exports;
    }

    private static string ReadNullTerminatedAscii(BinaryReader br)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var b = br.ReadByte();
            if (b == 0) break;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    private readonly struct Section
    {
        public readonly string Name;
        public readonly uint VirtualAddress;
        public readonly uint SizeOfRawData;
        public readonly uint PointerToRawData;

        public Section(string name, uint virtualAddress, uint sizeOfRawData, uint pointerToRawData)
        {
            Name = name;
            VirtualAddress = virtualAddress;
            SizeOfRawData = sizeOfRawData;
            PointerToRawData = pointerToRawData;
        }
    }
}

