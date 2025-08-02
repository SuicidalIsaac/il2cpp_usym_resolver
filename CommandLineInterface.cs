using System.Text.Json;

namespace Il2CppSymbolReader;

/// <summary>
/// 命令行接口，提供所有符号解析功能
/// </summary>
public static class CommandLineInterface
{
    private static readonly Dictionary<string, (int minArgs, string usage, Func<string[], int> handler)> Commands = 
        new Dictionary<string, (int, string, Func<string[], int>)>(StringComparer.OrdinalIgnoreCase)
    {
        ["read"]    = (2, "read <usym_file_path> [address]", HandleRead),
        ["lookup"]  = (3, "lookup <usym_file_path> <address>", HandleLookup),
        ["dump"]    = (2, "dump <usym_file_path>", HandleDump),
        ["resolve"] = (3, "resolve <usym_file_path> <address1> [address2] ...", HandleResolve),
        ["help"]    = (1, "help", _ => ShowUsage()),
        ["-h"]      = (1, "help", _ => ShowUsage()),
        ["--help"]  = (1, "help", _ => ShowUsage())
    };
    /// <summary>
    /// 命令行接口，提供所有符号解析功能
    /// </summary>
    public static int Execute(string[] args)
    {
        try
        {
            Console.WriteLine("IL2CPP Symbol Reader\n====================");
            
            if (args.Length == 0) 
                return ShowUsage();
            
            if (!Commands.TryGetValue(args[0], out var cmd))
            {
                Console.WriteLine($"Unknown command: {args[0]}");
                return ShowUsage();
            }
            
            if (args.Length < cmd.minArgs)
            {
                Console.WriteLine($"Usage: {cmd.usage}");
                return 1;
            }
            
            return cmd.handler(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
    private static int HandleRead(string[] args)
    {
        ReadUsymFile(args[1], args.Length > 2 ? args[2] : null);
        return 0;
    }

    private static int HandleLookup(string[] args)
    {
        LookupAddress(args[1], args[2]);
        return 0;
    }

    private static int HandleDump(string[] args)
    {
        DumpAllSymbols(args[1]);
        return 0;
    }

    private static int HandleResolve(string[] args)
    {
        ResolveAddresses(args[1], args[2..]);
        return 0;
    }
    private static int ShowUsage()
    {
        Console.WriteLine(string.Join(Environment.NewLine, new[]
        {
            "Usage:",
            "  read <usym_file_path> [address]  - Read usym file info and optionally lookup address",
            "  lookup <usym_file_path> <address> - Lookup specific address",
            "  dump <usym_file_path>           - Dump all symbols from file",
            "  resolve <usym_file_path> <addr1> [addr2] ... - Resolve multiple addresses to source locations",
            "  help                            - Show this help message",
            "\nAddress format:",
            "  Decimal: 1234567890 (C++ StackTrace output format)",
            "  Hex: 0x1234ABCD or 1234ABCD (for manual testing)",
            "\nExamples:",
            "  Il2CppSymbolReader read il2cpp.usym",
            "  Il2CppSymbolReader lookup il2cpp.usym 1234567890",
            "  Il2CppSymbolReader dump il2cpp.usym",
            "  Il2CppSymbolReader resolve il2cpp.usym 1234567890 2345678901 3456789012"
        }));
        
        return 0;
    }
    
    private static void ReadUsymFile(string filePath, string? addressStr)
    {
        try
        {
            using var reader = new UsymReader(filePath);
            var header = reader.Header;
            
            Console.WriteLine($"Successfully loaded usym file: {filePath}");
            Console.WriteLine(string.Join(Environment.NewLine, new[]
            {
                $"Magic: {header.Magic}",
                $"Version: {header.Version}",
                $"Line Count: {header.LineCount}",
                $"ID: {reader.GetString(header.Id)}",
                $"OS: {reader.GetString(header.Os)}",
                $"Arch: {reader.GetString(header.Arch)}",
                ""
            }));
    
            if (string.IsNullOrEmpty(addressStr)) return;
            
            if (TryParseAddress(addressStr, out ulong address))
                LookupAddressInternal(reader, address);
            else
                Console.WriteLine($"Invalid address format: {addressStr}");
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
    Console.WriteLine($"Looking up address: {address}\n");
    
    if (reader.FindSymbol(address) is { } symbol)
        Console.WriteLine(string.Join(Environment.NewLine, new[]
        {
            $"Found symbol:",
            $"  File: {symbol.FileName}",
            $"  Line: {symbol.LineNumber}",
            $"  Method Index: {symbol.MethodIndex}",
            $"  Address: {symbol.Address}",
            $"  Parent: {symbol.Parent}"
        }));
    else
        Console.WriteLine("No symbol found for this address.\n");

    var frames = reader.GetStackFrames(address);
    if (frames.Count == 0) return;
    
    Console.WriteLine("Stack frames (including inlined functions):");
    for (int i = 0; i < frames.Count; i++)
        Console.WriteLine(string.Join(Environment.NewLine, new[]
        {
            $"  Frame {i}:",
            $"    File: {frames[i].FileName}",
            $"    Line: {frames[i].LineNumber}" +
            $"    Method Index: {frames[i].MethodIndex}",
            $"    Address: {frames[i].Address}" }));
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