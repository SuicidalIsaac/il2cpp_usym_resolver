using System.Text.Json;

namespace Il2CppSymbolReader;

/// <summary>
/// IL2CPP地址解析器，用于解析从堆栈跟踪获取的C++地址
/// </summary>
public class Il2CppAddressResolver : IDisposable
{
    private readonly UsymReader _symbolReader;
    private readonly ulong _imageBase;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="symbolFilePath">符号文件路径</param>
    /// <param name="imageBase">镜像基地址，如果未知可以设为0</param>
    public Il2CppAddressResolver(string symbolFilePath, ulong imageBase = 0)
    {
        _symbolReader = new UsymReader(symbolFilePath);
        _imageBase = imageBase;
    }
    
    /// <summary>
    /// 解析地址字符串（来自StackTrace.cpp中的filename字段）
    /// 注意：C++代码使用%zu格式，产生十进制字符串
    /// </summary>
    /// <param name="addressString">十进制地址字符串</param>
    /// <returns>解析结果</returns>
    public AddressResolutionResult? ResolveAddress(string addressString)
    {
        // C++代码使用snprintf(buffer, sizeof(buffer), "%zu", fileNamePtr)
        // 所以这里期望的是十进制格式的字符串
        if (!ulong.TryParse(addressString, out ulong address))
        {
            return null;
        }
        
        return ResolveAddress(address);
    }
    
    /// <summary>
    /// 解析内存地址
    /// </summary>
    /// <param name="address">内存地址</param>
    /// <returns>解析结果</returns>
    public AddressResolutionResult? ResolveAddress(ulong address)
    {
        // 如果设置了镜像基地址，需要转换为相对地址
        ulong relativeAddress = _imageBase > 0 ? address - _imageBase : address;
        
        var symbol = _symbolReader.FindSymbol(relativeAddress);
        if (!symbol.HasValue)
        {
            return null;
        }
        
        var frames = _symbolReader.GetStackFrames(relativeAddress);
        
        return new AddressResolutionResult
        {
            OriginalAddress = address,
            RelativeAddress = relativeAddress,
            PrimarySymbol = symbol.Value,
            AllFrames = frames,
            ImageBase = _imageBase
        };
    }
    
    /// <summary>
    /// 批量解析地址
    /// </summary>
    /// <param name="addresses">地址列表</param>
    /// <returns>解析结果列表</returns>
    public List<AddressResolutionResult> ResolveAddresses(IEnumerable<string> addresses)
    {
        var results = new List<AddressResolutionResult>();
        
        foreach (var addressStr in addresses)
        {
            var result = ResolveAddress(addressStr);
            if (result.HasValue)
            {
                results.Add(result.Value);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 将解析结果导出为JSON格式
    /// </summary>
    /// <param name="results">解析结果</param>
    /// <returns>JSON字符串</returns>
    public static string ExportToJson(IEnumerable<AddressResolutionResult> results)
    {
        var exportData = results.Select(r => new
        {
            OriginalAddress = $"0x{r.OriginalAddress:X}",
            RelativeAddress = $"0x{r.RelativeAddress:X}",
            FileName = r.PrimarySymbol.FileName,
            LineNumber = r.PrimarySymbol.LineNumber,
            MethodIndex = r.PrimarySymbol.MethodIndex,
            AllFrames = r.AllFrames.Select(f => new
            {
                FileName = f.FileName,
                LineNumber = f.LineNumber,
                MethodIndex = f.MethodIndex
            }).ToList()
        });
        
        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    /// <summary>
    /// 创建可读的堆栈跟踪文本
    /// </summary>
    /// <param name="result">解析结果</param>
    /// <returns>格式化的堆栈跟踪</returns>
    public static string FormatStackTrace(AddressResolutionResult result)
    {
        var lines = new List<string>();
        lines.Add($"Address: 0x{result.OriginalAddress:X}");
        
        if (result.ImageBase > 0)
        {
            lines.Add($"Relative: 0x{result.RelativeAddress:X} (Base: 0x{result.ImageBase:X})");
        }
        
        lines.Add("Stack trace:");
        
        for (int i = 0; i < result.AllFrames.Count; i++)
        {
            var frame = result.AllFrames[i];
            lines.Add($"  {i}: {frame.FileName}:{frame.LineNumber} (Method: {frame.MethodIndex})");
        }
        
        return string.Join(Environment.NewLine, lines);
    }
    
    public void Dispose()
    {
        _symbolReader?.Dispose();
    }
}

/// <summary>
/// 地址解析结果
/// </summary>
public struct AddressResolutionResult
{
    /// <summary>
    /// 原始地址
    /// </summary>
    public ulong OriginalAddress { get; init; }
    
    /// <summary>
    /// 相对地址（减去镜像基地址后）
    /// </summary>
    public ulong RelativeAddress { get; init; }
    
    /// <summary>
    /// 主要符号信息
    /// </summary>
    public SymbolInfo PrimarySymbol { get; init; }
    
    /// <summary>
    /// 所有堆栈帧（包括内联函数）
    /// </summary>
    public List<SymbolInfo> AllFrames { get; init; }
    
    /// <summary>
    /// 镜像基地址
    /// </summary>
    public ulong ImageBase { get; init; }
}