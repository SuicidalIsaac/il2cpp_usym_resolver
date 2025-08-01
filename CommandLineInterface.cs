using System.Text.Json;

namespace Il2CppSymbolReader;

/// <summary>
/// 命令行接口，提供所有符号解析功能
/// </summary>
public static class CommandLineInterface
{
    /// <summary>
    /// 命令行处理入口点
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>退出代码</returns>
    public static int Execute(string[] args)
    {
        try
        {
            Console.WriteLine("IL2CPP Symbol Reader");
            Console.WriteLine("====================");
            
            if (args.Length == 0)
            {
                ShowUsage();
                return 0;
            }
            
            var command = args[0].ToLower();
            
            return command switch
            {
                "read" => HandleReadCommand(args),
                "lookup" => HandleLookupCommand(args),
                "dump" => HandleDumpCommand(args),
                "resolve" => HandleResolveCommand(args),
                "help" or "-h" or "--help" => ShowUsage(),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
    
    private static int HandleReadCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: read <usym_file_path> [address]");
            return 1;
        }
        
        ReadUsymFile(args[1], args.Length > 2 ? args[2] : null);
        return 0;
    }
    
    private static int HandleLookupCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: lookup <usym_file_path> <address>");
            return 1;
        }
        
        LookupAddress(args[1], args[2]);
        return 0;
    }
    
    private static int HandleDumpCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dump <usym_file_path>");
            return 1;
        }
        
        DumpAllSymbols(args[1]);
        return 0;
    }
    
    private static int HandleResolveCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: resolve <usym_file_path> <address1> [address2] [address3] ...");
            return 1;
        }
        
        ResolveAddresses(args[1], args.Skip(2).ToArray());
        return 0;
    }
    
    private static int HandleUnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        ShowUsage();
        return 1;
    }
    
    private static int ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  read <usym_file_path> [address]  - Read usym file info and optionally lookup address");
        Console.WriteLine("  lookup <usym_file_path> <address> - Lookup specific address");
        Console.WriteLine("  dump <usym_file_path>           - Dump all symbols from file");
        Console.WriteLine("  resolve <usym_file_path> <addr1> [addr2] ... - Resolve multiple addresses to source locations");
        Console.WriteLine("  help                            - Show this help message");
        Console.WriteLine();
        Console.WriteLine("Address format:");
        Console.WriteLine("  Decimal: 1234567890 (C++ StackTrace output format)");
        Console.WriteLine("  Hex: 0x1234ABCD or 1234ABCD (for manual testing)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Il2CppSymbolReader read il2cpp.usym");
        Console.WriteLine("  Il2CppSymbolReader lookup il2cpp.usym 1234567890");
        Console.WriteLine("  Il2CppSymbolReader dump il2cpp.usym");
        Console.WriteLine("  Il2CppSymbolReader resolve il2cpp.usym 1234567890 2345678901 3456789012");
        
        return 0;
    }
    
    private static void ReadUsymFile(string filePath, string? addressStr)
    {
        try
        {
            using var reader = new UsymReader(filePath);
            
            Console.WriteLine($"Successfully loaded usym file: {filePath}");
            Console.WriteLine($"Magic: {reader.Header.Magic}");
            Console.WriteLine($"Version: {reader.Header.Version}");
            Console.WriteLine($"Line Count: {reader.Header.LineCount}");
            Console.WriteLine($"ID: {reader.GetString(reader.Header.Id)}");
            Console.WriteLine($"OS: {reader.GetString(reader.Header.Os)}");
            Console.WriteLine($"Arch: {reader.GetString(reader.Header.Arch)}");
            Console.WriteLine();
            
            if (!string.IsNullOrEmpty(addressStr))
            {
                if (TryParseAddress(addressStr, out ulong address))
                {
                    LookupAddressInternal(reader, address);
                }
                else
                {
                    Console.WriteLine($"Invalid address format: {addressStr}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading usym file: {ex.Message}");
        }
    }
    
    private static void LookupAddress(string filePath, string addressStr)
    {
        if (!TryParseAddress(addressStr, out ulong address))
        {
            Console.WriteLine($"Invalid address format: {addressStr}");
            return;
        }
        
        try
        {
            using var reader = new UsymReader(filePath);
            LookupAddressInternal(reader, address);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    private static void LookupAddressInternal(UsymReader reader, ulong address)
    {
        Console.WriteLine($"Looking up address: {address}");
        Console.WriteLine();
        
        // 查找单个符号
        var symbol = reader.FindSymbol(address);
        if (symbol.HasValue)
        {
            Console.WriteLine("Found symbol:");
            Console.WriteLine($"  File: {symbol.Value.FileName}");
            Console.WriteLine($"  Line: {symbol.Value.LineNumber}");
            Console.WriteLine($"  Method Index: {symbol.Value.MethodIndex}");
            Console.WriteLine($"  Address: {symbol.Value.Address}");
            Console.WriteLine($"  Parent: {symbol.Value.Parent}");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("No symbol found for this address.");
            Console.WriteLine();
        }
        
        // 获取完整的堆栈帧（包括内联函数）
        var frames = reader.GetStackFrames(address);
        if (frames.Count > 0)
        {
            Console.WriteLine("Stack frames (including inlined functions):");
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                Console.WriteLine($"  Frame {i}:");
                Console.WriteLine($"    File: {frame.FileName}");
                Console.WriteLine($"    Line: {frame.LineNumber}");
                Console.WriteLine($"    Method Index: {frame.MethodIndex}");
                Console.WriteLine($"    Address: {frame.Address}");
                Console.WriteLine();
            }
        }
    }
    
    private static void DumpAllSymbols(string filePath)
    {
        try
        {
            using var reader = new UsymReader(filePath);
            using var writer = new StreamWriter("dump.txt", false);
            
            Console.WriteLine($"Dumping all symbols from: {filePath} to dump.txt");
            writer.WriteLine($"Dumping all symbols from: {filePath}");
            writer.WriteLine($"Total symbols: {reader.Header.LineCount}");
            writer.WriteLine();
            
            int count = 0;
            foreach (var symbol in reader.GetAllSymbols())
            {
                writer.WriteLine($"Symbol {count++}:");
                writer.WriteLine($"  Address: {symbol.Address}");
                writer.WriteLine($"  File: {symbol.FileName}");
                writer.WriteLine($"  Line: {symbol.LineNumber}");
                writer.WriteLine($"  Method Index: {symbol.MethodIndex}");
                if (symbol.Parent != 0xFFFFFFFF)
                    writer.WriteLine($"  Parent: {symbol.Parent}");
                writer.WriteLine();
            }
            
            Console.WriteLine($"Successfully dumped {count} symbols to dump.txt");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    private static void ResolveAddresses(string filePath, string[] addresses)
    {
        try
        {
            using var resolver = new Il2CppAddressResolver(filePath);
            
            Console.WriteLine($"Resolving {addresses.Length} addresses using: {filePath}");
            Console.WriteLine();
            
            var results = new List<AddressResolutionResult>();
            
            foreach (var addressStr in addresses)
            {
                var result = resolver.ResolveAddress(addressStr);
                if (result.HasValue)
                {
                    results.Add(result.Value);
                    Console.WriteLine($"Address: {addressStr}");
                    Console.WriteLine(Il2CppAddressResolver.FormatStackTrace(result.Value));
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"Failed to resolve address: {addressStr}");
                    Console.WriteLine();
                }
            }
            
            if (results.Count > 0)
            {
                Console.WriteLine("=== JSON Export ===");
                Console.WriteLine(Il2CppAddressResolver.ExportToJson(results));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    private static bool TryParseAddress(string addressStr, out ulong address)
    {
        address = 0;
        
        if (string.IsNullOrEmpty(addressStr))
            return false;
            
        // 优先尝试解析为十进制（C++代码使用%zu格式，产生十进制字符串）
        if (ulong.TryParse(addressStr, out address))
            return true;
            
        // 处理十六进制格式（用于手动测试）
        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(addressStr[2..], System.Globalization.NumberStyles.HexNumber, null, out address);
        }
        
        // 尝试解析为十六进制（无0x前缀，用于手动测试）
        if (addressStr.All(c => char.IsDigit(c) || "ABCDEFabcdef".Contains(c)))
        {
            return ulong.TryParse(addressStr, System.Globalization.NumberStyles.HexNumber, null, out address);
        }
        
        return false;
    }
}