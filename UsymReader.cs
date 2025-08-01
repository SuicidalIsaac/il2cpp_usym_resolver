using System.Runtime.InteropServices;
using System.Text;

namespace Il2CppSymbolReader;

/// <summary>
/// IL2CPP usym符号文件读取器
/// </summary>
public class UsymReader : IDisposable
{
    private const uint MagicUsymlite = 0x2D6D7973; // "sym-"
    private const uint NoLine = 0xFFFFFFFF;
    private const int HeaderSize = 24;
    private const int LineSize = 24;
    
    private readonly MemoryMappedFile? _mappedFile;
    private readonly UsymHeader _header;
    private readonly UsymLine[] _lines;
    private readonly byte[] _stringData;
    private readonly bool _isValid;

    /// <summary>
    /// 从文件路径加载usym文件
    /// </summary>
    /// <param name="filePath">usym文件路径</param>
    public UsymReader(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Symbol file not found: {filePath}");
        }

        try
        {
            _mappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "usym", 0, MemoryMappedFileAccess.Read);
            var accessor = _mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            
            // 读取文件头
            var headerBytes = new byte[HeaderSize];
            accessor.ReadArray(0, headerBytes, 0, HeaderSize);
            
            unsafe
            {
                fixed (byte* ptr = headerBytes)
                {
                    _header = Marshal.PtrToStructure<UsymHeader>((IntPtr)ptr);
                }
            }
            
            // 验证魔术数字
            if (_header.Magic != MagicUsymlite)
            {
                throw new InvalidDataException($"Invalid usym file magic: 0x{_header.Magic:X8}");
            }
            
            if (_header.LineCount == 0)
            {
                throw new InvalidDataException("Empty usym file");
            }
            
            // 计算偏移量
            var lineOffset = HeaderSize;
            var stringOffset = lineOffset + ((int)_header.LineCount * LineSize);
            var fileLength = accessor.Capacity;
            
            // 读取行数据
            var lineBytes = new byte[_header.LineCount * LineSize];
            accessor.ReadArray(lineOffset, lineBytes, 0, lineBytes.Length);
            
            // 读取字符串数据
            var stringLength = fileLength - stringOffset;
            var stringBytes = new byte[stringLength];
            accessor.ReadArray(stringOffset, stringBytes, 0, (int)stringLength);
            
            // 转换数据
            _lines = MemoryMarshal.Cast<byte, UsymLine>(lineBytes).ToArray();
            _stringData = stringBytes;
            
            _isValid = true;
            accessor.Dispose();
        }
        catch
        {
            _mappedFile?.Dispose();
            _isValid = false;
            throw;
        }
    }
    
    /// <summary>
    /// 检查符号文件是否有效
    /// </summary>
    public bool IsValid => _isValid;
    
    /// <summary>
    /// 获取符号文件头信息
    /// </summary>
    public UsymHeader Header => _header;
    
    /// <summary>
    /// 根据地址查找符号信息
    /// </summary>
    /// <param name="address">内存地址</param>
    /// <returns>符号信息，如果找不到返回null</returns>
    public SymbolInfo? FindSymbol(ulong address)
    {
        if (!_isValid || _lines.Length == 0)
            return null;
            
        // 二分查找最接近的地址
        var line = FindLine(address);
        
        // 检查是否为有效行
        if (line.Line == NoLine)
            return null;
            
        return new SymbolInfo
        {
            FileName = GetString(line.FileName),
            LineNumber = line.Line,
            MethodIndex = line.MethodIndex,
            Parent = line.Parent,
            Address = line.Address
        };
    }
    
    /// <summary>
    /// 根据地址获取所有相关的堆栈帧信息（包括内联函数）
    /// </summary>
    /// <param name="address">内存地址</param>
    /// <returns>堆栈帧信息列表</returns>
    public List<SymbolInfo> GetStackFrames(ulong address)
    {
        var frames = new List<SymbolInfo>();
        
        if (!_isValid)
            return frames;
            
        var line = FindLine(address);
        if (line.Line == NoLine)
            return frames;
            
        InsertStackFrame(line, frames);
        return frames;
    }
    
    /// <summary>
    /// 获取字符串表中的字符串
    /// </summary>
    /// <param name="offset">字符串偏移量</param>
    /// <returns>字符串内容</returns>
    public string? GetString(uint offset)
    {
        if (offset >= _stringData.Length)
            return null;
            
        var nullIndex = Array.IndexOf(_stringData, (byte)0, (int)offset);
        if (nullIndex == -1)
            nullIndex = _stringData.Length;
            
        var length = nullIndex - (int)offset;
        return Encoding.UTF8.GetString(_stringData, (int)offset, length);
    }
    
    /// <summary>
    /// 获取所有符号信息（调试用）
    /// </summary>
    /// <returns>所有符号信息</returns>
    public IEnumerable<SymbolInfo> GetAllSymbols()
    {
        if (!_isValid)
            yield break;
            
        for (int i = 0; i < _lines.Length; i++)
        {
            var line = _lines[i];
            if (line.Line != NoLine)
            {
                yield return new SymbolInfo
                {
                    FileName = GetString(line.FileName),
                    LineNumber = line.Line,
                    MethodIndex = line.MethodIndex,
                    Parent = line.Parent,
                    Address = line.Address
                };
            }
        }
    }
    
    /// <summary>
    /// 二分查找指定地址对应的行
    /// </summary>
    private UsymLine FindLine(ulong address)
    {
        uint head = 0;
        uint tail = (uint)_lines.Length - 1;
        
        while (head < tail)
        {
            uint mid = (head + tail + 1) / 2;
            ulong midAddr = _lines[(int)mid].Address;
            
            if (address < midAddr)
            {
                tail = mid - 1;
            }
            else
            {
                head = mid;
            }
        }
        
        ulong foundAddr = _lines[(int)head].Address;
        
        // 找到具有相同地址的最后一个条目
        while (head + 1 < _lines.Length && _lines[(int)(head + 1)].Address == foundAddr)
        {
            head += 1;
        }
        
        return _lines[(int)head];
    }
    
    /// <summary>
    /// 递归插入堆栈帧（处理内联函数）
    /// </summary>
    private void InsertStackFrame(UsymLine line, List<SymbolInfo> stackFrames)
    {
        // 先处理父级
        if (line.Parent != NoLine && line.Parent < _lines.Length)
        {
            InsertStackFrame(_lines[(int)line.Parent], stackFrames);
        }
        
        // 然后添加当前帧
        stackFrames.Add(new SymbolInfo
        {
            FileName = GetString(line.FileName),
            LineNumber = line.Line,
            MethodIndex = line.MethodIndex,
            Parent = line.Parent,
            Address = line.Address
        });
    }
    
    public void Dispose()
    {
        _mappedFile?.Dispose();
    }
}