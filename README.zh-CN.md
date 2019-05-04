# V5DLLAdapter
该程序让以DLL方式编写的旧版C/C++策略能够兼容新的Simuro5v5平台。

[![构建状态](https://ci.appveyor.com/api/projects/status/ws8rm84462xmh6f1/branch/master?svg=true)](https://ci.appveyor.com/project/azurefx/v5dlladapter/branch/master)

[下载最新的CI成功构建版本](https://ci.appveyor.com/api/projects/azurefx/v5dlladapter/artifacts/V5DLLAdapter.zip)

## 概述
新的[比赛平台](https://github.com/npuv5pp/Simuro5v5)改变了策略的加载方式，从平台直接加载DLL文件变为平台通过[网络通信中间件](https://github.com/npuv5pp/V5RPC)与策略服务器进行通信。
本程序提供了一个图形界面，可以加载指定的策略DLL，并作为策略服务器接受平台的调用。

由于比赛规则和接口有所变更，既有的策略DLL需要实现新的接口并重新编译，才能被本程序加载并正常工作。在[这里](https://github.com/npuv5pp/DLLStrategy)可以获取新的DLL示例工程。

此外，本程序提供了一个简易的日志查看器。策略DLL或其他程序可以通过跨进程通信方式向本程序写入调试日志。

## 文档

### DLL接口
新DLL需要导出下列函数：
```cpp
void OnEvent(EventType type, void* argument);

void GetTeamInfo(TeamInfo* teamInfo);

void GetInstruction(Field* field);

void GetPlacement(Field* field);
```
本程序默认运行于32位CLR环境下。原则上32位程序只能加载32位DLL，64位程序只能加载64位DLL。因此，DLL必须使用与本程序相同的字长编译。
当DLL编译为32位代码时，所有导出函数必须使用`__cdecl`调用约定。Windows的64位环境中调用约定是统一的，因此设置`__cdecl`或`__stdcall`等没有效果。
关于Windows下DLL的相关知识请参见[这里](https://discourse.juliacn.com/t/topic/1657)。

### 加载DLL并启动策略服务器

TODO

### 使用日志功能

TODO

### 常见问题

TODO

## 作者

该项目当前由AzureFx编写和维护。保留所有权利。

Simuro5v5是西北工业大学V5++团队的项目。
