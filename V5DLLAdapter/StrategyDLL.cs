using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using V5RPC;
using V5RPC.Proto;

namespace V5DLLAdapter
{
    class StrategyDLL : IStrategy, IDisposable
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpLibFileName);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr hLibModule);

        //BEGIN UNMANAGED FUNCTIONS
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetTeamInfoDelegate(ref Native.TeamInfo teamInfo);
        GetTeamInfoDelegate _getTeamInfo;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void OnEventDelegate(EventType type, IntPtr arguments);
        OnEventDelegate _onEvent;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetInstructionDelegate(ref Native.Field field);
        GetInstructionDelegate _getInstruction;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetPlacementDelegate(ref Native.Field field);
        GetPlacementDelegate _getPlacement;
        //END UNMANAGED FUNCTIONS

        IntPtr currentModule = IntPtr.Zero;
        public bool IsLoaded { get { return currentModule != IntPtr.Zero; } }
        string _lastDllPath = null;
        public string DLL { get { return IsLoaded ? _lastDllPath : null; } }

        public const int MAX_STRING_LEN = 128;

        public StrategyDLL()
        {
        }

        private DT LoadFunction<DT>(string funcName) where DT : Delegate
        {
            var ptr = GetProcAddress(currentModule, funcName);
            return (DT)Marshal.GetDelegateForFunctionPointer(ptr, typeof(DT));
        }

        public bool Load(string dllPath)
        {
            if (IsLoaded)
            {
                return false;
            }
            _lastDllPath = dllPath;
            var hModule = LoadLibrary(dllPath);
            if (hModule == IntPtr.Zero)
            {
                return false;
            }
            currentModule = hModule;
            try
            {
                //BEGIN UNMANAGED FUNCTIONS
                _getTeamInfo = LoadFunction<GetTeamInfoDelegate>("GetTeamInfo");
                _onEvent = LoadFunction<OnEventDelegate>("OnEvent");
                _getInstruction = LoadFunction<GetInstructionDelegate>("GetInstruction");
                _getPlacement = LoadFunction<GetPlacementDelegate>("GetPlacement");
                //END UNMANAGED FUNCTIONS
            }
            catch
            {
                Unload();
                return false;
            }
            return true;
        }

        public void Unload()
        {
            if (IsLoaded)
            {
                FreeLibrary(currentModule);
            }
            currentModule = IntPtr.Zero;
            //BEGIN UNMANAGED FUNCTIONS
            _getTeamInfo = null;
            _onEvent = null;
            _getInstruction = null;
            _getPlacement = null;
            //END UNMANAGED FUNCTIONS
        }

        public void Dispose()
        {
            Unload();
        }

        [HandleProcessCorruptedStateExceptions]
        void IStrategy.OnEvent(EventType type, EventArguments arguments)
        {
            if (_getTeamInfo == null)
            {
                throw new DllNotFoundException();
            }
            switch (arguments.ArgumentCase)
            {
                case EventArguments.ArgumentOneofCase.JudgeResult:
                    {
                        var args = new Native.JudgeResultEvent
                        {
                            type = arguments.JudgeResult.Type,
                            offensiveTeam = arguments.JudgeResult.OffensiveTeam,
                            reason = arguments.JudgeResult.Reason
                        };
                        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(args));
                        Marshal.StructureToPtr(args, ptr, false);
                        try
                        {
                            _onEvent(type, ptr);
                        }
                        catch (Exception e)
                        {
                            throw new DLLException("OnEvent", e);
                        }
                        finally
                        {
                            Marshal.DestroyStructure<Native.JudgeResultEvent>(ptr);
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    return;
            }
            _onEvent(type, IntPtr.Zero);
        }

        [HandleProcessCorruptedStateExceptions]
        TeamInfo IStrategy.GetTeamInfo()
        {
            if (_getTeamInfo == null)
            {
                throw new DllNotFoundException();
            }
            var teamInfo = new Native.TeamInfo();
            try
            {
                _getTeamInfo(ref teamInfo);
            }
            catch (Exception e)
            {
                throw new DLLException("GetTeamInfo", e);
            }
            return new TeamInfo
            {
                TeamName = teamInfo.teamName
            };
        }

        Wheel[] IStrategy.GetInstruction(Field field)
        {
            if (_getInstruction == null)
            {
                throw new DllNotFoundException();
            }
            var nativeField = new Native.Field(field);
            try
            {
                _getInstruction(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DLLException("GetInstruction", e);
            }
            return (from x in nativeField.SelfRobots select (Wheel)x.wheel).ToArray();
        }

        Placement IStrategy.GetPlacement(Field field)
        {
            if (_getPlacement == null)
            {
                throw new DllNotFoundException();
            }
            var nativeField = new Native.Field(field);
            try
            {
                _getPlacement(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DLLException("GetPlacement", e);
            }
            return new Placement
            {
                Ball = (Ball)nativeField.ball,
                Robots = { from x in nativeField.SelfRobots select (Robot)x }
            };
        }
    }

    class DLLException : Exception
    {
        string _functionName;
        public override string Message { get { return $"在 DLL 导出的函数 {_functionName} 中发生异常"; } }
        public Exception MaskedInnerException { get; }

        public DLLException(string functionName, Exception maskedInnerException)
        {
            _functionName = functionName;
            MaskedInnerException = maskedInnerException;
        }
    }

}
