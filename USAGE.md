# Il2CppSymbolReader 使用指南

## 快速开始

### 1. 获取可执行文件
运行以下命令构建可执行文件：
```bash
dotnet publish -c Release --self-contained false -p:PublishSingleFile=true
```

可执行文件位置：`bin\Release\net9.0\win-x64\publish\Il2CppSymbolReader.exe`

### 2. 基本用法

```bash
# 查看帮助
Il2CppSymbolReader help

# 读取符号文件信息
Il2CppSymbolReader read il2cpp.usym

# 解析单个地址（十进制格式，来自C++堆栈跟踪）
Il2CppSymbolReader lookup il2cpp.usym 1234567890

# 批量解析多个地址（推荐用于异常调试）
Il2CppSymbolReader resolve il2cpp.usym 1234567890 2345678901 3456789012
```

## 与您的C++修改集成

### C++端（已完成）
您修改的`StackTrace.cpp`会在异常堆栈的`filename`字段输出十进制地址：
```cpp
snprintf(buffer, sizeof(buffer), "%zu", fileNamePtr);  // 输出：1234567890
```

### 使用此工具解析
```bash
# 直接解析从异常堆栈获取的地址
Il2CppSymbolReader resolve il2cpp.usym 1234567890
```

输出示例：
```
Address: 1234567890
Address: 0x499602D2
Stack frames (including inlined functions):
  Frame 0:
    File: Assets/Scripts/MyScript.cs
    Line: 42
    Method Index: 1234
    Address: 0x499602D2
```

## 作为类库使用

```csharp
using Il2CppSymbolReader;

// 解析地址字符串（来自异常堆栈）
using var resolver = new Il2CppAddressResolver("il2cpp.usym");
var result = resolver.ResolveAddress("1234567890");

if (result.HasValue)
{
    Console.WriteLine($"文件: {result.Value.PrimarySymbol.FileName}");
    Console.WriteLine($"行号: {result.Value.PrimarySymbol.LineNumber}");
}
```

## 输出格式

### JSON导出
使用`resolve`命令会自动生成JSON格式的结果，方便集成到其他工具中。

### 格式化堆栈跟踪
工具会自动格式化堆栈跟踪，显示完整的调用链（包括内联函数）。

## 注意事项

1. **地址格式**：输入十进制地址（不是十六进制）
2. **符号文件**：确保`il2cpp.usym`文件与可执行文件版本匹配
3. **文件位置**：符号文件通常与Unity构建的可执行文件在同一目录