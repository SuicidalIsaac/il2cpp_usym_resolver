# IL2CPP Symbol Reader

这是一个用于读取和解析 Unity IL2CPP 符号文件（.usym）的 .NET 9 类库。它可以将 C++ 地址转换为对应的源码文件名和行号信息。

## 功能特性

- **符号文件读取**: 解析 Unity IL2CPP 生成的 .usym 符号文件
- **地址映射**: 将内存地址映射到源码文件和行号
- **堆栈重建**: 支持内联函数的完整堆栈帧重建
- **高性能**: 使用内存映射文件和二分查找算法
- **跨平台**: 基于 .NET 9，支持 Windows/Linux/macOS

## 项目结构

```
Il2CppSymbolReader/
├── UsymStructures.cs     # usym文件格式定义
├── UsymReader.cs         # 主要的符号读取器
├── MemoryMappedFile.cs   # 内存映射文件实现
└── README.md

Il2CppSymbolReader.TestApp/
└── Program.cs            # 测试应用程序
```

## 使用方法

### 基本用法

```csharp
using Il2CppSymbolReader;

// 加载符号文件
using var reader = new UsymReader("path/to/il2cpp.usym");

// 查找符号信息
ulong address = 0x1400010A0;
var symbol = reader.FindSymbol(address);

if (symbol.HasValue)
{
    Console.WriteLine($"File: {symbol.Value.FileName}");
    Console.WriteLine($"Line: {symbol.Value.LineNumber}");
    Console.WriteLine($"Method Index: {symbol.Value.MethodIndex}");
}
```

### 获取完整堆栈帧

```csharp
// 获取包括内联函数在内的完整堆栈帧
var frames = reader.GetStackFrames(address);
foreach (var frame in frames)
{
    Console.WriteLine($"{frame.FileName}:{frame.LineNumber}");
}
```

### 命令行工具

项目包含一个测试应用程序，提供命令行接口：

```bash
# 读取符号文件基本信息
dotnet run --project Il2CppSymbolReader.TestApp read il2cpp.usym

# 查找特定地址的符号信息
dotnet run --project Il2CppSymbolReader.TestApp lookup il2cpp.usym 0x1400010A0

# 导出所有符号（前100个）
dotnet run --project Il2CppSymbolReader.TestApp dump il2cpp.usym
```

## USYM 文件格式

USYM 文件是 Unity IL2CPP 生成的二进制符号文件，包含以下结构：

### 文件头 (24字节)
- `Magic` (4字节): 魔术数字 0x2D6D7973 ("sym-")
- `Version` (4字节): 版本号
- `LineCount` (4字节): 符号行数
- `Id` (4字节): 可执行文件ID在字符串表中的偏移
- `Os` (4字节): 操作系统在字符串表中的偏移
- `Arch` (4字节): 架构在字符串表中的偏移

### 符号行 (24字节每行)
- `Address` (8字节): 内存地址
- `MethodIndex` (4字节): 方法索引
- `FileName` (4字节): 文件名在字符串表中的偏移
- `Line` (4字节): 源码行号
- `Parent` (4字节): 父级索引（用于内联函数）

### 字符串表
包含所有文件名、ID等字符串数据，以null结尾。

## 与 C++ 实现的对应关系

这个 C# 实现基于 Unity IL2CPP 运行时中的 `DebugSymbolReader.cpp` 文件，主要对应以下功能：

- `FindLine()` → `FindSymbol()`
- `AddStackFrames()` → `GetStackFrames()`
- `GetString()` → `GetString()`
- `InsertStackFrame()` → `InsertStackFrame()`

## 编译和运行

```bash
# 编译类库
dotnet build Il2CppSymbolReader

# 编译测试应用
dotnet build Il2CppSymbolReader.TestApp

# 运行测试
dotnet run --project Il2CppSymbolReader.TestApp
```

## 使用场景

1. **异常调试**: 将 IL2CPP 运行时异常的 C++ 地址映射回 C# 源码
2. **性能分析**: 分析性能剖析器报告中的地址信息
3. **崩溃分析**: 解析崩溃转储中的地址信息
4. **调试工具**: 开发自定义调试和分析工具

## 注意事项

- USYM 文件必须与可执行文件版本匹配
- 需要确保符号文件在构建时正确生成
- 地址查找使用二分搜索，时间复杂度为 O(log n)
- 内存映射文件提供高效的文件访问性能